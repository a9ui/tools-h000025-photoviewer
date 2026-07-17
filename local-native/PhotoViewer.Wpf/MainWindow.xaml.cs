using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoViewer.Wpf;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp", ".gif", ".tif", ".tiff",
    };
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    private const int MinParallelThumbnailCount = 32;
    private const int MaxThumbnailDecodeWorkers = 12;
    private const int MaxMetadataReadWorkers = 4;
    private const int MaxPngMetadataChunkBytes = 4 * 1024 * 1024;
    private const int SearchFilterDebounceMilliseconds = 150;
    private const int SearchStateSaveDebounceMilliseconds = 300;
    private const int InitialGridRealizationCount = 96;
    private const int GridRealizationBatchSize = 96;
    private const int MaxGridRealizationCount = 384;
    private const int MaxRecentFolderSets = 8;
    private const int PersistenceLockTimeoutMilliseconds = 2_000;
    private const int PersistenceLockRetryMilliseconds = 25;
    private static readonly TimeSpan PersistenceLockStaleAfter = TimeSpan.FromSeconds(30);
    private const double DefaultCardWidth = 200;
    private const double CardWidthStep = 20;
    private const double DefaultRightPanelWidth = 340;
    private const double MinRightPanelWidth = 240;
    private const double MaxRightPanelWidth = 900;
    private const double ModalZoomMin = 0.25;
    private const double ModalZoomMax = 10;
    private const double ModalZoomKeyboardStep = 1.15;
    private const double ModalZoomWheelStep = 1.1;
    private const string DisplayStyleStandard = "standard";
    private const string DisplayStyleCompact = "compact";
    private const string DisplayStylePoster = "poster";
    private const string AspectOriginalValue = "original";
    private const string AspectSquareValue = "square";
    private const string AspectPortraitValue = "portrait";
    private const string SortModifiedNewestValue = "modified-newest";
    private const string SortModifiedOldestValue = "modified-oldest";
    private const string SortCreatedNewestValue = "created-newest";
    private const string SortCreatedOldestValue = "created-oldest";
    private const string SortNameValue = "name";
    private const string SortRandomValue = "random";
    // Runtime state is manual From/To only. These names are accepted only while migrating old files.
    private const string DatePresetNoneValue = "none";
    private const string DatePresetManualValue = "manual";
    private const string ModalMetadataPromptTab = "prompt";
    private const string ModalMetadataNegativeTab = "negative";
    private const string ModalMetadataSettingsTab = "settings";
    private const int MinFavoriteFilterLevel = 1;
    private const int MaxFavoriteFilterLevel = 5;
    private const int MaxPersistedPreviewTabs = 30;
    private static readonly JsonSerializerOptions SharedRecentJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    private readonly ObservableCollection<Tile> _tiles = new();
    private readonly ObservableCollection<Tile> _gridTiles = new();
    private readonly ObservableCollection<string> _landingFolderSet = new();
    private readonly ObservableCollection<FolderBucketView> _folderBucketViews = new();
    private readonly ObservableCollection<RecentFolderSetView> _recentFolderSetViews = new();
    private readonly ObservableCollection<PreviewTabView> _previewTabs = new();
    private readonly List<Tile> _allTiles = new();
    private readonly List<Tile> _closedPreviewTabs = new();
    private readonly HashSet<string> _pinnedPreviewPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _favoriteDirtyPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenDirtyPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _hiddenFolderBuckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedFolderBucketKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _enhancedOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _restoredPreviewTabPaths = [];
    private int _gridStartIndex;
    private int _lastInitialUnseenCount;
    private int _enhancementJobsRead;
    private int _enhancedCandidateCount;
    private int _favoriteSaveAttemptCount;
    private bool _enhancementReadOk = true;
    private string? _enhancementReadError;
    private Rect _restoreBounds;
    private bool _fakeMaximized;
    private bool _initializing = true;
    private bool _suppressStateSave;
    private bool _favoritesWriteBlocked;
    private bool _seenWriteBlocked;
    private bool _stateWriteBlocked;
    private double _rightPanelWidth = DefaultRightPanelWidth;
    private Dictionary<string, JsonElement>? _stateExtensionData;
    private bool _syncingSelection;
    private bool _syncingFavoriteFilterControls;
    private bool _syncingDateControls;
    private bool _dateFilterMigrationPending;
    private FileDragSession? _fileDragSession;
    private bool _suppressPreviewClickAfterFileDrag;
    private bool _settingSearchQuery;
    private string? _currentFolder;
    private List<string> _currentFolderSet = [];
    private List<string> _lastFolderSet = [];
    private string? _restoredSelectedPath;
    private string? _primarySelectedPath;
    private string? _activePreviewTabPath;
    private string? _restoredActivePreviewTabPath;
    private bool _previewTabsPersistenceReady = true;
    private string? _hoverPreviewTabPath;
    private string? _hoverPreviewTabBitmapPath;
    private CancellationTokenSource? _previewTabHoverCts;
    private TaskCompletionSource<PreviewTabHoverDecodeCompletion>? _previewTabHoverCompletion;
    private TaskCompletionSource<PreviewTabHoverDecodeCompletion>? _lastPreviewTabHoverCompletion;
    private readonly Dictionary<string, int> _previewTabHoverDecodeDelaysForSmoke = new(StringComparer.OrdinalIgnoreCase);
    private long _previewTabHoverGeneration;
    private int _previewTabHoverDecodeStartCount;
    private int _previewTabHoverDecodeFailureCount;
    private Point? _previewTabDragStartPoint;
    private PreviewTabView? _previewTabDragSource;
    private string? _modalTransformPath;
    private string? _modalSourceTilePath;
    private string? _modalDisplayPath;
    private Point? _modalPanStartPoint;
    private Vector _modalPanStartOffset;
    private Point? _modalPointerStartPoint;
    private bool _modalPointerMoved;
    private bool _modalChromeVisible = true;
    private long _modalSingleClickGeneration;
    private readonly DispatcherTimer _modalFeedbackTimer;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _modalCts;
    private TaskCompletionSource<bool>? _modalDecodeCompletion;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _previewMetadataCts;
    private TaskCompletionSource<PreviewDecodeResult>? _previewDecodeCompletion;
    private TaskCompletionSource<PngParametersMetadata?>? _previewMetadataCompletion;
    private PngParametersMetadata? _currentPreviewMetadata;
    private string? _currentPreviewMetadataPath;
    private string _lastMetadataCopyText = "";
    private int _previewUpdateCount;
    private long _previewMs;
    private int _previewDeferredDecodeCount;
    private long _previewDeferredDecodeMs;
    private long _lastPreviewImmediateMs;
    private string? _previewDecodedPath;
    private readonly DispatcherTimer _searchFilterTimer;
    private readonly DispatcherTimer _searchStateSaveTimer;
    private CancellationTokenSource? _searchFilterCts;
    private long _searchFilterGeneration;
    private long _scheduledSearchFilterGeneration;
    private long _lastAppliedSearchFilterGeneration;
    private TaskCompletionSource<SearchFilterCompletion>? _pendingSearchFilterCompletion;
    private string _displayStyle = DisplayStyleStandard;
    private string _aspectMode = AspectOriginalValue;
    private string _sortBy = SortModifiedNewestValue;
    private string _randomSortSeed = "default";
    private string _datePreset = DatePresetNoneValue;
    private DateTime? _dateFromLocal;
    private DateTime? _dateToLocal;
    private readonly HashSet<int> _favoriteFilterLevels = [];
    private bool _showUnseenDots;
    private bool _foldersSectionExpanded = true;
    private bool _syncingFolderBucketSelection;
    private string? _primarySelectedFolderBucketKey;
    private bool _restoringGridZoomAnchor;
    private string? _lastGridZoomAnchorPath;
    private double _lastGridZoomAnchorDrift;
    private double _modalZoom = 1;
    private double _modalFitScale = 1;
    private bool _modalFitUpdateQueued;
    private bool _modalFlipped;
    private double _modalPanX;
    private double _modalPanY;
    private bool _modalShowingEnhanced;
    private bool _confirmBeforeDelete = true;
    private Tile? _pendingDeleteTile;
    private DeleteSnapshot? _pendingBulkDeleteSnapshot;
    private Func<string, RecycleBinDeleteResult> _recycleBinDelete = SendFileToWindowsRecycleBin;
    private Func<string, string> _resolveFinalPath = ResolveFinalPathCore;
    private string _deleteStatus = "";
    private Action? _statusRetryAction;
    private IInputElement? _deleteFocusBeforeDialog;
    private IInputElement? _settingsFocusBeforeDialog;
    public LoadMetrics? LastLoadMetrics { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        _searchFilterTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(SearchFilterDebounceMilliseconds),
        };
        _searchFilterTimer.Tick += SearchFilterTimer_Tick;
        _searchStateSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(SearchStateSaveDebounceMilliseconds),
        };
        _searchStateSaveTimer.Tick += SearchStateSaveTimer_Tick;
        _modalFeedbackTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(650),
        };
        _modalFeedbackTimer.Tick += (_, _) =>
        {
            _modalFeedbackTimer.Stop();
            if (ModalInteractionFeedback is not null)
                ModalInteractionFeedback.Visibility = Visibility.Collapsed;
        };
        LandingFolderSetList.ItemsSource = _landingFolderSet;
        SidebarFolderSetList.ItemsSource = _folderBucketViews;
        RecentFolderSetList.ItemsSource = _recentFolderSetViews;
        PreviewTabList.ItemsSource = _previewTabs;
        RestoreState();
        BuildSampleTiles();
        _allTiles.AddRange(_tiles);
        _tiles.Clear();
        ApplyCardLayoutToAllTiles();

        CardsList.ItemsSource = BuildGroupedView(_gridTiles);
        RowsList.ItemsSource = BuildGroupedView(_tiles);
        CardsList.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(CardsList_ScrollChanged));
        ApplyFilters(selectFirst: false);

        Loaded += (_, _) =>
        {
            if (CardsList.Items.Count > 0)
                CardsList.SelectedIndex = 0;
        };
        Closed += (_, _) => CancelPreviewTabHoverDecode();
        CardsList.MouseDoubleClick += (_, _) => OpenModal();
        RowsList.MouseDoubleClick += (_, _) => OpenModal();
        SizeChanged += (_, _) => ScheduleModalFitUpdate();
        RefreshLandingFolderSetUi();
        RefreshPreviewTabs();
        SetPhase(landing: true);
        _initializing = false;
        if (_dateFilterMigrationPending)
            SaveState();
    }

    private static System.ComponentModel.ICollectionView BuildGroupedView(ObservableCollection<Tile> source)
    {
        var cvs = new CollectionViewSource { Source = source };
        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Tile.Group)));
        return cvs.View;
    }

    private sealed record FilterSnapshot(
        FilterTileSnapshot[] Tiles,
        string[] QueryTokens,
        bool FavoritesOnly,
        bool UnfavoriteOnly,
        bool EnhancedOnly,
        bool UnseenOnly,
        FrozenSet<int> FavoriteLevels,
        FrozenSet<string> HiddenFolderBuckets,
        DateTime? DateFromLocal,
        DateTime? DateToLocal,
        string SortBy,
        string RandomSortSeed);

    private sealed record FilterTileSnapshot(
        Tile Tile,
        string FileName,
        string Prompt,
        string Path,
        bool IsRealFile,
        int FavoriteLevel,
        bool Enhanced,
        bool Unseen,
        string FolderBucketKey,
        DateTime ModifiedUtc,
        DateTime CreatedUtc);

    private sealed record FilterResult(List<Tile> Tiles);

    private sealed record FileDragSession(FrameworkElement Surface, Tile Origin, Point Start, bool OriginWasSelected);

    public readonly record struct SearchFilterCompletion(bool Applied, bool Discarded, string? Error)
    {
        public static SearchFilterCompletion AppliedResult => new(true, false, null);
        public static SearchFilterCompletion DiscardedResult => new(false, true, null);
    }

    public readonly record struct PreviewTabHoverDecodeCompletion(bool Applied, bool Discarded, bool Failed, string? Error)
    {
        public static PreviewTabHoverDecodeCompletion AppliedResult => new(true, false, false, null);
        public static PreviewTabHoverDecodeCompletion DiscardedResult => new(false, true, false, null);
        public static PreviewTabHoverDecodeCompletion FailedResult => new(false, false, true, "image could not be decoded");
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await ChooseAndLoadFolderAsync();

    private async Task ChooseAndLoadFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select image folders",
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) == true)
        {
            var folders = dialog.FolderNames.Length > 0 ? dialog.FolderNames : [dialog.FolderName];
            AppendLandingFolders(folders);
            SetPhase(landing: true);
            await Task.CompletedTask;
        }
    }

    public async Task LoadFolderAsync(string folder)
        => await LoadFolderSetAsync([folder]);

    public async Task LoadFolderSetAsync(IEnumerable<string> folders)
    {
        var totalWatch = Stopwatch.StartNew();
        LastLoadMetrics = null;
        _previewUpdateCount = 0;
        _previewMs = 0;
        _previewDeferredDecodeCount = 0;
        _previewDeferredDecodeMs = 0;
        var requestedFolderSet = NormalizeFolderSet(folders);
        var existingFolderSet = requestedFolderSet.Where(Directory.Exists).ToList();
        if (existingFolderSet.Count == 0)
        {
            SetLandingFolderSet(requestedFolderSet);
            LandingFolderStatusText.Text = requestedFolderSet.Count == 0
                ? "No folders selected yet."
                : "Selected folders are unavailable.";
            SetPhase(landing: true);
            return;
        }

        string resolvedFolderSummary = FormatFolderSetSummary(existingFolderSet);

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        Landing.Visibility = Visibility.Visible;
        LandingPanel.IsEnabled = false;
        ScanPanel.Visibility = Visibility.Visible;
        ScanBar.Value = 0;
        ScanPercent.Text = "0%";
        ScanLabel.Text = "Scanning...";
        ScanMessage.Text = resolvedFolderSummary;

        IReadOnlyList<FileInfo> files;
        var scanAccessFailures = new ConcurrentQueue<string>();
        var scanWatch = Stopwatch.StartNew();
        try
        {
            files = await Task.Run(
                () => existingFolderSet
                    .SelectMany(folder => EnumerateImageFiles(folder, scanAccessFailures))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToList(),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        scanWatch.Stop();

        if (cts.IsCancellationRequested)
            return;
        if (!scanAccessFailures.IsEmpty)
            ReportScanAccessFailures(scanAccessFailures.Count);

        ImageMetadataLoadMetrics metadata = ImageMetadataLoadMetrics.Empty;
        if (files.Count > 0)
        {
            ScanLabel.Text = "Reading image metadata...";
            try
            {
                metadata = await Task.Run(() => ReadImageMetadata(files, cts.Token), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (metadata.DecodeFailures > 0)
                SetStatusToast($"{metadata.DecodeFailures:N0} image file(s) could not be decoded. They remain listed; refresh after fixing the files.");
        }

        var materializeWatch = Stopwatch.StartNew();
        Tile? restoredActivePreviewTile = null;
        bool previousSuppress = _suppressStateSave;
        _suppressStateSave = true;
        try
        {
            _currentFolderSet = existingFolderSet;
            _currentFolder = _currentFolderSet.FirstOrDefault();
            SetLandingFolderSet(_currentFolderSet);
            LoadFavorites();
            LoadSeenState();
            LoadEnhancedState();
            _allTiles.Clear();
            _tiles.Clear();
            double width = SizeSlider?.Value ?? 190;
            foreach (var file in files)
                _allTiles.Add(MakeFileTile(file, width, metadata.Dimensions, metadata.Prompts));
            _lastInitialUnseenCount = _allTiles.Count(static tile => tile.Unseen);
            PruneHiddenFolderBucketsToCurrentSet();
            RefreshFolderBucketViews();

            FolderPathText.Text = resolvedFolderSummary;
            ApplyFilters(selectFirst: false);
            restoredActivePreviewTile = ReconcilePreviewTabsWithCurrentCatalog();
            UpdateFolderStats();
        }
        finally
        {
            _suppressStateSave = previousSuppress;
        }
        materializeWatch.Stop();

        if (files.Count == 0)
        {
            SaveState();
            totalWatch.Stop();
            LastLoadMetrics = LoadMetrics.Create(
                resolvedFolderSummary,
                files.Count,
                scanWatch.ElapsedMilliseconds,
                materializeWatch.ElapsedMilliseconds,
                metadata.ElapsedMs,
                metadata.Workers,
                metadata.Completed,
                thumbnailMs: 0,
                thumbnailWorkers: 0,
                thumbnailsCompleted: 0,
                previewMs: _previewMs,
                previewUpdates: _previewUpdateCount,
                previewDeferredDecodeMs: _previewDeferredDecodeMs,
                previewDeferredDecodeCount: _previewDeferredDecodeCount,
                totalWatch.ElapsedMilliseconds);
            UpdateGridMetrics(LastLoadMetrics);
            LandingPanel.IsEnabled = true;
            ScanBar.Value = 0;
            ScanPercent.Text = "0%";
            ScanLabel.Text = "No images found";
            ScanMessage.Text = "Choose another folder set.";
            return;
        }

        SetPhase(landing: false);
        if (restoredActivePreviewTile is not null)
            _restoredSelectedPath = restoredActivePreviewTile.Path;
        SelectRestoredOrFirst();
        SaveState();

        var thumbnails = await LoadThumbnailsAsync(cts.Token);
        totalWatch.Stop();
        LastLoadMetrics = LoadMetrics.Create(
            resolvedFolderSummary,
            files.Count,
            scanWatch.ElapsedMilliseconds,
            materializeWatch.ElapsedMilliseconds,
            metadata.ElapsedMs,
            metadata.Workers,
            metadata.Completed,
            thumbnails.ElapsedMs,
            thumbnails.Workers,
            thumbnails.Completed,
            _previewMs,
            _previewUpdateCount,
            _previewDeferredDecodeMs,
            _previewDeferredDecodeCount,
            totalWatch.ElapsedMilliseconds);
        UpdateGridMetrics(LastLoadMetrics);
    }

    private void ReportScanAccessFailures(int skippedCount)
        => SetStatusToast($"Some folders could not be scanned because access was denied. {skippedCount:N0} location(s) were skipped; fix access and refresh the folder.");

    private int AppendLandingFolders(IEnumerable<string> folders)
    {
        var existing = NormalizeFolderSet(folders)
            .Where(Directory.Exists)
            .ToList();
        int added = 0;
        foreach (string folder in existing)
        {
            if (_landingFolderSet.Any(candidate => string.Equals(candidate, folder, StringComparison.OrdinalIgnoreCase)))
                continue;
            _landingFolderSet.Add(folder);
            added++;
        }

        RefreshLandingFolderSetUi();
        return added;
    }

    private void SetLandingFolderSet(IEnumerable<string> folders)
    {
        _landingFolderSet.Clear();
        foreach (string folder in NormalizeFolderSet(folders))
            _landingFolderSet.Add(folder);
        RefreshLandingFolderSetUi();
    }

    private IReadOnlyList<string> LandingFolderSetSnapshot()
        => _landingFolderSet.ToList();

    private async Task OpenLandingFolderSetAsync()
    {
        var folderSet = LandingFolderSetSnapshot();
        if (folderSet.Count == 0)
        {
            await ChooseAndLoadFolderAsync();
            return;
        }

        await LoadFolderSetAsync(folderSet);
    }

    private void RefreshLandingFolderSetUi()
    {
        if (LandingFolderStatusText is not null)
        {
            int existingCount = _landingFolderSet.Count(Directory.Exists);
            LandingFolderStatusText.Text = _landingFolderSet.Count == 0
                ? "No folders selected yet."
                : $"{existingCount:N0} usable / {_landingFolderSet.Count:N0} selected folder(s)";
        }

        if (OpenFolderSetButton is not null)
            OpenFolderSetButton.IsEnabled = _landingFolderSet.Count > 0;

        RefreshFolderBucketViews();
        RefreshRecentFolderSetViews();
    }

    private void RefreshFolderBucketViews()
    {
        if (SidebarFolderSetList is null)
            return;

        var buckets = _allTiles
            .Where(static tile => tile.IsRealFile && !string.IsNullOrWhiteSpace(tile.FolderBucketKey))
            .GroupBy(static tile => tile.FolderBucketKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                string key = group.Key;
                string label = group.Select(static tile => tile.FolderBucketLabel).FirstOrDefault(static label => !string.IsNullOrWhiteSpace(label)) ?? key;
                int count = group.Count();
                bool hidden = _hiddenFolderBuckets.Contains(key);
                return new FolderBucketView
                {
                    Key = key,
                    Label = label,
                    Path = key,
                    Count = count,
                    Hidden = hidden,
                    IsSelected = _selectedFolderBucketKeys.Contains(key),
                };
            })
            .OrderByDescending(static bucket => bucket.Count)
            .ThenBy(static bucket => bucket.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool wasSyncing = _syncingFolderBucketSelection;
        _syncingFolderBucketSelection = true;
        try
        {
            _folderBucketViews.Clear();
            foreach (var bucket in buckets)
                _folderBucketViews.Add(bucket);

            var availableKeys = buckets.Select(static bucket => bucket.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool hasCurrentCatalogBuckets = _currentFolderSet.Count > 0
                && _allTiles.Any(tile => tile.IsRealFile && _currentFolderSet.Any(root => IsPathWithinRoot(tile.Path, root)));
            if (hasCurrentCatalogBuckets)
                _selectedFolderBucketKeys.IntersectWith(availableKeys);
            if (string.IsNullOrWhiteSpace(_primarySelectedFolderBucketKey)
                || !_selectedFolderBucketKeys.Contains(_primarySelectedFolderBucketKey))
            {
                _primarySelectedFolderBucketKey = _selectedFolderBucketKeys.FirstOrDefault();
            }
            SynchronizeFolderBucketSelectionControl();
        }
        finally
        {
            _syncingFolderBucketSelection = wasSyncing;
        }

        if (FolderBucketStatusText is not null)
        {
            if (buckets.Count == 0)
            {
                var source = _currentFolderSet.Count > 0 ? _currentFolderSet : _landingFolderSet.ToList();
                FolderBucketStatusText.Text = source.Count == 0 ? "No folder buckets loaded" : "Open the folder set to build buckets.";
            }
            else
            {
                int hidden = buckets.Count(static bucket => bucket.Hidden);
                FolderBucketStatusText.Text = hidden == 0
                    ? $"{buckets.Count:N0} folder bucket(s)"
                    : $"{buckets.Count - hidden:N0} shown / {buckets.Count:N0} folder bucket(s)";
            }
        }
    }

    private void FolderBucketSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingFolderBucketSelection || sender is not ListBox listBox)
            return;

        foreach (FolderBucketView bucket in e.RemovedItems.OfType<FolderBucketView>())
            _selectedFolderBucketKeys.Remove(bucket.Key);
        foreach (FolderBucketView bucket in e.AddedItems.OfType<FolderBucketView>())
            _selectedFolderBucketKeys.Add(bucket.Key);

        _primarySelectedFolderBucketKey = (listBox.SelectedItem as FolderBucketView)?.Key
            ?? e.AddedItems.OfType<FolderBucketView>().LastOrDefault()?.Key
            ?? _selectedFolderBucketKeys.FirstOrDefault();
        foreach (FolderBucketView bucket in _folderBucketViews)
            bucket.IsSelected = _selectedFolderBucketKeys.Contains(bucket.Key);
        if (!_initializing)
            SaveState();
    }

    private void SynchronizeFolderBucketSelectionControl()
    {
        if (SidebarFolderSetList is null)
            return;

        bool wasSyncing = _syncingFolderBucketSelection;
        try
        {
            _syncingFolderBucketSelection = true;
            SidebarFolderSetList.SelectedItems.Clear();
            foreach (FolderBucketView bucket in _folderBucketViews.Where(bucket => _selectedFolderBucketKeys.Contains(bucket.Key)))
                SidebarFolderSetList.SelectedItems.Add(bucket);
            SidebarFolderSetList.SelectedItem = _folderBucketViews.FirstOrDefault(bucket => string.Equals(bucket.Key, _primarySelectedFolderBucketKey, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _syncingFolderBucketSelection = wasSyncing;
        }
    }

    private void SetFolderBucketSelection(IEnumerable<string> keys, string? primaryKey, bool persist)
    {
        var available = _folderBucketViews.Select(static bucket => bucket.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selectedFolderBucketKeys.Clear();
        foreach (string key in keys.Where(static key => !string.IsNullOrWhiteSpace(key)))
        {
            if (available.Contains(key))
                _selectedFolderBucketKeys.Add(key);
        }

        _primarySelectedFolderBucketKey = !string.IsNullOrWhiteSpace(primaryKey) && _selectedFolderBucketKeys.Contains(primaryKey)
            ? primaryKey
            : _selectedFolderBucketKeys.FirstOrDefault();
        foreach (FolderBucketView bucket in _folderBucketViews)
            bucket.IsSelected = _selectedFolderBucketKeys.Contains(bucket.Key);
        SynchronizeFolderBucketSelectionControl();
        if (persist && !_initializing)
            SaveState();
    }

    private void RefreshRecentFolderSetViews()
    {
        if (RecentFolderSetList is null)
            return;

        var read = ReadSharedRecentFolders();
        if (!read.Ok)
            ReportPersistenceRefusal("Recent folder history", ResolvedSharedRecentPath, protectedFile: true);
        _lastFolderSet = read.Recent.LastFolderSet;
        if (LastFolderSetText is not null)
            LastFolderSetText.Text = _lastFolderSet.Count == 0
                ? "  No saved folder set"
                : "  " + FormatFolderSetSummary(_lastFolderSet);

        _recentFolderSetViews.Clear();
        foreach (var folderSet in read.Recent.RecentFolderSets)
        {
            _recentFolderSetViews.Add(new RecentFolderSetView
            {
                FolderSet = folderSet,
                Display = FormatFolderSetSummary(folderSet),
                Detail = FormatRecentFolderSet(folderSet),
            });
        }
    }

    private static string FormatFolderSetSummary(IReadOnlyList<string> folderSet)
    {
        var normalized = NormalizeFolderSet(folderSet);
        return normalized.Count switch
        {
            0 => "No folder set",
            1 => normalized[0],
            _ => $"{normalized[0]} (+{normalized.Count - 1})",
        };
    }

    private async Task<ThumbnailLoadMetrics> LoadThumbnailsAsync(CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        // Catalog entries stay lightweight; decode only the current bounded Grid window.
        var snapshot = _gridTiles.Where(static tile => tile.IsRealFile && tile.Thumbnail is null).ToList();
        int total = snapshot.Count;
        if (total == 0)
            return new ThumbnailLoadMetrics(0, 0, 0, 0);

        if (total < MinParallelThumbnailCount)
            return await LoadThumbnailsSequentiallyAsync(snapshot, token);

        int workers = Math.Max(1, Math.Min(Math.Min(MaxThumbnailDecodeWorkers, Environment.ProcessorCount), total));
        int uiBatchSize = Math.Clamp(total / 48, 16, 48);
        var decoded = new ConcurrentQueue<DecodedThumbnail>();
        int done = 0;

        try
        {
            await Parallel.ForEachAsync(
                snapshot,
                new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = workers },
                async (tile, itemToken) =>
                {
                    BitmapSource? thumbnail = null;
                    try
                    {
                        int decodeWidth = (int)Math.Clamp(tile.CardWidth * 1.4, 180, 520);
                        thumbnail = await Task.Run(() => LoadBitmap(tile.Path, decodeWidth), itemToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        thumbnail = null;
                    }

                    decoded.Enqueue(new DecodedThumbnail(tile, thumbnail));
                    int completed = Interlocked.Increment(ref done);
                    if (completed % uiBatchSize == 0 || completed == total)
                    {
                        await Dispatcher.InvokeAsync(
                            () =>
                            {
                                DrainDecodedThumbnails(decoded);
                                UpdateThumbnailProgress(completed, total);
                            },
                            DispatcherPriority.Background,
                            itemToken);
                    }
                });
        }
        catch (OperationCanceledException)
        {
            watch.Stop();
            return new ThumbnailLoadMetrics(total, workers, done, watch.ElapsedMilliseconds);
        }
        finally
        {
            watch.Stop();
        }

        await Dispatcher.InvokeAsync(
            () =>
            {
                DrainDecodedThumbnails(decoded);
                UpdateThumbnailProgress(done, total);
            },
            DispatcherPriority.Background);

        return new ThumbnailLoadMetrics(total, workers, done, watch.ElapsedMilliseconds);
    }

    private static void DrainDecodedThumbnails(ConcurrentQueue<DecodedThumbnail> decoded)
    {
        while (decoded.TryDequeue(out var item))
            item.Tile.Thumbnail = item.Thumbnail;
    }

    private async Task<ThumbnailLoadMetrics> LoadThumbnailsSequentiallyAsync(IReadOnlyList<Tile> snapshot, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        int total = snapshot.Count;
        int done = 0;

        try
        {
            foreach (var tile in snapshot)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    int decodeWidth = (int)Math.Clamp(tile.CardWidth * 1.4, 180, 520);
                    tile.Thumbnail = await Task.Run(() => LoadBitmap(tile.Path, decodeWidth), token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    tile.Thumbnail = null;
                }

                done++;
                UpdateThumbnailProgress(done, total);
            }
        }
        catch (OperationCanceledException)
        {
            watch.Stop();
            return new ThumbnailLoadMetrics(total, Workers: 1, done, watch.ElapsedMilliseconds);
        }
        finally
        {
            watch.Stop();
        }

        return new ThumbnailLoadMetrics(total, Workers: 1, done, watch.ElapsedMilliseconds);
    }

    private void UpdateThumbnailProgress(int done, int total)
    {
        if (done % 8 != 0 && done != total)
            return;

        double progress = done * 100.0 / total;
        ScanBar.Value = progress;
        ScanPercent.Text = $"{(int)progress}%";
        ScanLabel.Text = $"{done:N0} / {total:N0} thumbnails";
    }

    private static IEnumerable<string> EnumerateImageFiles(string root, ConcurrentQueue<string>? accessFailures = null)
    {
        var pending = new Stack<string>();
        var images = new List<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var folder = pending.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    if (SupportedImageExtensions.Contains(Path.GetExtension(file)))
                        images.Add(file);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                accessFailures?.Enqueue(folder);
            }
            try
            {
                foreach (var child in Directory.EnumerateDirectories(folder))
                    pending.Push(child);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                accessFailures?.Enqueue(folder);
            }
        }

        return images;
    }

    private Tile MakeFileTile(
        FileInfo file,
        double width,
        IReadOnlyDictionary<string, ImageDimensions> dimensions,
        IReadOnlyDictionary<string, string> prompts)
    {
        var modified = file.LastWriteTime;
        int paletteIndex = file.FullName.GetHashCode(StringComparison.OrdinalIgnoreCase) & int.MaxValue;
        bool enhanced = TryGetEnhancedOutputForPath(file.FullName, out string? enhancedOutputPath);
        dimensions.TryGetValue(file.FullName, out var imageSize);
        prompts.TryGetValue(file.FullName, out string? indexedPrompt);
        var folderBucket = ResolveFolderBucket(file.FullName);
        var tile = new Tile
        {
            ArtBase = MakeBaseBrush(paletteIndex),
            ArtGlow = MakeGlowBrush(paletteIndex),
            FileName = file.Name,
            Fav = FavoriteLevelForPath(file.FullName),
            Unseen = !SeenStateContains(file.FullName),
            ShowUnseenDot = _showUnseenDots && !SeenStateContains(file.FullName),
            Group = FormatGroup(modified),
            CardWidth = width,
            ModifiedUtc = file.LastWriteTimeUtc,
            CreatedUtc = file.CreationTimeUtc,
            Prompt = indexedPrompt ?? "",
            Path = file.FullName,
            IsRealFile = true,
            FolderBucketKey = folderBucket.Key,
            FolderBucketLabel = folderBucket.Label,
            ImagePixelWidth = imageSize.Width,
            ImagePixelHeight = imageSize.Height,
            Enhanced = enhanced,
            EnhancedOutputPath = enhancedOutputPath,
            SizeText = FormatBytes(file.Length),
            ModifiedText = modified.ToString("yyyy-MM-dd HH:mm"),
        };
        ApplyCardLayout(tile);
        return tile;
    }

    private static string FormatGroup(DateTime modified)
    {
        var date = modified.Date;
        var today = DateTime.Today;
        return date == today ? $"Today  -  {date:yyyy-MM-dd}"
            : date == today.AddDays(-1) ? $"Yesterday  -  {date:yyyy-MM-dd}"
            : date.ToString("yyyy-MM-dd");
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.#} {units[unit]}";
    }

    private FolderBucketIdentity ResolveFolderBucket(string filePath)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch
        {
            fullPath = filePath;
        }

        string? bestRoot = null;
        foreach (string root in _currentFolderSet)
        {
            if (!IsPathWithinRoot(fullPath, root))
                continue;

            if (bestRoot is null || root.Length > bestRoot.Length)
                bestRoot = root;
        }

        bestRoot ??= Path.GetDirectoryName(fullPath) ?? fullPath;
        return new FolderBucketIdentity(bestRoot, FormatFolderBucketLabel(bestRoot));
    }

    private static bool IsPathWithinRoot(string fullPath, string root)
    {
        try
        {
            string normalizedPath = Path.GetFullPath(fullPath);
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string FormatFolderBucketLabel(string folder)
    {
        string trimmed = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string label = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(label) ? folder : label;
    }

    private static string ResolvedFavoritesPath
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_FAVORITES_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath))
                return Path.GetFullPath(overridePath);

            var root = FindProjectRoot(Environment.CurrentDirectory)
                ?? FindProjectRoot(AppContext.BaseDirectory)
                ?? Environment.CurrentDirectory;
            return Path.Combine(root, ".cache", "favorites.json");
        }
    }

    private static string? FindProjectRoot(string start)
    {
        try
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "project.toml")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "local-native")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch
        {
        }

        return null;
    }

    private void LoadFavorites()
    {
        _favorites.Clear();
        _favoritesWriteBlocked = false;

        string path = ResolvedFavoritesPath;
        if (!File.Exists(path))
            return;

        if (!TryLoadFavoritesFile(path, _favorites))
        {
            _favorites.Clear();
            _favoritesWriteBlocked = true;
            ReportPersistenceRefusal("Favorites", path, protectedFile: true);
        }
    }

    private static bool TryLoadFavoritesFile(string path, Dictionary<string, int> destination)
    {
        if (!File.Exists(path))
            return true;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name) || !TryReadFavoriteValue(property.Value, out int level))
                    return false;
                if (level > 0)
                    destination[NormalizeFavoritePath(property.Name)] = level;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadFavoriteValue(JsonElement value, out int level)
    {
        level = 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double numeric) && double.IsFinite(numeric))
            level = (int)Math.Clamp(Math.Truncate(numeric), 0, 5);
        else if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            level = Math.Clamp(parsed, 0, 5);
        else if (value.ValueKind == JsonValueKind.True)
            level = 1;
        else if (value.ValueKind is JsonValueKind.False or JsonValueKind.Null)
            level = 0;
        else
            return false;

        return true;
    }

    private static string NormalizeFavoritePath(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : path;
        }
        catch
        {
            return path;
        }
    }

    private static string? NormalizePinnedPreviewPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> NormalizePreviewTabPaths(IEnumerable<string?> paths, int maxCount = int.MaxValue)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? candidate in paths)
        {
            string? path = NormalizePinnedPreviewPath(candidate);
            if (path is null
                || !SupportedImageExtensions.Contains(Path.GetExtension(path))
                || !seen.Add(path))
            {
                continue;
            }

            normalized.Add(path);
            if (normalized.Count >= maxCount)
                break;
        }

        return normalized;
    }

    private int FavoriteLevelForPath(string path)
        => _favorites.TryGetValue(NormalizeFavoritePath(path), out int level) ? Math.Clamp(level, 0, 5) : 0;

    private static string ResolvedEnhancementJobsPath
    {
        get
        {
            string? overridePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_ENHANCEMENT_JOBS_PATH");
            return string.IsNullOrWhiteSpace(overridePath) ? ProjectCachePath(Path.Combine("enhance", "jobs.json")) : Path.GetFullPath(overridePath);
        }
    }

    private void LoadEnhancedState()
    {
        _enhancedOutputs.Clear();
        _enhancementJobsRead = 0;
        _enhancedCandidateCount = 0;
        _enhancementReadOk = true;
        _enhancementReadError = null;

        string path = ResolvedEnhancementJobsPath;
        if (!File.Exists(path))
            return;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("jobs", out var jobsElement) ||
                jobsElement.ValueKind != JsonValueKind.Array)
            {
                _enhancementReadOk = false;
                _enhancementReadError = "jobs array missing";
                return;
            }

            foreach (var job in jobsElement.EnumerateArray())
            {
                if (job.ValueKind != JsonValueKind.Object)
                    continue;
                _enhancementJobsRead++;
                if (!TryGetStringProperty(job, "status", out string? status) ||
                    !string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!TryGetStringProperty(job, "outputPath", out string? outputPath) ||
                    string.IsNullOrWhiteSpace(outputPath))
                    continue;

                string resolvedOutput = NormalizeFavoritePath(outputPath);
                if (!File.Exists(resolvedOutput))
                    continue;

                bool mapped = false;
                foreach (string propertyName in new[] { "sourcePath", "sourceId" })
                {
                    if (!TryGetStringProperty(job, propertyName, out string? sourcePath) ||
                        string.IsNullOrWhiteSpace(sourcePath))
                        continue;

                    string resolvedSource = NormalizeFavoritePath(sourcePath);
                    if (!File.Exists(resolvedSource))
                        continue;

                    if (!_enhancedOutputs.ContainsKey(resolvedSource))
                        _enhancedOutputs[resolvedSource] = resolvedOutput;
                    mapped = true;
                }

                if (mapped)
                    _enhancedCandidateCount++;
            }
        }
        catch (Exception ex)
        {
            _enhancedOutputs.Clear();
            _enhancementReadOk = false;
            _enhancementReadError = ex.Message;
        }
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private bool TryGetEnhancedOutputForPath(string path, out string? outputPath)
    {
        return _enhancedOutputs.TryGetValue(NormalizeFavoritePath(path), out outputPath);
    }

    private static bool TryWriteAtomicText(string path, string text)
    {
        string? tempPath = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            tempPath = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, text);
            File.Move(tempPath, path, overwrite: true);
            tempPath = null;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (tempPath is not null)
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private static bool TryWithPersistenceLock(string targetPath, Func<bool> operation)
    {
        // Interactive handlers take one create-new attempt and yield on contention;
        // background/smoke work follows the shared 2 s / 25 ms browser retry contract.
        bool onUiThread = Application.Current?.Dispatcher?.CheckAccess() == true;
        using var lease = TryAcquirePersistenceLock(targetPath, onUiThread ? 0 : PersistenceLockTimeoutMilliseconds);
        return lease is not null && operation();
    }

    private static PersistenceLockLease? TryAcquirePersistenceLock(string targetPath, int timeoutMilliseconds)
    {
        string lockPath = targetPath + ".lock";
        var wait = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
                try
                {
                    var payload = JsonSerializer.Serialize(new { pid = Environment.ProcessId, createdAtUtc = DateTimeOffset.UtcNow.ToString("O") });
                    byte[] bytes = Encoding.UTF8.GetBytes(payload);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);
                    return new PersistenceLockLease(lockPath, stream);
                }
                catch
                {
                    stream.Dispose();
                    try { File.Delete(lockPath); } catch { }
                    return null;
                }
            }
            catch (IOException)
            {
                TryRemoveStalePersistenceLock(lockPath);
                if (wait.ElapsedMilliseconds >= timeoutMilliseconds)
                    return null;
                Thread.Sleep(PersistenceLockRetryMilliseconds);
            }
            catch
            {
                return null;
            }
        }

    }

    private static void TryRemoveStalePersistenceLock(string lockPath)
    {
        try
        {
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(lockPath) <= PersistenceLockStaleAfter)
                return;

            File.Delete(lockPath);
        }
        catch
        {
            // A fresh or unreadable lock is authoritative until the shared stale limit.
        }
    }

    private sealed class PersistenceLockLease(string path, FileStream stream) : IDisposable
    {
        public void Dispose()
        {
            stream.Dispose();
            try { File.Delete(path); } catch { }
        }
    }

    private void ReportPersistenceRefusal(string subject, string path, bool protectedFile = false, Action? retryAction = null)
    {
        string message = protectedFile
            ? $"{subject} could not be saved because its local file is invalid or newer. Fix it, then reload the folder."
            : File.Exists(path + ".lock")
                ? $"{subject} is busy in another PhotoViewer window. Retry after that update finishes."
                : $"{subject} could not be saved. Check local file access, then retry the action.";
        SetStatusToast(message, retryAction);
    }

    private bool SaveFavorites()
    {
        _favoriteSaveAttemptCount++;
        if (_favoritesWriteBlocked)
        {
            ReportPersistenceRefusal("Favorites", ResolvedFavoritesPath, protectedFile: true);
            return false;
        }

        string path = ResolvedFavoritesPath;
        Dictionary<string, int>? mergedResult = null;
        bool malformed = false;
        bool saved = TryWithPersistenceLock(path, () =>
        {
            var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!TryLoadFavoritesFile(path, merged))
            {
                malformed = true;
                return false;
            }
            IEnumerable<string> dirtyKeys = _favoriteDirtyPaths.Count > 0 ? _favoriteDirtyPaths : _favorites.Keys.ToList();
            foreach (string key in dirtyKeys)
            {
                if (_favorites.TryGetValue(key, out int level) && level > 0)
                    merged[key] = Math.Clamp(level, 1, 5);
                else
                    merged.Remove(key);
            }
            var ordered = merged.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase);
            var json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
            if (!TryWriteAtomicText(path, json))
                return false;
            mergedResult = merged;
            return true;
        });

        if (!saved || mergedResult is null)
        {
            if (malformed)
                _favoritesWriteBlocked = true;
            ReportPersistenceRefusal("Favorites", path, malformed);
            return false;
        }

        _favorites.Clear();
        foreach (var item in mergedResult) _favorites[item.Key] = item.Value;
        _favoriteDirtyPaths.Clear();
        return true;
    }

    private static string ResolvedSeenPath
    {
        get
        {
            var overridePath = SeenPathOverride;
            if (!string.IsNullOrWhiteSpace(overridePath))
                return Path.GetFullPath(overridePath);

            return ProjectCachePath("seen.json");
        }
    }

    private static string LegacySeenPath => ProjectCachePath("wpf-seen.json");

    private static string? SeenPathOverride
        => Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_SEEN_PATH");

    private static string ProjectCachePath(string fileName)
    {
        var root = FindProjectRoot(Environment.CurrentDirectory)
            ?? FindProjectRoot(AppContext.BaseDirectory)
            ?? Environment.CurrentDirectory;
        return Path.Combine(root, ".cache", fileName);
    }

    private void LoadSeenState()
    {
        _seenPaths.Clear();
        _seenDirtyPaths.Clear();
        _seenWriteBlocked = false;

        if (!string.IsNullOrWhiteSpace(SeenPathOverride))
        {
            string overridePath = ResolvedSeenPath;
            _seenWriteBlocked = !TryLoadSeenFile(overridePath, _seenPaths);
            if (_seenWriteBlocked)
                ReportPersistenceRefusal("Seen state", overridePath, protectedFile: true);
            return;
        }

        bool sharedOk = TryLoadSeenFile(ResolvedSeenPath, _seenPaths);
        bool legacyOk = TryLoadSeenFile(LegacySeenPath, _seenPaths);
        _seenWriteBlocked = !sharedOk || !legacyOk;
        if (_seenWriteBlocked)
            ReportPersistenceRefusal("Seen state", !sharedOk ? ResolvedSeenPath : LegacySeenPath, protectedFile: true);
    }

    private static bool TryLoadSeenFile(string path, HashSet<string> destination)
    {
        if (!File.Exists(path))
            return true;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                    return false;

                if (!TryReadSeenValue(property.Value, out bool seen))
                    return false;
                if (seen)
                    destination.Add(NormalizeFavoritePath(property.Name));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadSeenValue(JsonElement value, out bool seen)
    {
        seen = false;
        if (value.ValueKind == JsonValueKind.True) { seen = true; return true; }
        if (value.ValueKind == JsonValueKind.False) return true;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric)) { seen = numeric != 0; return true; }
        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed)) { seen = parsed; return true; }
        return false;
    }

    private bool SeenStateContains(string path)
        => _seenPaths.Contains(NormalizeFavoritePath(path));

    private bool SaveSeenState()
    {
        if (_seenWriteBlocked)
        {
            ReportPersistenceRefusal("Seen state", ResolvedSeenPath, protectedFile: true);
            return false;
        }

        string path = ResolvedSeenPath;
        HashSet<string>? mergedResult = null;
        bool malformed = false;
        bool saved = TryWithPersistenceLock(path, () =>
        {
            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!TryLoadSeenFile(path, merged))
            {
                malformed = true;
                return false;
            }
            IEnumerable<string> dirtyKeys = _seenDirtyPaths.Count > 0 ? _seenDirtyPaths : _seenPaths;
            foreach (string key in dirtyKeys)
            {
                if (_seenPaths.Contains(key))
                    merged.Add(key);
                else
                    merged.Remove(key);
            }
            var ordered = merged
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static item => item, static _ => true, StringComparer.OrdinalIgnoreCase);
            var json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
            if (!TryWriteAtomicText(path, json))
                return false;
            mergedResult = merged;
            return true;
        });
        if (!saved || mergedResult is null)
        {
            if (malformed)
                _seenWriteBlocked = true;
            ReportPersistenceRefusal("Seen state", path, malformed);
            return false;
        }
        _seenPaths.Clear();
        _seenPaths.UnionWith(mergedResult);
        _seenDirtyPaths.Clear();
        return true;
    }

    private bool MarkTileSeen(Tile tile)
    {
        if (!tile.IsRealFile)
            return false;

        string key = NormalizeFavoritePath(tile.Path);
        bool wasUnseen = tile.Unseen;
        bool hadSeen = _seenPaths.Contains(key);
        bool wasDirty = _seenDirtyPaths.Contains(key);

        if (hadSeen && !wasUnseen)
            return true;

        _seenPaths.Add(key);
        _seenDirtyPaths.Add(key);
        tile.Unseen = false;
        tile.ShowUnseenDot = false;

        if (!SaveSeenState())
        {
            if (!hadSeen)
                _seenPaths.Remove(key);
            if (!wasDirty)
                _seenDirtyPaths.Remove(key);
            tile.Unseen = wasUnseen;
            return false;
        }

        if (UnseenOnlyFilter?.IsChecked == true)
        {
            ApplyFilters(selectFirst: false);
            if (!_tiles.Contains(tile))
                SelectTile(null);
        }
        else
        {
            UpdateHeaderStats();
        }

        return true;
    }

    private void RefreshUnseenDots()
    {
        foreach (var tile in _allTiles)
            tile.ShowUnseenDot = _showUnseenDots && tile.Unseen;
    }

    public FavoriteImportSummary ImportPvuFavoriteLevelsForSmoke(string browserStatePath)
    {
        if (string.IsNullOrWhiteSpace(browserStatePath) || !File.Exists(browserStatePath))
            return FavoriteImportSummary.Failed(browserStatePath, "missing browser state file");

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(browserStatePath));
            if (!TryFindPvuFavoriteLevels(document.RootElement, out var levelsElement, out string sourceShape))
                return FavoriteImportSummary.Failed(browserStatePath, "pvu_fav_levels not found");

            if (levelsElement.ValueKind == JsonValueKind.String)
            {
                string? embeddedJson = levelsElement.GetString();
                if (string.IsNullOrWhiteSpace(embeddedJson))
                    return FavoriteImportSummary.Failed(browserStatePath, "pvu_fav_levels string was empty", sourceShape);

                using var embeddedDocument = JsonDocument.Parse(embeddedJson);
                return ImportPvuFavoriteLevelsObject(embeddedDocument.RootElement, browserStatePath, sourceShape);
            }

            return ImportPvuFavoriteLevelsObject(levelsElement, browserStatePath, sourceShape);
        }
        catch (Exception ex)
        {
            return FavoriteImportSummary.Failed(browserStatePath, ex.Message);
        }
    }

    public FavoriteImportSummary ImportPvuFavoritesForSmoke(string browserStatePath)
    {
        if (string.IsNullOrWhiteSpace(browserStatePath) || !File.Exists(browserStatePath))
            return FavoriteImportSummary.Failed(browserStatePath, "missing browser state file");

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(browserStatePath));
            if (!TryFindBrowserStateProperty(document.RootElement, "pvu_favorites", out var favoritesElement, out string sourceShape))
                return FavoriteImportSummary.Failed(browserStatePath, "pvu_favorites not found");

            if (favoritesElement.ValueKind == JsonValueKind.String)
            {
                string? embeddedJson = favoritesElement.GetString();
                if (string.IsNullOrWhiteSpace(embeddedJson))
                    return FavoriteImportSummary.Failed(browserStatePath, "pvu_favorites string was empty", sourceShape);

                using var embeddedDocument = JsonDocument.Parse(embeddedJson);
                return ImportPvuFavoritesElement(embeddedDocument.RootElement, browserStatePath, sourceShape);
            }

            return ImportPvuFavoritesElement(favoritesElement, browserStatePath, sourceShape);
        }
        catch (Exception ex)
        {
            return FavoriteImportSummary.Failed(browserStatePath, ex.Message);
        }
    }

    public SeenImportSummary ImportPvuSeenImagesForSmoke(string browserStatePath)
    {
        if (string.IsNullOrWhiteSpace(browserStatePath) || !File.Exists(browserStatePath))
            return SeenImportSummary.Failed(browserStatePath, "missing browser state file");

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(browserStatePath));
            if (!TryFindBrowserStateProperty(document.RootElement, "pvu_seen_images", out var seenElement, out string sourceShape))
                return SeenImportSummary.Failed(browserStatePath, "pvu_seen_images not found");

            if (seenElement.ValueKind == JsonValueKind.String)
            {
                string? embeddedJson = seenElement.GetString();
                if (string.IsNullOrWhiteSpace(embeddedJson))
                    return SeenImportSummary.Failed(browserStatePath, "pvu_seen_images string was empty", sourceShape);

                using var embeddedDocument = JsonDocument.Parse(embeddedJson);
                return ImportPvuSeenImagesElement(embeddedDocument.RootElement, browserStatePath, sourceShape);
            }

            return ImportPvuSeenImagesElement(seenElement, browserStatePath, sourceShape);
        }
        catch (Exception ex)
        {
            return SeenImportSummary.Failed(browserStatePath, ex.Message);
        }
    }

    private FavoriteImportSummary ImportPvuFavoriteLevelsObject(JsonElement levelsElement, string browserStatePath, string sourceShape)
    {
        if (levelsElement.ValueKind != JsonValueKind.Object)
            return FavoriteImportSummary.Failed(browserStatePath, "pvu_fav_levels is not an object map", sourceShape);

        int total = 0;
        int imported = 0;
        int preserved = 0;
        int ignoredZero = 0;
        int ignoredInvalid = 0;
        int missing = 0;
        int unmatched = 0;
        var favoriteSnapshot = new Dictionary<string, int>(_favorites, StringComparer.OrdinalIgnoreCase);
        var tileSnapshot = _allTiles.ToDictionary(static tile => tile, static tile => tile.Fav);

        foreach (var property in levelsElement.EnumerateObject())
        {
            total++;
            string rawKey = property.Name.Trim();
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                missing++;
                continue;
            }

            if (!TryReadPvuFavoriteImportLevel(property.Value, out int level, out bool wasZero))
            {
                if (wasZero)
                    ignoredZero++;
                else
                    ignoredInvalid++;
                continue;
            }

            var tile = ResolvePvuFavoriteImportTile(rawKey);
            if (tile is null)
            {
                unmatched++;
                continue;
            }

            string key = NormalizeFavoritePath(tile.Path);
            int existingLevel = FavoriteLevelForPath(tile.Path);
            if (existingLevel > 0)
            {
                preserved++;
                continue;
            }

            _favorites[key] = level;
            tile.Fav = level;
            imported++;
        }

        if (imported > 0 && !SaveFavorites())
        {
            _favorites.Clear();
            foreach (var item in favoriteSnapshot)
                _favorites[item.Key] = item.Value;
            foreach (var (tile, fav) in tileSnapshot)
                tile.Fav = fav;
            return FavoriteImportSummary.Failed(browserStatePath, "favorites save failed", sourceShape);
        }

        if (imported > 0)
        {
            ApplyFilters();
            if (SelectedTile() is { } selected)
                UpdatePreview(selected);
            else
                UpdateHeaderStats();
        }

        return new FavoriteImportSummary(
            true,
            "pvu_fav_levels import policy applied",
            browserStatePath,
            sourceShape,
            total,
            imported,
            preserved,
            ignoredZero,
            ignoredInvalid,
            missing,
            unmatched,
            FavoriteStoreCountForSmoke);
    }

    private FavoriteImportSummary ImportPvuFavoritesElement(JsonElement favoritesElement, string browserStatePath, string sourceShape)
    {
        if (favoritesElement.ValueKind != JsonValueKind.Object && favoritesElement.ValueKind != JsonValueKind.Array)
            return FavoriteImportSummary.Failed(browserStatePath, "pvu_favorites is not an object map or list", sourceShape);

        int total = 0;
        int imported = 0;
        int preserved = 0;
        int ignoredZero = 0;
        int ignoredInvalid = 0;
        int missing = 0;
        int unmatched = 0;
        var favoriteSnapshot = new Dictionary<string, int>(_favorites, StringComparer.OrdinalIgnoreCase);
        var tileSnapshot = _allTiles.ToDictionary(static tile => tile, static tile => tile.Fav);

        void ImportCandidate(string rawKey, int level)
        {
            var tile = ResolvePvuFavoriteImportTile(rawKey);
            if (tile is null)
            {
                unmatched++;
                return;
            }

            string key = NormalizeFavoritePath(tile.Path);
            int existingLevel = FavoriteLevelForPath(tile.Path);
            if (existingLevel > 0)
            {
                preserved++;
                return;
            }

            _favorites[key] = level;
            tile.Fav = level;
            imported++;
        }

        if (favoritesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in favoritesElement.EnumerateObject())
            {
                total++;
                string rawKey = property.Name.Trim();
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    missing++;
                    continue;
                }

                if (!TryReadPvuFavoritesImportLevel(property.Value, out int level, out bool wasZero))
                {
                    if (wasZero)
                        ignoredZero++;
                    else
                        ignoredInvalid++;
                    continue;
                }

                ImportCandidate(rawKey, level);
            }
        }
        else
        {
            foreach (var item in favoritesElement.EnumerateArray())
            {
                total++;
                if (item.ValueKind != JsonValueKind.String)
                {
                    ignoredInvalid++;
                    continue;
                }

                string rawKey = item.GetString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    missing++;
                    continue;
                }

                ImportCandidate(rawKey, 5);
            }
        }

        if (imported > 0 && !SaveFavorites())
        {
            _favorites.Clear();
            foreach (var item in favoriteSnapshot)
                _favorites[item.Key] = item.Value;
            foreach (var (tile, fav) in tileSnapshot)
                tile.Fav = fav;
            return FavoriteImportSummary.Failed(browserStatePath, "favorites save failed", sourceShape);
        }

        if (imported > 0)
        {
            ApplyFilters();
            if (SelectedTile() is { } selected)
                UpdatePreview(selected);
            else
                UpdateHeaderStats();
        }

        return new FavoriteImportSummary(
            true,
            "pvu_favorites import policy applied",
            browserStatePath,
            sourceShape,
            total,
            imported,
            preserved,
            ignoredZero,
            ignoredInvalid,
            missing,
            unmatched,
            FavoriteStoreCountForSmoke);
    }

    private SeenImportSummary ImportPvuSeenImagesElement(JsonElement seenElement, string browserStatePath, string sourceShape)
    {
        if (seenElement.ValueKind != JsonValueKind.Object && seenElement.ValueKind != JsonValueKind.Array)
            return SeenImportSummary.Failed(browserStatePath, "pvu_seen_images is not an object map or list", sourceShape);

        int total = 0;
        int imported = 0;
        int preserved = 0;
        int ignoredZero = 0;
        int ignoredInvalid = 0;
        int missing = 0;
        int unmatched = 0;
        var seenSnapshot = _seenPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tileSnapshot = _allTiles.ToDictionary(static tile => tile, static tile => tile.Unseen);

        void ImportCandidate(string rawKey)
        {
            var tile = ResolvePvuSeenImportTile(rawKey);
            if (tile is null)
            {
                unmatched++;
                return;
            }

            string key = NormalizeFavoritePath(tile.Path);
            if (_seenPaths.Contains(key) || !tile.Unseen)
            {
                preserved++;
                return;
            }

            _seenPaths.Add(key);
            tile.Unseen = false;
            imported++;
        }

        if (seenElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in seenElement.EnumerateObject())
            {
                total++;
                string rawKey = property.Name.Trim();
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    missing++;
                    continue;
                }

                if (!TryReadPvuSeenImportFlag(property.Value, out bool wasZero))
                {
                    if (wasZero)
                        ignoredZero++;
                    else
                        ignoredInvalid++;
                    continue;
                }

                ImportCandidate(rawKey);
            }
        }
        else
        {
            foreach (var item in seenElement.EnumerateArray())
            {
                total++;
                if (item.ValueKind != JsonValueKind.String)
                {
                    ignoredInvalid++;
                    continue;
                }

                string rawKey = item.GetString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    missing++;
                    continue;
                }

                ImportCandidate(rawKey);
            }
        }

        if (imported > 0 && !SaveSeenState())
        {
            _seenPaths.Clear();
            foreach (var item in seenSnapshot)
                _seenPaths.Add(item);
            foreach (var (tile, unseen) in tileSnapshot)
                tile.Unseen = unseen;
            return SeenImportSummary.Failed(browserStatePath, "seen state save failed", sourceShape);
        }

        if (imported > 0)
        {
            if (UnseenOnlyFilter?.IsChecked == true)
                ApplyFilters(selectFirst: false);
            else
                UpdateHeaderStats();
        }

        return new SeenImportSummary(
            true,
            "pvu_seen_images import policy applied",
            browserStatePath,
            sourceShape,
            total,
            imported,
            preserved,
            ignoredZero,
            ignoredInvalid,
            missing,
            unmatched,
            SeenStoreCountForSmoke);
    }

    private static bool TryFindPvuFavoriteLevels(JsonElement root, out JsonElement levelsElement, out string sourceShape)
        => TryFindBrowserStateProperty(root, "pvu_fav_levels", out levelsElement, out sourceShape);

    private static bool TryFindBrowserStateProperty(JsonElement root, string propertyName, out JsonElement value, out string sourceShape)
    {
        value = default;
        sourceShape = "";
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        if (root.TryGetProperty(propertyName, out value))
        {
            sourceShape = propertyName;
            return true;
        }

        string[] containers =
        [
            "browserLocalStorage",
            "browser_local_storage",
            "localStorage",
            "local_storage",
            "browserState",
            "browser_state",
        ];

        foreach (string container in containers)
        {
            if (!root.TryGetProperty(container, out var nested) || nested.ValueKind != JsonValueKind.Object)
                continue;

            if (nested.TryGetProperty(propertyName, out value))
            {
                sourceShape = $"{container}.{propertyName}";
                return true;
            }
        }

        return false;
    }

    private static bool TryReadPvuFavoriteImportLevel(JsonElement value, out int level, out bool wasZero)
    {
        level = 0;
        wasZero = false;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric))
            level = numeric;
        else if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsed))
            level = parsed;
        else
            return false;

        if (level <= 0)
        {
            wasZero = true;
            return false;
        }

        level = Math.Clamp(level, 1, 5);
        return true;
    }

    private static bool TryReadPvuFavoritesImportLevel(JsonElement value, out int level, out bool wasZero)
    {
        level = 0;
        wasZero = false;

        if (value.ValueKind == JsonValueKind.True)
        {
            level = 5;
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            wasZero = true;
            return false;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric))
            level = numeric;
        else if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsed))
            level = parsed;
        else
            return false;

        if (level <= 0)
        {
            wasZero = true;
            return false;
        }

        level = Math.Clamp(level, 1, 5);
        return true;
    }

    private static bool TryReadPvuSeenImportFlag(JsonElement value, out bool wasZero)
    {
        wasZero = false;

        if (value.ValueKind == JsonValueKind.True)
            return true;

        if (value.ValueKind == JsonValueKind.False)
        {
            wasZero = true;
            return false;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric))
        {
            if (numeric != 0)
                return true;

            wasZero = true;
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString()?.Trim() ?? "";
            if (bool.TryParse(text, out bool parsedBool))
            {
                if (parsedBool)
                    return true;

                wasZero = true;
                return false;
            }

            if (int.TryParse(text, out int parsedInt))
            {
                if (parsedInt != 0)
                    return true;

                wasZero = true;
                return false;
            }
        }

        return false;
    }

    private Tile? ResolvePvuFavoriteImportTile(string rawKey)
    {
        string normalizedKey = NormalizeFavoritePath(rawKey);
        var byPath = _allTiles.FirstOrDefault(tile =>
            tile.IsRealFile &&
            string.Equals(NormalizeFavoritePath(tile.Path), normalizedKey, StringComparison.OrdinalIgnoreCase));
        if (byPath is not null)
            return byPath;

        if (!string.IsNullOrWhiteSpace(_currentFolder))
        {
            try
            {
                string relativePath = NormalizeFavoritePath(Path.Combine(_currentFolder, rawKey));
                byPath = _allTiles.FirstOrDefault(tile =>
                    tile.IsRealFile &&
                    string.Equals(NormalizeFavoritePath(tile.Path), relativePath, StringComparison.OrdinalIgnoreCase));
                if (byPath is not null)
                    return byPath;
            }
            catch
            {
            }
        }

        string fileName = Path.GetFileName(rawKey);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return _allTiles.FirstOrDefault(tile =>
            tile.IsRealFile &&
            string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase));
    }

    private Tile? ResolvePvuSeenImportTile(string rawKey)
        => ResolvePvuFavoriteImportTile(rawKey);

    private static BitmapSource? LoadBitmap(string path, int decodePixelWidth)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodePixelWidth > 0)
                image.DecodePixelWidth = decodePixelWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadBitmapSize(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            width = frame.PixelWidth;
            height = frame.PixelHeight;
            return width > 0 && height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static ImageMetadataLoadMetrics ReadImageMetadata(IReadOnlyList<FileInfo> files, CancellationToken token)
    {
        if (files.Count == 0)
            return ImageMetadataLoadMetrics.Empty;

        var watch = Stopwatch.StartNew();
        var dimensions = new ConcurrentDictionary<string, ImageDimensions>(StringComparer.OrdinalIgnoreCase);
        var prompts = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int workers = Math.Max(1, Math.Min(Math.Min(MaxMetadataReadWorkers, Environment.ProcessorCount), files.Count));
        int completed = 0;
        int decodeFailures = 0;
        Parallel.ForEach(
            files,
            new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = workers },
            file =>
            {
                token.ThrowIfCancellationRequested();
                if (!TryReadBitmapSize(file.FullName, out int width, out int height))
                    Interlocked.Increment(ref decodeFailures);
                dimensions[file.FullName] = new ImageDimensions(width, height);
                PngParametersMetadata? metadata = ReadPngParametersMetadata(file.FullName, token);
                if (!string.IsNullOrWhiteSpace(metadata?.Prompt))
                    prompts[file.FullName] = metadata.Prompt;
                Interlocked.Increment(ref completed);
            });
        watch.Stop();
        return new ImageMetadataLoadMetrics(dimensions, prompts, workers, completed, watch.ElapsedMilliseconds, decodeFailures);
    }

    // ─────────── Sample data (shell only — no real files, procedural art) ───────────
    private static readonly string[][] Palettes =
    {
        new[] { "#F0ABFC", "#818CF8", "#0B1026" }, new[] { "#FCA5A5", "#7C3AED", "#0A0A14" },
        new[] { "#FCD34D", "#FB7185", "#160B1A" }, new[] { "#67E8F9", "#3B82F6", "#020617" },
        new[] { "#A7F3D0", "#10B981", "#04120C" }, new[] { "#FDBA74", "#EF4444", "#1A0808" },
        new[] { "#C4B5FD", "#6366F1", "#0A0A1A" }, new[] { "#F9A8D4", "#DB2777", "#1A0714" },
        new[] { "#93C5FD", "#1E40AF", "#020814" }, new[] { "#FEF08A", "#A16207", "#141002" },
    };

    private static readonly string[] Names =
    {
        "portrait", "elf", "castle", "mecha", "sunset", "samurai", "forest", "android",
        "harbor", "witch", "dragon", "street", "angel", "ruins", "neon", "garden",
        "knight", "ocean", "temple", "cyber", "moon", "fox",
    };

    private static readonly string[] Prompts =
    {
        "masterpiece, best quality, 1girl, portrait, silver hair, soft rim lighting, cinematic",
        "highly detailed, elf, forest, dappled light, intricate armor, depth of field",
        "epic castle, dramatic sky, volumetric fog, wide shot, matte painting",
        "mecha, neon city, rain, reflective surfaces, cyberpunk, 8k",
        "golden hour, sunset over hills, warm tones, atmospheric, film grain",
        "samurai, cherry blossoms, ink wash style, dynamic pose, motion",
    };

    private void BuildSampleTiles()
    {
        double w = SizeSlider?.Value ?? 190;

        // (fav, unseen) per card
        (int fav, bool unseen)[] today =
        {
            (3, true), (0, true), (1, false), (0, false), (0, true), (5, false),
            (0, false), (0, false), (2, false), (0, false), (0, true), (0, false),
        };
        (int fav, bool unseen)[] yesterday =
        {
            (2, false), (0, false), (0, false), (1, false), (0, false),
            (0, true), (0, false), (4, false), (0, false), (0, false),
        };

        for (int i = 0; i < today.Length; i++)
            _tiles.Add(MakeTile(i, "Today  ·  2026-07-08", today[i].fav, today[i].unseen, 427, w));
        for (int i = 0; i < yesterday.Length; i++)
            _tiles.Add(MakeTile(i, "Yesterday  ·  2026-07-07", yesterday[i].fav, yesterday[i].unseen, 391, w, offset: 4));
    }

    private Tile MakeTile(int i, string group, int fav, bool unseen, int baseNum, double width, int offset = 0)
    {
        int idx = i + offset;
        string name = $"{(baseNum - i):00000}-{Names[idx % Names.Length]}.png";
        var tile = new Tile
        {
            ArtBase = MakeBaseBrush(idx),
            ArtGlow = MakeGlowBrush(idx),
            FileName = name,
            Fav = fav,
            Unseen = unseen,
            ShowUnseenDot = _showUnseenDots && unseen,
            Group = group,
            CardWidth = width,
            Prompt = Prompts[idx % Prompts.Length],
            Path = $@"D:\SD\outputs\txt2img\{name}",
            ImagePixelWidth = 832,
            ImagePixelHeight = 1216,
            SizeText = "832 x 1216",
            ModifiedText = "2026-07-08 14:22",
        };
        ApplyCardLayout(tile);
        return tile;
    }

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static Brush MakeBaseBrush(int i)
    {
        var p = Palettes[i % Palettes.Length];
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        b.GradientStops.Add(new GradientStop(Hex(p[2]), 0));
        b.GradientStops.Add(new GradientStop(Hex(p[1]), 0.55));
        b.GradientStops.Add(new GradientStop(Hex(p[2]), 1));
        b.Freeze();
        return b;
    }

    private static Brush MakeGlowBrush(int i)
    {
        var p = Palettes[i % Palettes.Length];
        var c = Hex(p[0]);
        double gx = 0.15 + (i * 0.17) % 0.7;
        double gy = 0.2 + (i * 0.11) % 0.6;
        var b = new RadialGradientBrush
        {
            GradientOrigin = new Point(gx, gy),
            Center = new Point(gx, gy),
            RadiusX = 0.95,
            RadiusY = 0.95,
        };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0xCC, c.R, c.G, c.B), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, c.R, c.G, c.B), 0.62));
        b.Freeze();
        return b;
    }

    // ─────────── Selection → right preview ───────────
    private void CardsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || sender is not ListBox lb)
            return;

        foreach (Tile tile in e.RemovedItems.OfType<Tile>())
            _selectedPaths.Remove(tile.Path);
        foreach (Tile tile in e.AddedItems.OfType<Tile>())
            _selectedPaths.Add(tile.Path);

        Tile? primary = lb.SelectedItem as Tile;
        if (primary is null || !_selectedPaths.Contains(primary.Path))
            primary = e.AddedItems.OfType<Tile>().LastOrDefault()
                ?? SelectedTiles().LastOrDefault();
        _primarySelectedPath = primary?.Path;

        SynchronizeSelectionControls();
        ApplyPrimarySelection(primary);
    }

    private IReadOnlyList<Tile> SelectedTiles()
        => _tiles.Where(tile => _selectedPaths.Contains(tile.Path)).ToList();

    private void SetSelection(IEnumerable<Tile> selectedTiles, Tile? primary)
    {
        _selectedPaths.Clear();
        foreach (Tile tile in selectedTiles.Where(tile => _tiles.Contains(tile)))
            _selectedPaths.Add(tile.Path);

        Tile? effectivePrimary = primary is not null && _selectedPaths.Contains(primary.Path)
            ? primary
            : SelectedTiles().LastOrDefault();
        _primarySelectedPath = effectivePrimary?.Path;

        SynchronizeSelectionControls();
        ApplyPrimarySelection(effectivePrimary);
    }

    private void SynchronizeSelectionControls()
    {
        if (_syncingSelection)
            return;

        try
        {
            _syncingSelection = true;
            SynchronizeSelectionControl(CardsList);
            SynchronizeSelectionControl(RowsList);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void SynchronizeSelectionControl(ListBox listBox)
    {
        listBox.SelectedItems.Clear();
        foreach (Tile tile in listBox.Items.OfType<Tile>())
        {
            if (_selectedPaths.Contains(tile.Path))
                listBox.SelectedItems.Add(tile);
        }
    }

    private void ApplyPrimarySelection(Tile? primary)
    {
        if (primary is null)
        {
            ClearPreview();
            return;
        }

        EnsureGridTileRealized(primary);
        CardsList.ScrollIntoView(primary);
        RowsList.ScrollIntoView(primary);
        UpdatePreview(primary);
        if (primary.IsRealFile)
        {
            MarkTileSeen(primary);
            SaveState();
        }
    }

    private void UpdatePreview(Tile t)
    {
        var watch = Stopwatch.StartNew();
        bool hasRealFile = t.IsRealFile;
        Visibility generatedMetadataVisibility = hasRealFile ? Visibility.Collapsed : Visibility.Visible;
        _previewCts?.Cancel();
        _previewDecodeCompletion?.TrySetResult(PreviewDecodeResult.Canceled);
        _previewMetadataCts?.Cancel();
        _previewMetadataCompletion?.TrySetResult(null);
        ClearPreviewMetadataCopyState();
        _previewDecodedPath = null;

        var cts = new CancellationTokenSource();
        _previewCts = cts;
        var completion = new TaskCompletionSource<PreviewDecodeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _previewDecodeCompletion = completion;

        var immediate = hasRealFile ? t.Thumbnail : null;
        PreviewBitmap.Source = immediate;
        PreviewBitmap.Visibility = immediate is null ? Visibility.Collapsed : Visibility.Visible;
        PreviewArtBase.Visibility = immediate is null ? Visibility.Visible : Visibility.Collapsed;
        PreviewArtGlow.Visibility = immediate is null ? Visibility.Visible : Visibility.Collapsed;
        PreviewArtBase.Fill = t.ArtBase;
        PreviewArtGlow.Fill = t.ArtGlow;
        PreviewFileName.Text = t.FileName;
        PreviewTabName.Text = t.FileName;
        PreviewSizeText.Text = hasRealFile
            ? t.ImagePixelWidth > 0 && t.ImagePixelHeight > 0
                ? $"{t.ImagePixelWidth} x {t.ImagePixelHeight}"
                : "Loading..."
            : t.SizeText;
        PreviewModelLabel.Text = hasRealFile ? "TYPE" : "MODEL";
        PreviewModelText.Text = hasRealFile
            ? Path.GetExtension(t.Path).TrimStart('.').ToUpperInvariant()
            : "animagineXL_v31";
        PreviewPromptLabel.Text = hasRealFile ? "PATH" : "PROMPT";
        PreviewSamplerLabel.Visibility = generatedMetadataVisibility;
        PreviewSamplerText.Visibility = generatedMetadataVisibility;
        PreviewStepsLabel.Visibility = generatedMetadataVisibility;
        PreviewStepsText.Visibility = generatedMetadataVisibility;
        PreviewCfgLabel.Visibility = generatedMetadataVisibility;
        PreviewCfgText.Visibility = generatedMetadataVisibility;
        PreviewSeedLabel.Visibility = generatedMetadataVisibility;
        PreviewSeedText.Visibility = generatedMetadataVisibility;
        PreviewNegativeLabel.Visibility = generatedMetadataVisibility;
        PreviewNegativeCard.Visibility = generatedMetadataVisibility;
        if (hasRealFile)
            PreviewNegativeText.Text = "";
        PreviewDateText.Text = t.ModifiedText;
        PreviewPromptText.Text = hasRealFile ? t.Path : (string.IsNullOrWhiteSpace(t.Prompt) ? t.Path : t.Prompt);
        FavoriteLevelText.Text = t.Fav.ToString();
        ModalFavoriteLevelText.Text = t.Fav.ToString();
        SyncSelectionActionSurface();
        UpdateHeaderStats();
        ModalTitle.Text = $"{t.FileName} - {PreviewSizeText.Text}";
        watch.Stop();
        _previewUpdateCount++;
        _previewMs += watch.ElapsedMilliseconds;
        _lastPreviewImmediateMs = watch.ElapsedMilliseconds;

        if (hasRealFile)
        {
            _ = LoadPreviewBitmapAsync(t.Path, cts.Token, completion);
            var metadataCts = new CancellationTokenSource();
            _previewMetadataCts = metadataCts;
            var metadataCompletion = new TaskCompletionSource<PngParametersMetadata?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _previewMetadataCompletion = metadataCompletion;
            _ = LoadPreviewPngMetadataAsync(t.Path, metadataCts.Token, metadataCompletion);
        }
        else
            completion.TrySetResult(new PreviewDecodeResult(t.Path, immediate, 0, 0, 0, Applied: true));
    }

    private async Task LoadPreviewBitmapAsync(string path, CancellationToken token, TaskCompletionSource<PreviewDecodeResult> completion)
    {
        PreviewDecodeResult decoded;
        try
        {
            decoded = await Task.Run(
                () =>
                {
                    var watch = Stopwatch.StartNew();
                    var bitmap = LoadBitmap(path, 900);
                    bool hasSize = TryReadBitmapSize(path, out int width, out int height);
                    watch.Stop();
                    return new PreviewDecodeResult(path, bitmap, hasSize ? width : 0, hasSize ? height : 0, watch.ElapsedMilliseconds, Applied: false);
                },
                token);
        }
        catch (OperationCanceledException)
        {
            completion.TrySetResult(PreviewDecodeResult.Canceled);
            return;
        }
        catch
        {
            completion.TrySetResult(new PreviewDecodeResult(path, null, 0, 0, 0, Applied: false));
            return;
        }

        if (token.IsCancellationRequested)
        {
            completion.TrySetResult(PreviewDecodeResult.Canceled);
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    if (token.IsCancellationRequested || !string.Equals(SelectedTile()?.Path, path, StringComparison.OrdinalIgnoreCase))
                    {
                        completion.TrySetResult(PreviewDecodeResult.Canceled);
                        return;
                    }

                    if (decoded.Bitmap is not null)
                    {
                        PreviewBitmap.Source = decoded.Bitmap;
                        PreviewBitmap.Visibility = Visibility.Visible;
                        PreviewArtBase.Visibility = Visibility.Collapsed;
                        PreviewArtGlow.Visibility = Visibility.Collapsed;
                    }

                    if (decoded.Width > 0 && decoded.Height > 0)
                        PreviewSizeText.Text = $"{decoded.Width} x {decoded.Height}";

                    ModalTitle.Text = $"{PreviewFileName.Text} - {PreviewSizeText.Text}";
                    _previewDecodedPath = path;
                    _previewDeferredDecodeCount++;
                    _previewDeferredDecodeMs += decoded.DecodeMs;
                    if (LastLoadMetrics is not null)
                    {
                        LastLoadMetrics.PreviewDeferredDecodeMs = _previewDeferredDecodeMs;
                        LastLoadMetrics.PreviewDeferredDecodeCount = _previewDeferredDecodeCount;
                    }

                    completion.TrySetResult(decoded with { Applied = true });
                },
                DispatcherPriority.Background,
                token);
        }
        catch (OperationCanceledException)
        {
            completion.TrySetResult(PreviewDecodeResult.Canceled);
        }
    }

    private async Task LoadPreviewPngMetadataAsync(
        string path,
        CancellationToken token,
        TaskCompletionSource<PngParametersMetadata?> completion)
    {
        PngParametersMetadata? metadata;
        try
        {
            metadata = await Task.Run(() => ReadPngParametersMetadata(path, token), token);
        }
        catch (OperationCanceledException)
        {
            completion.TrySetResult(null);
            return;
        }
        catch
        {
            completion.TrySetResult(null);
            return;
        }

        if (token.IsCancellationRequested)
        {
            completion.TrySetResult(null);
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    if (token.IsCancellationRequested || !string.Equals(SelectedTile()?.Path, path, StringComparison.OrdinalIgnoreCase))
                    {
                        completion.TrySetResult(null);
                        return;
                    }

                    if (metadata is not null)
                        ApplyPngParametersMetadata(metadata);
                    completion.TrySetResult(metadata);
                },
                DispatcherPriority.Background,
                token);
        }
        catch (OperationCanceledException)
        {
            completion.TrySetResult(null);
        }
    }

    private void ApplyPngParametersMetadata(PngParametersMetadata metadata)
    {
        Tile? selected = SelectedTile();
        if (selected is null)
            return;

        _currentPreviewMetadata = metadata;
        _currentPreviewMetadataPath = selected.Path;
        CopyPreviewMetadataButton.IsEnabled = true;
        CopyPreviewMetadataButton.Content = "Copy";
        CopyPreviewMetadataButton.ToolTip = "Copy PNG metadata";
        CopyPreviewPromptButton.IsEnabled = !string.IsNullOrWhiteSpace(metadata.Prompt);
        CopyPreviewPromptButton.ToolTip = CopyPreviewPromptButton.IsEnabled ? "Copy prompt" : "No prompt metadata loaded";
        CopyPreviewNegativeButton.IsEnabled = !string.IsNullOrWhiteSpace(metadata.NegativePrompt);
        CopyPreviewNegativeButton.ToolTip = CopyPreviewNegativeButton.IsEnabled ? "Copy negative prompt" : "No negative prompt metadata loaded";
        PreviewPromptLabel.Text = "PROMPT";
        PreviewPromptText.Text = string.IsNullOrWhiteSpace(metadata.Prompt) ? PreviewPromptText.Text : metadata.Prompt;
        SetPreviewMetadataRow(PreviewSamplerLabel, PreviewSamplerText, "SAMPLER", metadata.Setting("Sampler"));
        SetPreviewMetadataRow(PreviewStepsLabel, PreviewStepsText, "STEPS", metadata.Setting("Steps"));
        SetPreviewMetadataRow(PreviewCfgLabel, PreviewCfgText, "CFG", metadata.Setting("CFG scale"));
        SetPreviewMetadataRow(PreviewSeedLabel, PreviewSeedText, "SEED", metadata.Setting("Seed"));

        bool hasNegative = !string.IsNullOrWhiteSpace(metadata.NegativePrompt);
        PreviewNegativeLabel.Visibility = hasNegative ? Visibility.Visible : Visibility.Collapsed;
        PreviewNegativeCard.Visibility = hasNegative ? Visibility.Visible : Visibility.Collapsed;
        PreviewNegativeText.Text = hasNegative ? metadata.NegativePrompt : "";
        SyncModalMetadataSidebar();
    }

    private void SyncModalMetadataSidebar()
    {
        Tile? selected = SelectedTile();
        bool current = selected is not null
            && _currentPreviewMetadata is not null
            && string.Equals(selected.Path, _currentPreviewMetadataPath, StringComparison.OrdinalIgnoreCase);
        PngParametersMetadata? metadata = current ? _currentPreviewMetadata : null;
        bool hasPrompt = !string.IsNullOrWhiteSpace(metadata?.Prompt);
        bool hasNegative = !string.IsNullOrWhiteSpace(metadata?.NegativePrompt);

        string settingsText = metadata is not null && metadata.Settings.Count > 0
            ? string.Join("  ·  ", metadata.Settings.Select(static pair => $"{pair.Key}: {pair.Value}"))
            : "No settings metadata.";
        ModalSettingsText.Text = settingsText;
        ModalMetadataStatusText.Text = metadata is null
            ? "No PNG metadata loaded"
            : metadata.Settings.Count > 0
                ? settingsText
                : "PNG parameters loaded";
        ModalPromptText.Text = hasPrompt ? metadata!.Prompt : "-";
        SyncModalPromptChips(hasPrompt ? metadata!.Prompt : "");
        ModalNegativeText.Text = hasNegative ? metadata!.NegativePrompt : "-";
        CopyModalMetadataButton.IsEnabled = metadata is not null;
        CopyModalMetadataButton.ToolTip = metadata is null ? "No PNG metadata loaded" : "Copy PNG metadata";
        CopyModalPromptButton.IsEnabled = hasPrompt;
        CopyModalPromptButton.ToolTip = hasPrompt ? "Copy prompt" : "No prompt metadata loaded";
        CopyModalNegativeButton.IsEnabled = hasNegative;
        CopyModalNegativeButton.ToolTip = hasNegative ? "Copy negative prompt" : "No negative prompt metadata loaded";
    }

    private static List<string> ParsePromptTags(string? prompt)
    {
        var tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string tag in (prompt ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (seen.Add(tag))
                tags.Add(tag);
        }
        return tags;
    }

    private void SyncModalPromptChips(string prompt)
    {
        if (ModalPromptChips is null)
            return;

        ModalPromptChips.Children.Clear();
        List<string> tags = ParsePromptTags(prompt);
        ModalPromptEmptyText.Visibility = tags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (string tag in tags)
        {
            var chip = new Button
            {
                Content = tag,
                Tag = tag,
                Style = (Style)FindResource("GhostButton"),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 6),
                FontSize = 11.5,
                ToolTip = $"Search for {tag}",
            };
            System.Windows.Automation.AutomationProperties.SetName(chip, $"Search prompt tag {tag}");
            System.Windows.Automation.AutomationProperties.SetHelpText(chip, "Append this tag to search, close the modal, and focus search.");
            chip.Click += ModalPromptTag_Click;
            chip.PreviewKeyDown += ModalPromptTag_PreviewKeyDown;
            ModalPromptChips.Children.Add(chip);
        }
    }

    private void ModalPromptTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
            ApplyModalPromptTagSearch(tag);
    }

    private void ModalPromptTag_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space) || sender is not Button { Tag: string tag })
            return;

        e.Handled = true;
        ApplyModalPromptTagSearch(tag);
    }

    private bool ApplyModalPromptTagSearch(string tag)
    {
        string normalizedTag = tag.Trim();
        if (normalizedTag.Length == 0)
            return false;

        List<string> queryTags = ParsePromptTags(SearchInput.Text);
        if (!queryTags.Contains(normalizedTag, StringComparer.OrdinalIgnoreCase))
            queryTags.Add(normalizedTag);

        SetSearchQuery(string.Join(", ", queryTags));
        CloseModal();
        SearchInput.Focus();
        return true;
    }

    private void ClearPreviewMetadataCopyState()
    {
        _currentPreviewMetadata = null;
        _currentPreviewMetadataPath = null;
        _lastMetadataCopyText = "";
        if (CopyPreviewMetadataButton is null)
            return;
        CopyPreviewMetadataButton.IsEnabled = false;
        CopyPreviewMetadataButton.Content = "Copy";
        CopyPreviewMetadataButton.ToolTip = "No PNG metadata loaded";
        CopyPreviewPromptButton.IsEnabled = false;
        CopyPreviewPromptButton.Content = "Copy";
        CopyPreviewPromptButton.ToolTip = "No prompt metadata loaded";
        CopyPreviewNegativeButton.IsEnabled = false;
        CopyPreviewNegativeButton.Content = "Copy";
        CopyPreviewNegativeButton.ToolTip = "No negative prompt metadata loaded";
        if (ModalMetadataStatusText is not null)
            SyncModalMetadataSidebar();
    }

    private void CopyPreviewPrompt_Click(object sender, RoutedEventArgs e)
        => CopyCurrentPreviewMetadataValue(negative: false, useSystemClipboard: true);

    private void CopyPreviewNegative_Click(object sender, RoutedEventArgs e)
        => CopyCurrentPreviewMetadataValue(negative: true, useSystemClipboard: true);

    private bool CopyCurrentPreviewMetadataValue(bool negative, bool useSystemClipboard)
    {
        Tile? selected = SelectedTile();
        if (selected is null || _currentPreviewMetadata is null
            || !string.Equals(selected.Path, _currentPreviewMetadataPath, StringComparison.OrdinalIgnoreCase))
            return false;
        string text = (negative ? _currentPreviewMetadata.NegativePrompt : _currentPreviewMetadata.Prompt).Trim();
        if (text.Length == 0)
            return false;
        _lastMetadataCopyText = text;
        if (!useSystemClipboard)
            return true;
        Button button = negative ? CopyPreviewNegativeButton : CopyPreviewPromptButton;
        try
        {
            Clipboard.SetText(text);
            button.Content = "Copied";
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            button.ToolTip = $"Copy failed: {ex.Message}";
            return false;
        }
    }

    private void CopyPreviewMetadata_Click(object sender, RoutedEventArgs e)
        => CopyCurrentPreviewMetadata(useSystemClipboard: true);

    private bool CopyCurrentPreviewMetadata(bool useSystemClipboard)
    {
        Tile? selected = SelectedTile();
        if (selected is null
            || _currentPreviewMetadata is null
            || !string.Equals(selected.Path, _currentPreviewMetadataPath, StringComparison.OrdinalIgnoreCase))
            return false;

        string text = BuildPngMetadataCopyText(_currentPreviewMetadata);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        _lastMetadataCopyText = text;
        if (!useSystemClipboard)
            return true;

        try
        {
            Clipboard.SetText(text);
            CopyPreviewMetadataButton.Content = "Copied";
            CopyPreviewMetadataButton.ToolTip = "PNG metadata copied";
            return true;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            CopyPreviewMetadataButton.Content = "Copy";
            CopyPreviewMetadataButton.ToolTip = $"Copy failed: {ex.Message}";
            return false;
        }
    }

    private static string BuildPngMetadataCopyText(PngParametersMetadata metadata)
    {
        var lines = new List<string>();
        AddPngMetadataCopyLine(lines, "Prompt", metadata.Prompt);
        AddPngMetadataCopyLine(lines, "Negative prompt", metadata.NegativePrompt);
        foreach ((string key, string value) in metadata.Settings)
            AddPngMetadataCopyLine(lines, key, value);
        AddPngMetadataCopyLine(lines, "Raw parameters", metadata.Raw);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddPngMetadataCopyLine(ICollection<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            lines.Add($"{label}: {value.Trim()}");
    }

    private static void SetPreviewMetadataRow(TextBlock label, TextBlock value, string title, string? text)
    {
        bool visible = !string.IsNullOrWhiteSpace(text);
        label.Text = title;
        label.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        value.Text = visible ? text : "";
        value.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static PngParametersMetadata? ReadPngParametersMetadata(string path, CancellationToken token)
    {
        if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            var signature = new byte[8];
            if (!TryReadExactly(stream, signature) || !signature.SequenceEqual(PngSignature))
                return null;

            var chunkHeader = new byte[8];
            while (stream.Position + 12 <= stream.Length)
            {
                token.ThrowIfCancellationRequested();
                if (!TryReadExactly(stream, chunkHeader))
                    return null;

                int length = (chunkHeader[0] << 24) | (chunkHeader[1] << 16) | (chunkHeader[2] << 8) | chunkHeader[3];
                if (length < 0 || length > MaxPngMetadataChunkBytes || stream.Position + length + 4 > stream.Length)
                    return null;

                string type = Encoding.ASCII.GetString(chunkHeader, 4, 4);
                if (string.Equals(type, "IDAT", StringComparison.Ordinal))
                    return null;

                if (string.Equals(type, "tEXt", StringComparison.Ordinal))
                {
                    var data = new byte[length];
                    if (!TryReadExactly(stream, data) || !TrySkip(stream, 4))
                        return null;

                    int separator = Array.IndexOf(data, (byte)0);
                    if (separator <= 0 || !string.Equals(Encoding.Latin1.GetString(data, 0, separator), "parameters", StringComparison.Ordinal))
                        continue;

                    string raw = Encoding.UTF8.GetString(data, separator + 1, data.Length - separator - 1);
                    return ParsePngParameters(raw);
                }

                if (!TrySkip(stream, length + 4))
                    return null;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static PngParametersMetadata? ParsePngParameters(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        const string negativeMarker = "Negative prompt:";
        const string settingsMarker = "\nSteps:";
        int negativeStart = raw.IndexOf(negativeMarker, StringComparison.Ordinal);
        int settingsStart = raw.IndexOf(settingsMarker, StringComparison.Ordinal);
        string prompt = negativeStart >= 0 ? raw[..negativeStart].Trim() : (settingsStart >= 0 ? raw[..settingsStart].Trim() : raw.Trim());
        int negativeValueStart = negativeStart >= 0 ? negativeStart + negativeMarker.Length : -1;
        string negative = negativeValueStart >= 0
            ? raw[negativeValueStart..(settingsStart >= negativeValueStart ? settingsStart : raw.Length)].Trim()
            : "";
        string settings = settingsStart >= 0 ? raw[(settingsStart + 1)..].Trim() : "";
        return new PngParametersMetadata(prompt, negative, ParsePngSettings(settings), raw.Trim());
    }

    private static Dictionary<string, string> ParsePngSettings(string settings)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string item in SplitPngSettings(settings))
        {
            int separator = item.IndexOf(':');
            if (separator <= 0)
                continue;
            string key = item[..separator].Trim();
            string value = item[(separator + 1)..].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                parsed[key] = value;
        }
        return parsed;
    }

    private static IEnumerable<string> SplitPngSettings(string settings)
    {
        if (string.IsNullOrWhiteSpace(settings))
            yield break;

        var current = new StringBuilder();
        bool quoted = false;
        bool escaped = false;
        foreach (char character in settings)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }
            if (character == '\\')
            {
                current.Append(character);
                escaped = true;
                continue;
            }
            if (character == '"')
                quoted = !quoted;
            if (character == ',' && !quoted)
            {
                yield return current.ToString();
                current.Clear();
                continue;
            }
            current.Append(character);
        }
        if (current.Length > 0)
            yield return current.ToString();
    }

    private static bool TryReadExactly(Stream stream, byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
                return false;
            offset += read;
        }
        return true;
    }

    private static bool TrySkip(Stream stream, int count)
    {
        if (count < 0 || stream.Position + count > stream.Length)
            return false;
        stream.Seek(count, SeekOrigin.Current);
        return true;
    }

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchWatermark is not null)
            SearchWatermark.Visibility = string.IsNullOrEmpty(SearchInput.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (_initializing || _settingSearchQuery) return;
        ScheduleSearchFilter();
        ScheduleSearchStateSave();
    }

    private void ScheduleSearchFilter()
    {
        CancelPendingSearchFilter(completePending: true);
        long generation = ++_searchFilterGeneration;
        _scheduledSearchFilterGeneration = generation;
        _pendingSearchFilterCompletion = new TaskCompletionSource<SearchFilterCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);
        _searchFilterTimer.Stop();
        _searchFilterTimer.Start();
    }

    private void SearchFilterTimer_Tick(object? sender, EventArgs e)
    {
        _searchFilterTimer.Stop();
        _ = ApplySearchFilterAsync(_scheduledSearchFilterGeneration, _pendingSearchFilterCompletion);
    }

    private async Task ApplySearchFilterAsync(long generation, TaskCompletionSource<SearchFilterCompletion>? completion)
    {
        CancellationTokenSource cts = new();
        _searchFilterCts = cts;
        FilterSnapshot snapshot = CaptureFilterSnapshot();
        try
        {
            FilterResult result = await Task.Run(() => ComputeFilterResult(snapshot, cts.Token), cts.Token);
            if (cts.IsCancellationRequested || generation != _searchFilterGeneration)
            {
                completion?.TrySetResult(SearchFilterCompletion.DiscardedResult);
                return;
            }

            ApplyFilterResult(result, selectFirst: true);
            _lastAppliedSearchFilterGeneration = generation;
            completion?.TrySetResult(SearchFilterCompletion.AppliedResult);
        }
        catch (OperationCanceledException)
        {
            completion?.TrySetResult(SearchFilterCompletion.DiscardedResult);
        }
        catch (Exception ex)
        {
            completion?.TrySetResult(new SearchFilterCompletion(false, false, ex.Message));
            SetStatusToast("Search could not be updated. Retry the query.");
        }
        finally
        {
            if (ReferenceEquals(_searchFilterCts, cts))
                _searchFilterCts = null;
            cts.Dispose();
        }
    }

    private void CancelPendingSearchFilter(bool completePending)
    {
        _searchFilterTimer.Stop();
        _searchFilterCts?.Cancel();
        _searchFilterCts = null;
        if (completePending)
            _pendingSearchFilterCompletion?.TrySetResult(SearchFilterCompletion.DiscardedResult);
        _pendingSearchFilterCompletion = null;
    }

    private void ScheduleSearchStateSave()
    {
        if (_suppressStateSave)
            return;

        _searchStateSaveTimer.Stop();
        _searchStateSaveTimer.Start();
    }

    private void SearchStateSaveTimer_Tick(object? sender, EventArgs e)
    {
        _searchStateSaveTimer.Stop();
        SaveState();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        ApplyFiltersForCurrentFilterChange();
    }

    private void FavoriteFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || _syncingFavoriteFilterControls) return;

        bool favoritesOnly = FavoriteOnlyFilter?.IsChecked == true;
        bool unfavoriteOnly = UnfavoriteOnlyFilter?.IsChecked == true;
        if (sender == FavoriteOnlyFilter && favoritesOnly)
            unfavoriteOnly = false;
        else if (sender == UnfavoriteOnlyFilter && unfavoriteOnly)
            favoritesOnly = false;

        SetFavoriteFilterState(favoritesOnly, unfavoriteOnly, apply: true, persist: true);
    }

    private void FavoriteLevelFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || _syncingFavoriteFilterControls) return;
        SyncFavoriteFilterLevelsFromControls();
        ApplyFilters();
        SaveState();
    }

    private void ShowUnseenDots_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _showUnseenDots = ShowUnseenDots?.IsChecked == true;
        RefreshUnseenDots();
        SaveState();
    }

    private void ToggleFoldersSection_Click(object sender, RoutedEventArgs e)
    {
        _foldersSectionExpanded = !_foldersSectionExpanded;
        SyncFoldersSectionControls();
        if (!_initializing)
            SaveState();
    }

    private void SyncFoldersSectionControls()
    {
        if (FoldersSectionContent is null || FoldersSectionToggleText is null) return;
        FoldersSectionContent.Visibility = _foldersSectionExpanded ? Visibility.Visible : Visibility.Collapsed;
        FoldersSectionToggleText.Text = _foldersSectionExpanded ? "−" : "+";
    }

    private void ToggleFolderBucket_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: FolderBucketView bucket })
            SetFolderBucketHidden(bucket.Key, !bucket.Hidden);
    }

    private void ShowAllFolderBuckets_Click(object sender, RoutedEventArgs e)
    {
        if (_hiddenFolderBuckets.Count == 0)
            return;

        _hiddenFolderBuckets.Clear();
        ApplyFolderBucketFilterChange();
    }

    private void HideAllFolderBuckets_Click(object sender, RoutedEventArgs e)
    {
        var keys = _folderBucketViews.Select(static bucket => bucket.Key).Where(static key => !string.IsNullOrWhiteSpace(key)).ToList();
        if (keys.Count == 0)
            return;

        bool changed = false;
        foreach (string key in keys)
            changed |= _hiddenFolderBuckets.Add(key);

        if (changed)
            ApplyFolderBucketFilterChange();
    }

    private void InvertFolderBuckets_Click(object sender, RoutedEventArgs e)
    {
        var keys = _folderBucketViews.Select(static bucket => bucket.Key).Where(static key => !string.IsNullOrWhiteSpace(key)).ToList();
        if (keys.Count == 0)
            return;

        var next = keys.Where(key => !_hiddenFolderBuckets.Contains(key)).ToList();
        _hiddenFolderBuckets.Clear();
        foreach (string key in next)
            _hiddenFolderBuckets.Add(key);
        ApplyFolderBucketFilterChange();
    }

    private void ShowSelectedFolderBuckets_Click(object sender, RoutedEventArgs e)
        => SetSelectedFolderBucketsHidden(hidden: false);

    private void HideSelectedFolderBuckets_Click(object sender, RoutedEventArgs e)
        => SetSelectedFolderBucketsHidden(hidden: true);

    private bool SetSelectedFolderBucketsHidden(bool hidden)
    {
        var keys = _folderBucketViews
            .Where(bucket => _selectedFolderBucketKeys.Contains(bucket.Key))
            .Select(static bucket => bucket.Key)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToList();
        if (keys.Count == 0)
            return false;

        bool changed = false;
        foreach (string key in keys)
            changed |= hidden ? _hiddenFolderBuckets.Add(key) : _hiddenFolderBuckets.Remove(key);
        if (changed)
            ApplyFolderBucketFilterChange();
        return changed;
    }

    private bool SetFolderBucketHidden(string key, bool hidden)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        bool changed = hidden ? _hiddenFolderBuckets.Add(key) : _hiddenFolderBuckets.Remove(key);
        if (changed)
            ApplyFolderBucketFilterChange();

        return changed;
    }

    private void ApplyFolderBucketFilterChange()
    {
        List<string> selectedKeys = _selectedFolderBucketKeys.ToList();
        string? primaryKey = _primarySelectedFolderBucketKey;
        PruneHiddenFolderBucketsToCurrentSet();
        RefreshFolderBucketViews();
        SetFolderBucketSelection(selectedKeys, primaryKey, persist: false);
        ApplyFilters();
        SaveState();
    }

    private bool SetFavoriteFilterLevels(IEnumerable<int> levels)
    {
        var normalized = levels.Where(level => level is >= MinFavoriteFilterLevel and <= MaxFavoriteFilterLevel).ToHashSet();
        if (_favoriteFilterLevels.SetEquals(normalized)) return false;
        _favoriteFilterLevels.Clear();
        _favoriteFilterLevels.UnionWith(normalized);
        SyncFavoriteFilterControls();
        if (!_initializing) { ApplyFilters(); SaveState(); }
        return true;
    }

    private void SetFavoriteFilterState(bool favoritesOnly, bool unfavoriteOnly, bool apply, bool persist)
    {
        if (favoritesOnly && unfavoriteOnly)
            unfavoriteOnly = false;

        if (FavoriteOnlyFilter is not null && UnfavoriteOnlyFilter is not null)
        {
            _syncingFavoriteFilterControls = true;
            try
            {
                FavoriteOnlyFilter.IsChecked = favoritesOnly;
                UnfavoriteOnlyFilter.IsChecked = unfavoriteOnly;
            }
            finally
            {
                _syncingFavoriteFilterControls = false;
            }
        }

        SyncFavoriteFilterControls();

        if (apply)
            ApplyFilters();
        if (persist && !_initializing)
            SaveState();
    }

    private void SyncFavoriteFilterControls()
    {
        if (FavoriteLevel1Filter is null
            || FavoriteLevel2Filter is null
            || FavoriteLevel3Filter is null
            || FavoriteLevel4Filter is null
            || FavoriteLevel5Filter is null)
        {
            return;
        }

        bool favoritesOnly = FavoriteOnlyFilter?.IsChecked == true;
        bool unfavoriteOnly = UnfavoriteOnlyFilter?.IsChecked == true;

        _syncingFavoriteFilterControls = true;
        try
        {
            FavoriteLevel1Filter.IsChecked = _favoriteFilterLevels.Contains(1);
            FavoriteLevel2Filter.IsChecked = _favoriteFilterLevels.Contains(2);
            FavoriteLevel3Filter.IsChecked = _favoriteFilterLevels.Contains(3);
            FavoriteLevel4Filter.IsChecked = _favoriteFilterLevels.Contains(4);
            FavoriteLevel5Filter.IsChecked = _favoriteFilterLevels.Contains(5);
        }
        finally
        {
            _syncingFavoriteFilterControls = false;
        }

        if (FavoriteLevelFilterPanel is not null)
            FavoriteLevelFilterPanel.IsEnabled = favoritesOnly;

        if (FavoriteFilterSummary is null)
            return;

        FavoriteFilterSummary.Text = favoritesOnly
            ? (_favoriteFilterLevels.Count == 0 ? "All ratings" : string.Join(" + ", _favoriteFilterLevels.OrderBy(static level => level).Select(static level => $"Lv {level}")))
            : unfavoriteOnly
                ? "Unrated only"
                : "All ratings";
    }

    private void SyncFavoriteFilterLevelsFromControls()
    {
        _favoriteFilterLevels.Clear();
        if (FavoriteLevel1Filter?.IsChecked == true) _favoriteFilterLevels.Add(1);
        if (FavoriteLevel2Filter?.IsChecked == true) _favoriteFilterLevels.Add(2);
        if (FavoriteLevel3Filter?.IsChecked == true) _favoriteFilterLevels.Add(3);
        if (FavoriteLevel4Filter?.IsChecked == true) _favoriteFilterLevels.Add(4);
        if (FavoriteLevel5Filter?.IsChecked == true) _favoriteFilterLevels.Add(5);
        SyncFavoriteFilterControls();
    }

    private void PruneHiddenFolderBucketsToCurrentSet()
    {
        if (_currentFolderSet.Count == 0)
        {
            _hiddenFolderBuckets.Clear();
            return;
        }

        var active = new HashSet<string>(_currentFolderSet, StringComparer.OrdinalIgnoreCase);
        _hiddenFolderBuckets.RemoveWhere(key => !active.Contains(key));
    }

    private void ApplyFiltersForCurrentFilterChange()
    {
        if (UnseenOnlyFilter?.IsChecked == true)
        {
            var previous = SelectedTile();
            ApplyFilters(selectFirst: false);
            if (previous is not null && !_tiles.Contains(previous))
                SelectTile(null);
            return;
        }

        ApplyFilters();
    }

    private void FavoriteDecrease_Click(object sender, RoutedEventArgs e)
    {
        AdjustSelectedFavorite(-1);
    }

    private void FavoriteIncrease_Click(object sender, RoutedEventArgs e)
    {
        AdjustSelectedFavorite(1);
    }

    private void BulkFavoriteLevel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string rawLevel }
            && int.TryParse(rawLevel, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
        {
            SetFavoriteLevelForSelection(level);
        }
    }

    private void ToggleSelectedFavorite_Click(object sender, RoutedEventArgs e)
    {
        ToggleSelectedFavorite();
    }

    private void ModalFavoriteIncrease_Click(object sender, RoutedEventArgs e)
        => AdjustSelectedFavorite(1);

    private void ModalFavoriteDecrease_Click(object sender, RoutedEventArgs e)
        => AdjustSelectedFavorite(-1);

    private bool ToggleSelectedFavorite()
    {
        if (SelectedTile() is not { IsRealFile: true } tile)
            return false;

        return SetFavoriteLevel(tile, tile.Fav > 0 ? 0 : 5);
    }

    private bool AdjustSelectedFavorite(int delta)
    {
        if (SelectedTiles().Count > 1)
            return MutateSelectedFavorites(tile => Math.Clamp(tile.Fav + delta, 0, 5), $"Adjusted favorite for {{0:N0}} selected images.");

        if (SelectedTile() is not { IsRealFile: true } tile)
            return false;

        int next = Math.Clamp(tile.Fav + delta, 0, 5);
        return SetFavoriteLevel(tile, next);
    }

    private bool SetFavoriteLevelForSelection(int level)
    {
        int clamped = Math.Clamp(level, 0, 5);
        return MutateSelectedFavorites(_ => clamped, $"Set favorite level {clamped} for {{0:N0}} selected images.");
    }

    private bool MutateSelectedFavorites(Func<Tile, int> levelSelector, string successMessageFormat)
    {
        var selected = SelectedTiles().Where(static tile => tile.IsRealFile).ToList();
        if (selected.Count == 0)
            return false;
        if (selected.Count == 1)
            return SetFavoriteLevel(selected[0], levelSelector(selected[0]));

        var previousFavorites = new Dictionary<string, int>(_favorites, StringComparer.OrdinalIgnoreCase);
        var previousDirtyPaths = new HashSet<string>(_favoriteDirtyPaths, StringComparer.OrdinalIgnoreCase);
        var nextLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Tile tile in selected)
        {
            string key = NormalizeFavoritePath(tile.Path);
            int next = Math.Clamp(levelSelector(tile), 0, 5);
            nextLevels[key] = next;
            if (next > 0)
                _favorites[key] = next;
            else
                _favorites.Remove(key);
            _favoriteDirtyPaths.Add(key);
        }

        if (!SaveFavorites())
        {
            _favorites.Clear();
            foreach (var item in previousFavorites) _favorites[item.Key] = item.Value;
            _favoriteDirtyPaths.Clear();
            _favoriteDirtyPaths.UnionWith(previousDirtyPaths);
            return false;
        }

        foreach (Tile tile in selected)
        {
            string key = NormalizeFavoritePath(tile.Path);
            tile.Fav = nextLevels[key];
        }

        ApplyFilters();
        SyncSelectionActionSurface();
        if (Modal.Visibility == Visibility.Visible)
        {
            if (SelectedTile() is null)
                Modal.Visibility = Visibility.Collapsed;
            else
                OpenModal();
        }
        SaveState();
        SetStatusToast(string.Format(CultureInfo.InvariantCulture, successMessageFormat, selected.Count));
        return true;
    }

    private void SyncSelectionActionSurface()
    {
        if (BulkFavoritePanel is null || SingleSelectionActions is null || BulkSelectionText is null)
            return;

        var selected = SelectedTiles().Where(static tile => tile.IsRealFile).ToList();
        bool bulk = selected.Count > 1;
        BulkFavoritePanel.Visibility = bulk ? Visibility.Visible : Visibility.Collapsed;
        SingleSelectionActions.Visibility = bulk ? Visibility.Collapsed : Visibility.Visible;
        if (!bulk)
            return;

        int distinctLevels = selected.Select(static tile => tile.Fav).Distinct().Count();
        string levelSummary = distinctLevels == 1 ? $"Lv {selected[0].Fav}" : "mixed levels";
        BulkSelectionText.Text = $"{selected.Count:N0} images selected · {levelSummary}";
    }

    private bool SetFavoriteLevel(Tile tile, int level)
    {
        if (!tile.IsRealFile)
            return false;

        int clamped = Math.Clamp(level, 0, 5);
        string key = NormalizeFavoritePath(tile.Path);
        int previousLevel = tile.Fav;
        bool hadStoredLevel = _favorites.TryGetValue(key, out int previousStoredLevel);

        if (clamped > 0)
            _favorites[key] = clamped;
        else
            _favorites.Remove(key);
        _favoriteDirtyPaths.Add(key);

        if (!SaveFavorites())
        {
            if (hadStoredLevel)
                _favorites[key] = previousStoredLevel;
            else
                _favorites.Remove(key);
            _favoriteDirtyPaths.Remove(key);
            return false;
        }

        tile.Fav = clamped;
        ApplyFilters();
        if (_tiles.Contains(tile))
            SelectTile(tile);
        else if (previousLevel != clamped)
            UpdateHeaderStats();

        if (Modal.Visibility == Visibility.Visible)
        {
            if (SelectedTile() is null)
                Modal.Visibility = Visibility.Collapsed;
            else
                OpenModal();
        }

        SaveState();
        return true;
    }

    private void OpenSelectedPreviewTab_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTile() is { IsRealFile: true } tile)
            OpenPreviewTab(tile, makeActive: true);
    }

    private void ActivatePreviewTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab })
            ActivatePreviewTab(tab.Path);
    }

    private void PreviewTab_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab } target)
            ShowPreviewTabHover(tab, target);
    }

    private void PreviewTab_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab } target)
        {
            ShowPreviewTabHover(tab, target);
            if (_previewTabDragSource == tab
                && _previewTabDragStartPoint is Point start
                && e.LeftButton == MouseButtonState.Pressed)
            {
                Point current = e.GetPosition(target);
                if (Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance
                    || Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance)
                {
                    _previewTabDragStartPoint = null;
                    try
                    {
                        DragDrop.DoDragDrop(target, new DataObject(typeof(PreviewTabView), tab), DragDropEffects.Move);
                    }
                    finally
                    {
                        _previewTabDragSource = null;
                        SetPreviewTabDragOver(null);
                    }
                }
            }
        }
    }

    private void PreviewTab_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab })
            HidePreviewTabHover(tab.Path);
    }

    private void PreviewTab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab } target)
        {
            _previewTabDragSource = tab;
            _previewTabDragStartPoint = e.GetPosition(target);
        }
    }

    private void PreviewTab_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _previewTabDragStartPoint = null;
        _previewTabDragSource = null;
    }

    private void PreviewTab_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle
            && sender is FrameworkElement { Tag: PreviewTabView tab })
        {
            ClosePreviewTab(tab.Path);
            e.Handled = true;
        }
    }

    private void PreviewTab_DragEnter(object sender, DragEventArgs e) => PreviewTab_DragOver(sender, e);

    private void PreviewTab_DragOver(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PreviewTabView tab }
            || !TryGetDraggedPreviewTab(e.Data, out PreviewTabView? source)
            || ReferenceEquals(source, tab))
        {
            e.Effects = DragDropEffects.None;
            SetPreviewTabDragOver(null);
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        SetPreviewTabDragOver(tab);
        e.Handled = true;
    }

    private void PreviewTab_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab } && tab.IsDragOver)
            SetPreviewTabDragOver(null);
    }

    private void PreviewTab_Drop(object sender, DragEventArgs e)
    {
        try
        {
            PreviewTabView? source = null;
            if (sender is not FrameworkElement { Tag: PreviewTabView target })
            {
                ReportPreviewTabReorderFailure(source);
                return;
            }

            if (!TryGetDraggedPreviewTab(e.Data, out source) || source is null)
            {
                ReportPreviewTabReorderFailure(source);
                return;
            }

            if (ReferenceEquals(source, target))
                return;

            int sourceIndex = _previewTabs.IndexOf(source);
            int targetIndex = _previewTabs.IndexOf(target);
            if (sourceIndex < 0 || targetIndex < 0)
            {
                ReportPreviewTabReorderFailure(source);
                return;
            }

            bool placeAfter = e.GetPosition((IInputElement)sender).X >= ((FrameworkElement)sender).ActualWidth / 2;
            int destination = targetIndex + (placeAfter ? 1 : 0);
            if (sourceIndex < destination)
                destination--;
            MovePreviewTab(source, destination, reportFailure: true);
        }
        finally
        {
            SetPreviewTabDragOver(null);
            e.Handled = true;
        }
    }

    private void ClosePreviewTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab })
            ClosePreviewTab(tab.Path);
    }

    private void TogglePreviewTabPin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab })
            TogglePreviewTabPin(tab.Path);
    }

    private void RestorePreviewTab_Click(object sender, RoutedEventArgs e) => RestoreLastClosedPreviewTab();

    private void CloseAllPreviewTabs_Click(object sender, RoutedEventArgs e) => CloseAllPreviewTabs();

    private Tile? ReconcilePreviewTabsWithCurrentCatalog()
    {
        IReadOnlyList<string> requestedPaths = _previewTabsPersistenceReady
            ? _previewTabs.Select(static tab => tab.Path).ToList()
            : _restoredPreviewTabPaths;
        string? requestedActivePath = _previewTabsPersistenceReady
            ? _activePreviewTabPath
            : _restoredActivePreviewTabPath;

        var currentByPath = _tiles
            .Where(static tile => tile.IsRealFile)
            .ToDictionary(tile => NormalizeFavoritePath(tile.Path), StringComparer.OrdinalIgnoreCase);
        var restoredTiles = new List<Tile>();
        foreach (string path in NormalizePreviewTabPaths(requestedPaths))
        {
            if (currentByPath.TryGetValue(path, out Tile? tile))
                restoredTiles.Add(tile);
        }

        _previewTabs.Clear();
        foreach (Tile tile in restoredTiles)
        {
            bool isPinned = NormalizePinnedPreviewPath(tile.Path) is string normalizedPath
                && _pinnedPreviewPaths.Contains(normalizedPath);
            _previewTabs.Add(new PreviewTabView(tile.Path, tile.FileName, isPinned));
        }

        string? normalizedActivePath = NormalizePinnedPreviewPath(requestedActivePath);
        Tile? active = normalizedActivePath is null
            ? null
            : restoredTiles.FirstOrDefault(tile => string.Equals(tile.Path, normalizedActivePath, StringComparison.OrdinalIgnoreCase));
        active ??= restoredTiles.FirstOrDefault();
        _activePreviewTabPath = active?.Path;
        _restoredPreviewTabPaths.Clear();
        _restoredActivePreviewTabPath = null;
        _previewTabsPersistenceReady = true;
        _closedPreviewTabs.RemoveAll(tile => !_allTiles.Contains(tile));
        RefreshPreviewTabs();
        return active;
    }

    private bool OpenPreviewTab(Tile tile, bool makeActive)
    {
        if (!tile.IsRealFile)
            return false;

        if (_previewTabs.All(tab => !string.Equals(tab.Path, tile.Path, StringComparison.OrdinalIgnoreCase)))
        {
            bool isPinned = NormalizePinnedPreviewPath(tile.Path) is string normalizedPath
                && _pinnedPreviewPaths.Contains(normalizedPath);
            _previewTabs.Add(new PreviewTabView(tile.Path, tile.FileName, isPinned));
        }

        _closedPreviewTabs.RemoveAll(closed => string.Equals(closed.Path, tile.Path, StringComparison.OrdinalIgnoreCase));

        if (makeActive)
            return ActivatePreviewTab(tile.Path);

        RefreshPreviewTabs();
        return true;
    }

    private bool TogglePreviewTabPin(string path)
    {
        var tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
            return false;

        string? normalizedPath = NormalizePinnedPreviewPath(tab.Path);
        if (normalizedPath is null)
            return false;
        bool isPinned = !_pinnedPreviewPaths.Remove(normalizedPath);
        if (isPinned)
            _pinnedPreviewPaths.Add(normalizedPath);
        tab.IsPinned = isPinned;
        RefreshPreviewTabs();
        SaveState();
        return true;
    }

    private bool ActivatePreviewTab(string path)
    {
        var tile = _tiles.FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
        if (tile is null)
            return false;

        HidePreviewTabHover(path);
        _activePreviewTabPath = tile.Path;
        SelectTile(tile);
        RefreshPreviewTabs();
        SaveState();
        return true;
    }

    private bool ClosePreviewTab(string path)
    {
        var tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
            return false;

        HidePreviewTabHover(path);
        _previewTabs.Remove(tab);
        if (_allTiles.FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase)) is { } closed)
        {
            _closedPreviewTabs.RemoveAll(candidate => string.Equals(candidate.Path, closed.Path, StringComparison.OrdinalIgnoreCase));
            _closedPreviewTabs.Insert(0, closed);
            if (_closedPreviewTabs.Count > 30)
                _closedPreviewTabs.RemoveRange(30, _closedPreviewTabs.Count - 30);
        }

        if (string.Equals(_activePreviewTabPath, path, StringComparison.OrdinalIgnoreCase))
        {
            _activePreviewTabPath = null;
            if (_previewTabs.LastOrDefault() is { } next)
                ActivatePreviewTab(next.Path);
            else
            {
                RefreshPreviewTabs();
                FocusRestorePreviewTabButton();
            }
        }
        else
        {
            RefreshPreviewTabs();
        }

        SaveState();
        return true;
    }

    private bool RestoreLastClosedPreviewTab()
    {
        while (_closedPreviewTabs.Count > 0)
        {
            var tile = _closedPreviewTabs[0];
            _closedPreviewTabs.RemoveAt(0);
            if (_tiles.Contains(tile))
            {
                bool restored = OpenPreviewTab(tile, makeActive: true);
                if (restored)
                    FocusPreviewTab(tile.Path);
                return restored;
            }
        }

        RefreshPreviewTabs();
        return false;
    }

    private void CloseAllPreviewTabs()
    {
        foreach (var tab in _previewTabs.ToList())
        {
            if (_allTiles.FirstOrDefault(candidate => string.Equals(candidate.Path, tab.Path, StringComparison.OrdinalIgnoreCase)) is { } tile)
            {
                _closedPreviewTabs.RemoveAll(candidate => string.Equals(candidate.Path, tile.Path, StringComparison.OrdinalIgnoreCase));
                _closedPreviewTabs.Insert(0, tile);
            }
        }

        if (_closedPreviewTabs.Count > 30)
            _closedPreviewTabs.RemoveRange(30, _closedPreviewTabs.Count - 30);

        HidePreviewTabHover();
        _previewTabs.Clear();
        _activePreviewTabPath = null;
        RefreshPreviewTabs();
        SaveState();
    }

    private static bool TryGetDraggedPreviewTab(IDataObject data, out PreviewTabView? tab)
    {
        tab = data.GetDataPresent(typeof(PreviewTabView)) ? data.GetData(typeof(PreviewTabView)) as PreviewTabView : null;
        return tab is not null;
    }

    private void SetPreviewTabDragOver(PreviewTabView? target)
    {
        foreach (PreviewTabView tab in _previewTabs)
            tab.IsDragOver = ReferenceEquals(tab, target);
    }

    private bool MovePreviewTab(PreviewTabView? tab, int destinationIndex, bool reportFailure)
    {
        int sourceIndex = tab is null ? -1 : _previewTabs.IndexOf(tab);
        if (sourceIndex < 0 || destinationIndex < 0 || destinationIndex >= _previewTabs.Count)
        {
            if (reportFailure)
                ReportPreviewTabReorderFailure(tab);
            return false;
        }

        if (sourceIndex == destinationIndex)
            return false;

        _previewTabs.Move(sourceIndex, destinationIndex);
        RefreshPreviewTabs();
        SaveState();
        FocusPreviewTab(tab!.Path);
        return true;
    }

    private void ReportPreviewTabReorderFailure(PreviewTabView? tab)
    {
        SetStatusToast("Preview tab reorder was not applied. The existing tab order was preserved.");
        if (tab is not null)
            FocusPreviewTab(tab.Path);
    }

    private bool TryReorderFocusedPreviewTab(int delta)
    {
        if (delta == 0 || !TryGetFocusedPreviewTab(out PreviewTabView? tab) || tab is null)
            return false;

        int sourceIndex = _previewTabs.IndexOf(tab);
        return sourceIndex >= 0 && MovePreviewTab(tab, sourceIndex + delta, reportFailure: false);
    }

    private static bool TryGetFocusedPreviewTab(out PreviewTabView? tab)
    {
        DependencyObject? current = Keyboard.FocusedElement as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { Tag: PreviewTabView previewTab })
            {
                tab = previewTab;
                return true;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        tab = null;
        return false;
    }

    private void FocusPreviewTab(string path)
        => Dispatcher.BeginInvoke(() =>
        {
            PreviewTabView? tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase));
            if (tab is null)
                return;

            FindPreviewTabButton(tab)?.Focus();
        }, DispatcherPriority.Input);

    private Button? FindPreviewTabButton(PreviewTabView tab)
    {
        UpdateLayout();
        return FindVisualDescendants<Button>(PreviewTabList)
            .FirstOrDefault(candidate => candidate.Tag == tab && candidate.Content is StackPanel);
    }

    private void FocusRestorePreviewTabButton()
        => Dispatcher.BeginInvoke(() =>
        {
            if (RestorePreviewTabButton.IsVisible && RestorePreviewTabButton.IsEnabled)
                RestorePreviewTabButton.Focus();
        }, DispatcherPriority.Input);

    private void RefreshPreviewTabs()
    {
        foreach (var tab in _previewTabs)
        {
            tab.IsActive = string.Equals(tab.Path, _activePreviewTabPath, StringComparison.OrdinalIgnoreCase);
            tab.IsPinned = NormalizePinnedPreviewPath(tab.Path) is string normalizedPath
                && _pinnedPreviewPaths.Contains(normalizedPath);
        }

        if (PreviewTabsEmptyText is not null)
            PreviewTabsEmptyText.Visibility = _previewTabs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (CloseAllPreviewTabsButton is not null)
            CloseAllPreviewTabsButton.IsEnabled = _previewTabs.Count > 0;
        if (RestorePreviewTabButton is not null)
            RestorePreviewTabButton.IsEnabled = _closedPreviewTabs.Count > 0;
    }

    private bool ShowPreviewTabHover(PreviewTabView tab, FrameworkElement? placementTarget, bool forceDecode = false)
    {
        var tile = _allTiles.FirstOrDefault(candidate => string.Equals(candidate.Path, tab.Path, StringComparison.OrdinalIgnoreCase));
        if (tile is null || PreviewTabHoverPopup is null)
            return false;

        if (PreviewTabHoverPopup.IsOpen
            && string.Equals(_hoverPreviewTabPath, tile.Path, StringComparison.OrdinalIgnoreCase))
        {
            if (placementTarget is not null)
                PreviewTabHoverPopup.PlacementTarget = placementTarget;
            return true;
        }

        CancelPreviewTabHoverDecode();
        _hoverPreviewTabPath = tile.Path;
        PreviewTabHoverName.Text = tile.FileName;
        PreviewTabHoverPath.Text = tile.Path;
        _hoverPreviewTabBitmapPath = null;
        PreviewTabHoverBitmap.Source = null;
        if (placementTarget is not null)
            PreviewTabHoverPopup.PlacementTarget = placementTarget;
        PreviewTabHoverPopup.IsOpen = true;

        if (!forceDecode && tile.Thumbnail is not null)
        {
            PreviewTabHoverBitmap.Source = tile.Thumbnail;
            _hoverPreviewTabBitmapPath = tile.Path;
            var immediate = new TaskCompletionSource<PreviewTabHoverDecodeCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);
            immediate.TrySetResult(PreviewTabHoverDecodeCompletion.AppliedResult);
            _previewTabHoverCompletion = immediate;
            _lastPreviewTabHoverCompletion = immediate;
            return true;
        }

        var cts = new CancellationTokenSource();
        _previewTabHoverCts = cts;
        long generation = ++_previewTabHoverGeneration;
        var completion = new TaskCompletionSource<PreviewTabHoverDecodeCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);
        _previewTabHoverCompletion = completion;
        _lastPreviewTabHoverCompletion = completion;
        _previewTabHoverDecodeStartCount++;
        _ = DecodePreviewTabHoverAsync(tile.Path, generation, cts, completion);
        return true;
    }

    private async Task DecodePreviewTabHoverAsync(
        string path,
        long generation,
        CancellationTokenSource cts,
        TaskCompletionSource<PreviewTabHoverDecodeCompletion> completion)
    {
        try
        {
            if (_previewTabHoverDecodeDelaysForSmoke.TryGetValue(path, out int delayMilliseconds) && delayMilliseconds > 0)
                await Task.Delay(delayMilliseconds, cts.Token);

            BitmapSource? bitmap = await Task.Run(() => LoadBitmap(path, 360), cts.Token);
            if (cts.IsCancellationRequested || generation != _previewTabHoverGeneration
                || !string.Equals(_hoverPreviewTabPath, path, StringComparison.OrdinalIgnoreCase)
                || PreviewTabHoverPopup?.IsOpen != true)
            {
                completion.TrySetResult(PreviewTabHoverDecodeCompletion.DiscardedResult);
                return;
            }

            if (bitmap is null)
            {
                _previewTabHoverDecodeFailureCount++;
                PreviewTabHoverBitmap.Source = null;
                _hoverPreviewTabBitmapPath = null;
                SetStatusToast("Preview tab image could not be decoded. You can continue browsing.");
                completion.TrySetResult(PreviewTabHoverDecodeCompletion.FailedResult);
                return;
            }

            PreviewTabHoverBitmap.Source = bitmap;
            _hoverPreviewTabBitmapPath = path;
            completion.TrySetResult(PreviewTabHoverDecodeCompletion.AppliedResult);
        }
        catch (OperationCanceledException)
        {
            completion.TrySetResult(PreviewTabHoverDecodeCompletion.DiscardedResult);
        }
        catch
        {
            if (generation == _previewTabHoverGeneration
                && string.Equals(_hoverPreviewTabPath, path, StringComparison.OrdinalIgnoreCase)
                && PreviewTabHoverPopup?.IsOpen == true)
            {
                _previewTabHoverDecodeFailureCount++;
                PreviewTabHoverBitmap.Source = null;
                _hoverPreviewTabBitmapPath = null;
                SetStatusToast("Preview tab image could not be decoded. You can continue browsing.");
                completion.TrySetResult(PreviewTabHoverDecodeCompletion.FailedResult);
            }
            else
            {
                completion.TrySetResult(PreviewTabHoverDecodeCompletion.DiscardedResult);
            }
        }
        finally
        {
            if (ReferenceEquals(_previewTabHoverCts, cts))
                _previewTabHoverCts = null;
            if (ReferenceEquals(_previewTabHoverCompletion, completion))
                _previewTabHoverCompletion = null;
            cts.Dispose();
        }
    }

    private void CancelPreviewTabHoverDecode()
    {
        _previewTabHoverGeneration++;
        _previewTabHoverCts?.Cancel();
        _previewTabHoverCts = null;
        _previewTabHoverCompletion?.TrySetResult(PreviewTabHoverDecodeCompletion.DiscardedResult);
        _previewTabHoverCompletion = null;
    }

    private bool HidePreviewTabHover(string? path = null)
    {
        if (!string.IsNullOrWhiteSpace(path)
            && !string.Equals(_hoverPreviewTabPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool wasVisible = PreviewTabHoverPopup?.IsOpen == true || _hoverPreviewTabPath is not null;
        CancelPreviewTabHoverDecode();
        _hoverPreviewTabPath = null;
        _hoverPreviewTabBitmapPath = null;
        if (PreviewTabHoverPopup is not null)
            PreviewTabHoverPopup.IsOpen = false;
        if (PreviewTabHoverBitmap is not null)
            PreviewTabHoverBitmap.Source = null;
        if (PreviewTabHoverName is not null)
            PreviewTabHoverName.Text = "";
        if (PreviewTabHoverPath is not null)
            PreviewTabHoverPath.Text = "";
        return wasVisible;
    }

    public void SetSearchQuery(string query, bool persist = true)
    {
        bool previous = _suppressStateSave;
        _suppressStateSave = !persist;
        try
        {
            _settingSearchQuery = true;
            SearchInput.Text = query;
            ApplyFilters();
        }
        finally
        {
            _settingSearchQuery = false;
            _suppressStateSave = previous;
        }

        if (persist)
        {
            _searchStateSaveTimer.Stop();
            SaveState();
        }
    }

    public Task<SearchFilterCompletion> SetSearchInputForSmokeAsync(string query)
    {
        SearchInput.Text = query;
        return _pendingSearchFilterCompletion?.Task
            ?? Task.FromResult(new SearchFilterCompletion(false, false, "search input did not schedule a filter"));
    }

    public long LastAppliedSearchFilterGenerationForSmoke => _lastAppliedSearchFilterGeneration;

    public void SuppressStatePersistence()
    {
        _suppressStateSave = true;
    }

    private void ApplyFilters(bool selectFirst = true)
    {
        if (CardsList is null || RowsList is null) return;

        ++_searchFilterGeneration;
        CancelPendingSearchFilter(completePending: true);
        ApplyFilterResult(ComputeFilterResult(CaptureFilterSnapshot(), CancellationToken.None), selectFirst);
    }

    private FilterSnapshot CaptureFilterSnapshot()
    {
        string query = SearchInput?.Text?.Trim() ?? "";
        return new FilterSnapshot(
            _allTiles.Select(static tile => new FilterTileSnapshot(
                tile,
                tile.FileName,
                tile.Prompt,
                tile.Path,
                tile.IsRealFile,
                tile.Fav,
                tile.Enhanced,
                tile.Unseen,
                tile.FolderBucketKey,
                tile.ModifiedUtc,
                tile.CreatedUtc)).ToArray(),
            query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            FavoriteOnlyFilter?.IsChecked == true,
            UnfavoriteOnlyFilter?.IsChecked == true,
            EnhancedOnlyFilter?.IsChecked == true,
            UnseenOnlyFilter?.IsChecked == true,
            _favoriteFilterLevels.ToFrozenSet(),
            _hiddenFolderBuckets.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            _dateFromLocal,
            _dateToLocal,
            _sortBy,
            _randomSortSeed);
    }

    private static FilterResult ComputeFilterResult(FilterSnapshot snapshot, CancellationToken cancellationToken)
    {
        var filtered = new List<FilterTileSnapshot>(snapshot.Tiles.Length);
        for (int index = 0; index < snapshot.Tiles.Length; index++)
        {
            if ((index & 63) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            FilterTileSnapshot tile = snapshot.Tiles[index];
            if (!MatchesFilterSnapshot(tile, snapshot))
                continue;
            filtered.Add(tile);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<FilterTileSnapshot> ordered = snapshot.SortBy switch
        {
            SortModifiedOldestValue => filtered
                .OrderBy(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortCreatedNewestValue => filtered
                .OrderByDescending(static tile => tile.CreatedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortCreatedOldestValue => filtered
                .OrderBy(static tile => tile.CreatedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortRandomValue => filtered
                .OrderBy(tile => StableRandomSortKey(snapshot.RandomSortSeed, tile.Path))
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortNameValue => filtered
                .OrderBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            _ => filtered
                .OrderByDescending(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
        };
        return new FilterResult(ordered.Select(static tile => tile.Tile).ToList());
    }

    private static bool MatchesFilterSnapshot(FilterTileSnapshot tile, FilterSnapshot snapshot)
    {
        foreach (string token in snapshot.QueryTokens)
        {
            if (!ContainsText(tile.FileName, token) && !ContainsText(tile.Prompt, token))
                return false;
        }

        if (snapshot.FavoritesOnly && (tile.FavoriteLevel <= 0 || (snapshot.FavoriteLevels.Count > 0 && !snapshot.FavoriteLevels.Contains(tile.FavoriteLevel))))
            return false;
        if (snapshot.UnfavoriteOnly && tile.FavoriteLevel > 0)
            return false;
        if (snapshot.EnhancedOnly && !tile.Enhanced)
            return false;
        if (snapshot.UnseenOnly && !tile.Unseen)
            return false;
        if (tile.IsRealFile && !string.IsNullOrWhiteSpace(tile.FolderBucketKey) && snapshot.HiddenFolderBuckets.Contains(tile.FolderBucketKey))
            return false;
        if (!tile.IsRealFile || (!snapshot.DateFromLocal.HasValue && !snapshot.DateToLocal.HasValue))
            return true;

        DateTime createdDate = tile.CreatedUtc.ToLocalTime().Date;
        return (!snapshot.DateFromLocal.HasValue || createdDate >= snapshot.DateFromLocal.Value.Date)
            && (!snapshot.DateToLocal.HasValue || createdDate <= snapshot.DateToLocal.Value.Date);
    }

    private void ApplyFilterResult(FilterResult filterResult, bool selectFirst)
    {
        var previous = SelectedTile();
        List<Tile> filtered = filterResult.Tiles;

        var availablePaths = new HashSet<string>(filtered.Select(static tile => tile.Path), StringComparer.OrdinalIgnoreCase);
        _selectedPaths.IntersectWith(availablePaths);

        bool wasSyncingSelection = _syncingSelection;
        _syncingSelection = true;
        try
        {
            _tiles.Clear();
            foreach (var tile in filtered)
                _tiles.Add(tile);
        }
        finally
        {
            _syncingSelection = wasSyncingSelection;
        }

        Tile? preferred = !string.IsNullOrWhiteSpace(_primarySelectedPath)
            ? _tiles.FirstOrDefault(tile => string.Equals(tile.Path, _primarySelectedPath, StringComparison.OrdinalIgnoreCase))
            : null;
        preferred ??= previous is not null && filtered.Contains(previous) ? previous : null;
        preferred ??= SelectedTiles().LastOrDefault();
        preferred ??= selectFirst && _tiles.Count > 0 ? _tiles[0] : null;
        RebuildGridTiles(preferred);
        UpdateFolderStats();

        if (preferred is not null)
        {
            if (_selectedPaths.Count == 0)
                _selectedPaths.Add(preferred.Path);
            SetSelection(SelectedTiles(), preferred);
        }
        else if (filtered.Count == 0)
            SelectTile(null);
    }

    private void RebuildGridTiles(Tile? ensureTile = null)
    {
        if (_tiles.Count == 0)
        {
            RebuildGridWindow(0, 0);
            return;
        }

        int target = Math.Min(_tiles.Count, InitialGridRealizationCount);
        int startIndex = 0;

        if (ensureTile is not null)
        {
            int index = _tiles.IndexOf(ensureTile);
            if (index >= target)
            {
                target = Math.Min(_tiles.Count, MaxGridRealizationCount);
                int maxStart = Math.Max(0, _tiles.Count - target);
                startIndex = Math.Clamp(index - (target / 2), 0, maxStart);
            }
        }

        RebuildGridWindow(startIndex, target);
    }

    private void EnsureGridTileRealized(Tile tile)
    {
        int index = _tiles.IndexOf(tile);
        if (index < 0)
            return;

        int windowEnd = _gridStartIndex + _gridTiles.Count;
        if (index >= _gridStartIndex && index < windowEnd)
            return;

        int target = Math.Min(_tiles.Count, MaxGridRealizationCount);
        int maxStart = Math.Max(0, _tiles.Count - target);
        int startIndex = Math.Clamp(index - (target / 2), 0, maxStart);
        RebuildGridWindow(startIndex, target);
    }

    private void RealizeNextGridBatch()
    {
        if (_gridTiles.Count >= _tiles.Count)
            return;

        if (_gridTiles.Count < MaxGridRealizationCount)
        {
            RebuildGridWindow(_gridStartIndex, _gridTiles.Count + GridRealizationBatchSize);
            return;
        }

        int maxStart = Math.Max(0, _tiles.Count - _gridTiles.Count);
        if (_gridStartIndex >= maxStart)
            return;

        RebuildGridWindow(_gridStartIndex + GridRealizationBatchSize, _gridTiles.Count);
    }

    private void RealizePreviousGridBatch()
    {
        if (_gridStartIndex <= 0 || _gridTiles.Count == 0)
            return;

        RebuildGridWindow(_gridStartIndex - GridRealizationBatchSize, _gridTiles.Count);
    }

    private void RebuildGridWindow(int requestedStartIndex, int requestedCount)
    {
        bool wasSyncingSelection = _syncingSelection;
        _syncingSelection = true;
        try
        {
            _gridTiles.Clear();
            if (_tiles.Count == 0 || requestedCount <= 0)
            {
                _gridStartIndex = 0;
                if (LastLoadMetrics is not null)
                    UpdateGridMetrics(LastLoadMetrics);
                return;
            }

            int target = Math.Min(_tiles.Count, Math.Min(MaxGridRealizationCount, requestedCount));
            int maxStart = Math.Max(0, _tiles.Count - target);
            _gridStartIndex = Math.Clamp(requestedStartIndex, 0, maxStart);
            int end = Math.Min(_tiles.Count, _gridStartIndex + target);
            for (int i = _gridStartIndex; i < end; i++)
                _gridTiles.Add(_tiles[i]);

            if (!_initializing && _gridTiles.Any(static tile => tile.Thumbnail is null))
                _ = LoadThumbnailsAsync(_loadCts?.Token ?? CancellationToken.None);

            if (LastLoadMetrics is not null)
                UpdateGridMetrics(LastLoadMetrics);
        }
        finally
        {
            _syncingSelection = wasSyncingSelection;
        }

        SynchronizeSelectionControls();
    }

    private void CardsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (CardsList.Visibility != Visibility.Visible || _gridTiles.Count >= _tiles.Count)
            return;

        double remaining = e.ExtentHeight - (e.VerticalOffset + e.ViewportHeight);
        double threshold = Math.Max(360, e.ViewportHeight * 0.75);
        if (remaining <= threshold)
            RealizeNextGridBatch();
        else if (e.VerticalOffset <= threshold)
            RealizePreviousGridBatch();
    }

    private static bool MatchesSearch(Tile tile, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;

        var tokens = query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (ContainsText(tile.FileName, token)
                || ContainsText(tile.Prompt, token))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool ContainsText(string? value, string token)
        => !string.IsNullOrEmpty(value) && value.Contains(token, StringComparison.OrdinalIgnoreCase);

    private bool MatchesFavoriteFilter(Tile tile, bool favoritesOnly, bool unfavoriteOnly)
    {
        if (favoritesOnly)
            return tile.Fav > 0 && (_favoriteFilterLevels.Count == 0 || _favoriteFilterLevels.Contains(tile.Fav));
        if (unfavoriteOnly)
            return tile.Fav <= 0;
        return true;
    }

    private bool MatchesFolderBucketFilter(Tile tile)
        => !tile.IsRealFile
            || string.IsNullOrWhiteSpace(tile.FolderBucketKey)
            || !_hiddenFolderBuckets.Contains(tile.FolderBucketKey);

    private bool MatchesDateFilter(Tile tile)
    {
        if ((!_dateFromLocal.HasValue && !_dateToLocal.HasValue)
            || !tile.IsRealFile)
        {
            return true;
        }

        DateTime createdDate = tile.CreatedUtc.ToLocalTime().Date;
        if (_dateFromLocal.HasValue && createdDate < _dateFromLocal.Value.Date)
            return false;

        if (_dateToLocal.HasValue && createdDate > _dateToLocal.Value.Date)
            return false;

        return true;
    }

    private IEnumerable<Tile> SortTiles(IEnumerable<Tile> source)
    {
        return _sortBy switch
        {
            SortModifiedOldestValue => source
                .OrderBy(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortCreatedNewestValue => source
                .OrderByDescending(static tile => tile.CreatedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortCreatedOldestValue => source
                .OrderBy(static tile => tile.CreatedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortRandomValue => source
                .OrderBy(tile => StableRandomSortKey(_randomSortSeed, tile.Path))
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortNameValue => source
                .OrderBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            _ => source
                .OrderByDescending(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
        };
    }

    private void SelectFirstAvailable()
    {
        SelectTile(_tiles.Count > 0 ? _tiles[0] : null);
    }

    private void SelectRestoredOrFirst()
    {
        var restored = !string.IsNullOrWhiteSpace(_restoredSelectedPath)
            ? _tiles.FirstOrDefault(tile => string.Equals(tile.Path, _restoredSelectedPath, StringComparison.OrdinalIgnoreCase))
            : null;
        SelectTile(restored ?? (_tiles.Count > 0 ? _tiles[0] : null));
    }

    private void SelectTile(Tile? tile)
    {
        SetSelection(tile is null ? [] : [tile], tile);
    }

    private void ClearPreview()
    {
        _previewCts?.Cancel();
        _previewDecodeCompletion?.TrySetResult(PreviewDecodeResult.Canceled);
        _previewMetadataCts?.Cancel();
        _previewMetadataCompletion?.TrySetResult(null);
        ClearPreviewMetadataCopyState();
        _previewDecodedPath = null;
        PreviewBitmap.Source = null;
        PreviewBitmap.Visibility = Visibility.Collapsed;
        PreviewArtBase.Visibility = Visibility.Visible;
        PreviewArtGlow.Visibility = Visibility.Visible;
        PreviewFileName.Text = "No matching image";
        PreviewTabName.Text = "No selection";
        PreviewSizeText.Text = "-";
        PreviewModelText.Text = "-";
        PreviewDateText.Text = "-";
        PreviewPromptText.Text = "";
        FavoriteLevelText.Text = "0";
        ModalFavoriteLevelText.Text = "0";
        SyncSelectionActionSurface();
        ModalTitle.Text = "No selection";
        UpdateHeaderStats();
    }

    private void UpdateFolderStats()
    {
        if (FolderCountText is null) return;

        int total = _allTiles.Count;
        int visible = _tiles.Count;
        string loaded = $"{total:N0} images indexed";
        FolderCountText.Text = visible == total ? loaded : $"{visible:N0} shown / {loaded}";
        UpdateHeaderStats();
    }

    private void UpdateHeaderStats()
    {
        if (HeaderStats is null) return;

        int selected = SelectedTiles().Count;
        int visible = _tiles.Count;
        int total = _allTiles.Count;
        string imageText = visible == total ? $"{total:N0} images" : $"{visible:N0} / {total:N0} images";
        string folderText = _currentFolderSet.Count == 0 ? "sample" : $"{_currentFolderSet.Count:N0} folder(s)";
        HeaderStats.Text = $"{selected:N0} selected - {imageText} - {folderText}";
    }

    private void UpdateGridMetrics(LoadMetrics metrics)
    {
        metrics.GridTotalItems = _tiles.Count;
        metrics.GridRealizedItems = _gridTiles.Count;
        metrics.GridDeferredItems = Math.Max(0, _tiles.Count - _gridTiles.Count);
        metrics.GridInitialRealizationLimit = InitialGridRealizationCount;
        metrics.GridRealizationBatchSize = GridRealizationBatchSize;
        metrics.GridMaxRealizationCount = MaxGridRealizationCount;
        metrics.GridWindowStartIndex = _gridStartIndex;
        metrics.GridWindowEndIndex = _gridStartIndex + _gridTiles.Count;
    }

    // ─────────── Size slider ───────────
    private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_restoringGridZoomAnchor)
            return;

        if (RowsList?.Visibility == Visibility.Visible)
        {
            _restoringGridZoomAnchor = true;
            try { SizeSlider.Value = e.OldValue; }
            finally { _restoringGridZoomAnchor = false; }
            return;
        }

        GridZoomAnchor? anchor = CaptureGridZoomAnchor();
        ApplyCardLayoutToAllTiles();
        RestoreGridZoomAnchorAfterLayout(anchor);
        if (!_initializing)
            SaveState();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => AdjustCardWidth(-1);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => AdjustCardWidth(1);
    private void ZoomReset_Click(object sender, RoutedEventArgs e) => ResetCardWidth();

    private bool AdjustCardWidth(int steps)
    {
        double before = SizeSlider.Value;
        SetCardWidth(before + (CardWidthStep * steps));
        return Math.Abs(SizeSlider.Value - before) > 0.01;
    }

    private bool ResetCardWidth()
    {
        double before = SizeSlider.Value;
        SetCardWidth(DefaultCardWidth);
        return Math.Abs(SizeSlider.Value - before) > 0.01;
    }

    private void SetCardWidth(double value)
    {
        if (RowsList?.Visibility == Visibility.Visible)
            return;
        SizeSlider.Value = Math.Clamp(value, SizeSlider.Minimum, SizeSlider.Maximum);
    }

    private GridZoomAnchor? CaptureGridZoomAnchor()
    {
        if (CardsList?.Visibility != Visibility.Visible)
            return null;
        ScrollViewer? viewer = FindVisualDescendant<ScrollViewer>(CardsList);
        if (viewer is null || viewer.ViewportHeight <= 0)
            return null;

        GridZoomAnchor? best = null;
        GridZoomAnchor? previous = null;
        double center = viewer.ViewportHeight / 2;
        foreach (var item in FindVisualDescendants<FrameworkElement>(CardsList))
        {
            if (item.DataContext is not Tile tile || !tile.IsRealFile || item.ActualHeight <= 0)
                continue;
            try
            {
                Point top = item.TransformToAncestor(viewer).Transform(new Point(0, 0));
                double distance = Math.Abs((top.Y + item.ActualHeight / 2) - center);
                if (best is null || distance < best.Value.CenterDistance)
                    best = new GridZoomAnchor(tile.Path, top.Y, distance);
                if (!string.IsNullOrWhiteSpace(_lastGridZoomAnchorPath) && string.Equals(tile.Path, _lastGridZoomAnchorPath, StringComparison.OrdinalIgnoreCase))
                    previous = new GridZoomAnchor(tile.Path, top.Y, distance);
            }
            catch (InvalidOperationException)
            {
                // The visual can be recycled while a scroll/layout pass is in flight.
            }
        }
        return previous ?? best;
    }

    private void RestoreGridZoomAnchorAfterLayout(GridZoomAnchor? anchor)
    {
        if (anchor is null)
            return;
        _lastGridZoomAnchorPath = anchor.Value.Path;
        Dispatcher.BeginInvoke(() =>
        {
            ScrollViewer? viewer = FindVisualDescendant<ScrollViewer>(CardsList);
            FrameworkElement? item = FindVisualDescendants<FrameworkElement>(CardsList)
                .FirstOrDefault(element => element.DataContext is Tile tile && string.Equals(tile.Path, anchor.Value.Path, StringComparison.OrdinalIgnoreCase));
            if (viewer is null || item is null)
                return;
            try
            {
                double before = item.TransformToAncestor(viewer).Transform(new Point(0, 0)).Y;
                double requested = Math.Clamp(viewer.VerticalOffset + (before - anchor.Value.ViewportY), 0, Math.Max(0, viewer.ExtentHeight - viewer.ViewportHeight));
                viewer.ScrollToVerticalOffset(requested);
                Dispatcher.BeginInvoke(() =>
                {
                    FrameworkElement? settled = FindVisualDescendants<FrameworkElement>(CardsList)
                        .FirstOrDefault(element => element.DataContext is Tile tile && string.Equals(tile.Path, anchor.Value.Path, StringComparison.OrdinalIgnoreCase));
                    if (settled is null) return;
                    double actual = settled.TransformToAncestor(viewer).Transform(new Point(0, 0)).Y;
                    _lastGridZoomAnchorDrift = Math.Abs(actual - anchor.Value.ViewportY);
                }, DispatcherPriority.Render);
            }
            catch (InvalidOperationException)
            {
            }
        }, DispatcherPriority.Render);
    }

    private static T? FindVisualDescendant<T>(DependencyObject root) where T : DependencyObject
        => FindVisualDescendants<T>(root).FirstOrDefault();

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;
            foreach (T nested in FindVisualDescendants<T>(child))
                yield return nested;
        }
    }

    private void ApplyCardLayoutToAllTiles()
    {
        foreach (var tile in _allTiles)
            ApplyCardLayout(tile);
    }

    private void ApplyCardLayout(Tile tile)
    {
        double baseWidth = SizeSlider?.Value ?? DefaultCardWidth;
        double minWidth = SizeSlider?.Minimum ?? 130;
        double maxWidth = SizeSlider?.Maximum ?? 280;
        (double widthFactor, double listThumbnailBase) = _displayStyle switch
        {
            DisplayStyleCompact => (0.82, 42),
            DisplayStylePoster => (1.12, 64),
            _ => (1.0, 52),
        };

        double width = Math.Clamp(baseWidth * widthFactor, minWidth, maxWidth);
        double aspectHeightFactor = AspectHeightFactor(tile);
        tile.CardWidth = width;
        tile.CardHeight = Math.Max(64, width * aspectHeightFactor);
        tile.ListThumbnailWidth = listThumbnailBase;
        tile.ListThumbnailHeight = Math.Clamp(listThumbnailBase * aspectHeightFactor, 32, 120);
        tile.ListThumbnailSize = Math.Max(tile.ListThumbnailWidth, tile.ListThumbnailHeight);
    }

    private double AspectHeightFactor(Tile tile)
    {
        return _aspectMode switch
        {
            AspectSquareValue => 1.0,
            AspectPortraitValue => 1.5,
            _ => OriginalAspectHeightFactor(tile),
        };
    }

    private static double OriginalAspectHeightFactor(Tile tile)
    {
        if (tile.ImagePixelWidth > 0 && tile.ImagePixelHeight > 0)
            return Math.Clamp((double)tile.ImagePixelHeight / tile.ImagePixelWidth, 0.65, 1.8);

        return 1.5;
    }

    private static string NormalizeDisplayStyle(string? style)
    {
        return style?.Trim().ToLowerInvariant() switch
        {
            DisplayStyleCompact => DisplayStyleCompact,
            DisplayStylePoster => DisplayStylePoster,
            _ => DisplayStyleStandard,
        };
    }

    private bool SetDisplayStyle(string style)
    {
        string normalized = NormalizeDisplayStyle(style);
        bool changed = !string.Equals(_displayStyle, normalized, StringComparison.Ordinal);
        _displayStyle = normalized;
        SyncDisplayStyleButtons();
        ApplyCardLayoutToAllTiles();
        if (!_initializing)
            SaveState();
        return changed;
    }

    private void SyncDisplayStyleButtons()
    {
        if (StyleStandard is null || StyleCompact is null || StylePoster is null)
            return;

        StyleStandard.IsChecked = _displayStyle == DisplayStyleStandard;
        StyleCompact.IsChecked = _displayStyle == DisplayStyleCompact;
        StylePoster.IsChecked = _displayStyle == DisplayStylePoster;
    }

    private static string NormalizeAspectMode(string? aspectMode)
    {
        return aspectMode?.Trim().ToLowerInvariant() switch
        {
            AspectSquareValue or "1:1" => AspectSquareValue,
            AspectPortraitValue or "2:3" => AspectPortraitValue,
            AspectOriginalValue => AspectOriginalValue,
            _ => AspectOriginalValue,
        };
    }

    private bool SetAspectMode(string aspectMode)
    {
        string normalized = NormalizeAspectMode(aspectMode);
        bool changed = !string.Equals(_aspectMode, normalized, StringComparison.Ordinal);
        _aspectMode = normalized;
        SyncAspectButtons();
        ApplyCardLayoutToAllTiles();
        if (changed && !_initializing)
            SaveState();
        return changed;
    }

    private void SyncAspectButtons()
    {
        if (AspectOriginalButton is null || AspectSquareButton is null || AspectPortraitButton is null)
            return;

        AspectOriginalButton.IsChecked = _aspectMode == AspectOriginalValue;
        AspectSquareButton.IsChecked = _aspectMode == AspectSquareValue;
        AspectPortraitButton.IsChecked = _aspectMode == AspectPortraitValue;
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            SortModifiedOldestValue or "oldest" => SortModifiedOldestValue,
            SortCreatedNewestValue => SortCreatedNewestValue,
            SortCreatedOldestValue => SortCreatedOldestValue,
            SortNameValue => SortNameValue,
            SortRandomValue => SortRandomValue,
            SortModifiedNewestValue or "newest" => SortModifiedNewestValue,
            _ => SortModifiedNewestValue,
        };
    }

    private bool SetSortBy(string sortBy)
    {
        string normalized = NormalizeSortBy(sortBy);
        bool changed = !string.Equals(_sortBy, normalized, StringComparison.Ordinal);
        _sortBy = normalized;
        SyncSortButtons();

        if (changed && !_initializing)
        {
            ApplyFilters();
            SaveState();
        }

        return changed;
    }

    private void SyncSortButtons()
    {
        if (SortModifiedNewest is null || SortModifiedOldest is null || SortCreatedNewest is null || SortCreatedOldest is null || SortName is null || SortRandom is null)
            return;

        SortModifiedNewest.IsChecked = _sortBy == SortModifiedNewestValue;
        SortModifiedOldest.IsChecked = _sortBy == SortModifiedOldestValue;
        SortCreatedNewest.IsChecked = _sortBy == SortCreatedNewestValue;
        SortCreatedOldest.IsChecked = _sortBy == SortCreatedOldestValue;
        SortName.IsChecked = _sortBy == SortNameValue;
        SortRandom.IsChecked = _sortBy == SortRandomValue;
    }

    private static ulong StableRandomSortKey(string seed, string path)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        ulong hash = offset;
        foreach (char value in seed + "\0" + path)
        {
            hash ^= value;
            hash *= prime;
        }
        return hash;
    }

    private bool ReshuffleRandomSort()
    {
        _randomSortSeed = Guid.NewGuid().ToString("N");
        if (_sortBy != SortRandomValue)
            _sortBy = SortRandomValue;
        SyncSortButtons();
        if (!_initializing)
        {
            ApplyFilters();
            SaveState();
        }
        return true;
    }

    private static string NormalizeDatePreset(string? preset)
    {
        return preset?.Trim().ToLowerInvariant() switch
        {
            DatePresetManualValue or "range" => DatePresetManualValue,
            "clear" or "" or null => DatePresetNoneValue,
            _ => DatePresetNoneValue,
        };
    }

    private static bool TryGetLegacyDateRange(string? preset, out DateTime? from, out DateTime? to)
    {
        DateTime today = DateTime.Today;
        switch (preset?.Trim().ToLowerInvariant())
        {
            case "today":
                from = today;
                to = today;
                return true;
            case "7d":
            case "last7":
            case "last-7":
                from = today.AddDays(-6);
                to = today;
                return true;
            case "30d":
            case "last30":
            case "last-30":
                from = today.AddDays(-29);
                to = today;
                return true;
            case "this-year":
            case "year":
                from = new DateTime(today.Year, 1, 1);
                to = today;
                return true;
            default:
                from = null;
                to = null;
                return false;
        }
    }

    private static string? FormatStateDate(DateTime? date)
        => date?.ToString("yyyy-MM-dd");

    private static DateTime? ParseStateDate(string? value)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            return parsed.Date;

        return null;
    }

    private bool SetManualDateRange(DateTime? from, DateTime? to)
    {
        DateTime? normalizedFrom = from?.Date;
        DateTime? normalizedTo = to?.Date;
        string normalizedPreset = normalizedFrom.HasValue || normalizedTo.HasValue
            ? DatePresetManualValue
            : DatePresetNoneValue;

        bool changed = !string.Equals(_datePreset, normalizedPreset, StringComparison.Ordinal)
            || !SameDate(_dateFromLocal, normalizedFrom)
            || !SameDate(_dateToLocal, normalizedTo);

        _datePreset = normalizedPreset;
        _dateFromLocal = normalizedFrom;
        _dateToLocal = normalizedTo;
        SyncDateControls();
        UpdateDateFilterSummary();

        if (changed && !_initializing)
        {
            ApplyFilters();
            SaveState();
        }

        return changed;
    }

    private static bool SameDate(DateTime? left, DateTime? right)
        => left?.Date == right?.Date;

    private void RestoreDateFilter(ViewerState state)
    {
        DateTime? persistedFrom = ParseStateDate(state.DateFrom);
        DateTime? persistedTo = ParseStateDate(state.DateTo);
        if (TryGetLegacyDateRange(state.DatePreset, out DateTime? legacyFrom, out DateTime? legacyTo))
        {
            // Preserve an old saved range exactly. Only range-less legacy tokens are resolved once,
            // then immediately saved as a fixed manual range so they never move with the calendar again.
            bool hasSavedRangeValue = persistedFrom.HasValue || persistedTo.HasValue;
            _dateFromLocal = hasSavedRangeValue ? persistedFrom : legacyFrom;
            _dateToLocal = hasSavedRangeValue ? persistedTo : legacyTo;
            _datePreset = DatePresetManualValue;
            _dateFilterMigrationPending = true;
        }
        else
        {
            _dateFromLocal = persistedFrom;
            _dateToLocal = persistedTo;
            _datePreset = _dateFromLocal.HasValue || _dateToLocal.HasValue
                ? DatePresetManualValue
                : DatePresetNoneValue;
        }

        SyncDateControls();
        UpdateDateFilterSummary();
    }

    private void SyncDateControls()
    {
        if (DateFromInput is null || DateToInput is null)
        {
            return;
        }

        _syncingDateControls = true;
        try
        {
            DateFromInput.SelectedDate = _dateFromLocal;
            DateToInput.SelectedDate = _dateToLocal;
        }
        finally
        {
            _syncingDateControls = false;
        }
    }

    private void UpdateDateFilterSummary()
    {
        if (DateFilterSummary is null)
            return;

        if (!_dateFromLocal.HasValue && !_dateToLocal.HasValue)
        {
            DateFilterSummary.Text = "No date filter";
            return;
        }

        DateFilterSummary.Text = $"Manual: {FormatStateDate(_dateFromLocal) ?? "..."} – {FormatStateDate(_dateToLocal) ?? "..."}";
    }

    private static bool IsZoomModifierActive()
        => (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Windows)) != 0;

    private bool TryHandleZoomKey(KeyEventArgs e)
    {
        if (!IsZoomModifierActive())
            return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Add or Key.OemPlus)
            return AdjustCardWidth(1);
        if (key is Key.Subtract or Key.OemMinus)
            return AdjustCardWidth(-1);
        if (key is Key.D0 or Key.NumPad0)
            return ResetCardWidth();

        return false;
    }

    // ─────────── Panel toggles ───────────
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        bool show = Sidebar.Visibility != Visibility.Visible;
        Sidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SidebarCol.Width = show ? new GridLength(240) : new GridLength(0);
        ToggleSidebar.Style = (Style)FindResource(show ? "IconButtonActive" : "IconButton");
    }

    private void ToggleRight_Click(object sender, RoutedEventArgs e)
    {
        bool show = RightPanel.Visibility != Visibility.Visible;
        ApplyRightPanelState(show);
        SaveState();
    }

    private void ApplyRightPanelState(bool open)
    {
        if (open)
        {
            _rightPanelWidth = NormalizeRightPanelWidth(_rightPanelWidth);
            RightPanel.Visibility = Visibility.Visible;
            RightPanelSplitter.Visibility = Visibility.Visible;
            RightSplitterCol.Width = new GridLength(6);
            RightCol.MinWidth = MinRightPanelWidth;
            RightCol.MaxWidth = MaxRightPanelWidth;
            RightCol.Width = new GridLength(_rightPanelWidth);
            ToggleRight.Style = (Style)FindResource("IconButtonActive");
            ToggleRight.ToolTip = "Hide right panel";
        }
        else
        {
            if (RightCol.ActualWidth >= MinRightPanelWidth)
                _rightPanelWidth = NormalizeRightPanelWidth(RightCol.ActualWidth);
            RightPanel.Visibility = Visibility.Collapsed;
            RightPanelSplitter.Visibility = Visibility.Collapsed;
            RightSplitterCol.Width = new GridLength(0);
            RightCol.MinWidth = 0;
            RightCol.Width = new GridLength(0);
            ToggleRight.Style = (Style)FindResource("IconButton");
            ToggleRight.ToolTip = "Show right panel";
        }
    }

    private static double NormalizeRightPanelWidth(double width)
        => double.IsFinite(width) ? Math.Clamp(width, MinRightPanelWidth, MaxRightPanelWidth) : DefaultRightPanelWidth;

    private bool SetRightPanelWidth(double width)
    {
        double normalized = NormalizeRightPanelWidth(width);
        bool changed = Math.Abs(normalized - _rightPanelWidth) >= 0.5;
        _rightPanelWidth = normalized;
        if (RightPanel.Visibility == Visibility.Visible)
            RightCol.Width = new GridLength(_rightPanelWidth);
        return changed;
    }

    private void RightPanelSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (RightPanel.Visibility != Visibility.Visible)
            return;

        SetRightPanelWidth(RightCol.ActualWidth);
        SaveState();
    }

    // ─────────── Display mode (Grid / List) ───────────
    private void ModeGrid_Checked(object sender, RoutedEventArgs e)
    {
        if (CardsList is null || RowsList is null) return;
        CardsList.Visibility = Visibility.Visible;
        RowsList.Visibility = Visibility.Collapsed;
        SizeSlider.IsEnabled = true;
    }

    private void ModeList_Checked(object sender, RoutedEventArgs e)
    {
        if (CardsList is null || RowsList is null) return;
        if (RowsList.SelectedItem is null && CardsList.SelectedIndex >= 0)
            RowsList.SelectedIndex = CardsList.SelectedIndex;
        CardsList.Visibility = Visibility.Collapsed;
        RowsList.Visibility = Visibility.Visible;
        SizeSlider.IsEnabled = false;
    }

    // ─────────── Landing / scan ───────────
    private void StyleStandard_Checked(object sender, RoutedEventArgs e) => SetDisplayStyle(DisplayStyleStandard);
    private void StyleCompact_Checked(object sender, RoutedEventArgs e) => SetDisplayStyle(DisplayStyleCompact);
    private void StylePoster_Checked(object sender, RoutedEventArgs e) => SetDisplayStyle(DisplayStylePoster);
    private void AspectOriginal_Checked(object sender, RoutedEventArgs e) => SetAspectMode(AspectOriginalValue);
    private void AspectSquare_Checked(object sender, RoutedEventArgs e) => SetAspectMode(AspectSquareValue);
    private void AspectPortrait_Checked(object sender, RoutedEventArgs e) => SetAspectMode(AspectPortraitValue);
    private void SortModifiedNewest_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing)
            SetSortBy(SortModifiedNewestValue);
    }

    private void SortModifiedOldest_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing)
            SetSortBy(SortModifiedOldestValue);
    }

    private void SortName_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing)
            SetSortBy(SortNameValue);
    }

    private void SortCreatedNewest_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing)
            SetSortBy(SortCreatedNewestValue);
    }

    private void SortCreatedOldest_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing)
            SetSortBy(SortCreatedOldestValue);
    }

    private void SortRandom_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing)
            SetSortBy(SortRandomValue);
    }

    private void ReshuffleSort_Click(object sender, RoutedEventArgs e) => ReshuffleRandomSort();

    private void ManualDateRange_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _syncingDateControls)
            return;

        SetManualDateRange(DateFromInput.SelectedDate, DateToInput.SelectedDate);
    }

    private void Logo_Click(object sender, RoutedEventArgs e) => SetPhase(landing: true);

    private async void AddFolderToViewer_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> selected = ChooseFolders("Add image folders");
        if (selected.Count == 0)
            return;

        await AddFoldersToCurrentSetAsync(selected);
    }

    private void ChangeFolderSet_Click(object sender, RoutedEventArgs e)
        => ReturnToFolderSetEditor();

    private IReadOnlyList<string> ChooseFolders(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) != true)
            return [];

        string[] folders = dialog.FolderNames.Length > 0 ? dialog.FolderNames : [dialog.FolderName];
        return NormalizeFolderSet(folders).Where(Directory.Exists).ToList();
    }

    private async Task<bool> AddFoldersToCurrentSetAsync(IEnumerable<string> folders)
    {
        var merged = NormalizeFolderSet(_currentFolderSet.Concat(folders))
            .Where(Directory.Exists)
            .ToList();
        if (merged.Count == 0 || merged.SequenceEqual(_currentFolderSet, StringComparer.OrdinalIgnoreCase))
            return false;

        await LoadFolderSetAsync(merged);
        return true;
    }

    private void ReturnToFolderSetEditor()
    {
        SetLandingFolderSet(_currentFolderSet);
        SetPhase(landing: true);
    }

    private void SetPhase(bool landing)
    {
        ScanPanel.Visibility = Visibility.Collapsed;
        LandingPanel.IsEnabled = true;
        ScanBar.Value = 0;
        Landing.Visibility = landing ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void StartScan_Click(object sender, RoutedEventArgs e) => await OpenLandingFolderSetAsync();

    private void AddPastedFolders_Click(object sender, RoutedEventArgs e)
    {
        int added = AppendLandingFolders(SplitFolderSet(PastedFoldersInput.Text));
        if (added > 0)
            PastedFoldersInput.Clear();
        else if (LandingFolderStatusText is not null)
            LandingFolderStatusText.Text = "No usable pasted folders were added.";
    }

    private void RemoveLandingFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string folder })
            return;

        for (int i = _landingFolderSet.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_landingFolderSet[i], folder, StringComparison.OrdinalIgnoreCase))
                _landingFolderSet.RemoveAt(i);
        }

        RefreshLandingFolderSetUi();
    }

    private async void RecentFolderSet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: RecentFolderSetView recent })
            return;

        SetLandingFolderSet(recent.FolderSet);
        await LoadFolderSetAsync(recent.FolderSet);
    }

    private async void RefreshActiveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFolderSet.Any(Directory.Exists))
            await LoadFolderSetAsync(_currentFolderSet);
        else
            await ChooseAndLoadFolderAsync();
    }

    private async void OpenLastFolder_Click(object sender, RoutedEventArgs e)
    {
        var lastFolderSet = _lastFolderSet.Count > 0
            ? _lastFolderSet
            : ReadSharedRecentFolders().Recent.LastFolderSet;
        if (lastFolderSet.Count > 0)
        {
            SetLandingFolderSet(lastFolderSet);
            await LoadFolderSetAsync(lastFolderSet);
        }
        else
            await ChooseAndLoadFolderAsync();
    }

    // ─────────── Screen router (used by --shot and normal flow) ───────────
    public void ShowScreen(string screen)
    {
        switch (screen)
        {
            case "landing": SetPhase(landing: true); break;
            case "list": SetPhase(landing: false); ModeList.IsChecked = true; break;
            case "modal": SetPhase(landing: false); ShowModalForShot(); break;
            default: SetPhase(landing: false); break;
        }
    }

    // ─────────── Modal ───────────
    private void PreviewImage_Click(object sender, MouseButtonEventArgs e)
    {
        if (_suppressPreviewClickAfterFileDrag)
            return;
        OpenModal();
    }

    private void FileDragSurface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement surface || e.ChangedButton != MouseButton.Left)
            return;

        Tile? origin = ResolveFileDragOrigin(surface, e.OriginalSource as DependencyObject);
        if (origin is null || !origin.IsRealFile)
        {
            _fileDragSession = null;
            return;
        }

        _fileDragSession = new FileDragSession(surface, origin, e.GetPosition(surface), _selectedPaths.Contains(origin.Path));
    }

    private void FileDragSurface_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement surface
            || _fileDragSession is not FileDragSession session
            || !ReferenceEquals(session.Surface, surface)
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point current = e.GetPosition(surface);
        if (!ExceedsFileDragThreshold(session.Start, current))
            return;

        _fileDragSession = null;
        if (!TryBuildFileDropPayload(session.Origin, session.OriginWasSelected, out string[] paths, out string reason))
        {
            SetStatusToast($"Explorer drag was not started: {reason}.");
            return;
        }

        try
        {
            if (ReferenceEquals(surface, PreviewImageDragSurface))
                _suppressPreviewClickAfterFileDrag = true;
            var data = new DataObject(DataFormats.FileDrop, paths);
            DragDrop.DoDragDrop(surface, data, DragDropEffects.Copy);
        }
        catch (Exception ex)
        {
            SetStatusToast($"Explorer drag could not start: {ex.Message}");
        }
        finally
        {
            Dispatcher.BeginInvoke(() => _suppressPreviewClickAfterFileDrag = false, DispatcherPriority.Input);
        }
    }

    private void FileDragSurface_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement surface
            && _fileDragSession is FileDragSession session
            && ReferenceEquals(session.Surface, surface))
        {
            _fileDragSession = null;
        }
    }

    private Tile? ResolveFileDragOrigin(FrameworkElement surface, DependencyObject? originalSource)
    {
        if (ReferenceEquals(surface, PreviewImageDragSurface))
            return SelectedTile();

        for (DependencyObject? current = originalSource; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: Tile tile })
                return tile;
            if (ReferenceEquals(current, surface))
                break;
        }

        return null;
    }

    private static bool ExceedsFileDragThreshold(Point start, Point current)
        => Math.Abs(current.X - start.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(current.Y - start.Y) > SystemParameters.MinimumVerticalDragDistance;

    private bool TryBuildFileDropPayload(Tile origin, bool originWasSelected, out string[] paths, out string reason)
    {
        IReadOnlyList<Tile> candidates = originWasSelected
            ? _tiles.Where(tile => tile.IsRealFile && _selectedPaths.Contains(tile.Path)).ToList()
            : [origin];
        if (candidates.Count == 0)
        {
            paths = [];
            reason = "no real selected image";
            return false;
        }

        var canonicalPaths = new List<string>(candidates.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Tile tile in candidates)
        {
            if (!TryValidateFileDropTile(tile, out string canonical, out reason))
            {
                paths = [];
                return false;
            }
            if (seen.Add(canonical))
                canonicalPaths.Add(canonical);
        }

        if (canonicalPaths.Count == 0)
        {
            paths = [];
            reason = "no valid source image";
            return false;
        }

        paths = canonicalPaths.ToArray();
        reason = "";
        return true;
    }

    private bool TryValidateFileDropTile(Tile tile, out string canonical, out string reason)
    {
        canonical = "";
        if (!tile.IsRealFile || string.IsNullOrWhiteSpace(tile.Path))
            return Fail("not a source image", out reason);
        if (!_allTiles.Contains(tile))
            return Fail("not in the current catalog", out reason);
        if (!Path.IsPathFullyQualified(tile.Path))
            return Fail("path is not absolute", out reason);
        if (!SupportedImageExtensions.Contains(Path.GetExtension(tile.Path)))
            return Fail("unsupported file type", out reason);
        if (!File.Exists(tile.Path))
            return Fail("source no longer exists", out reason);

        string lexical;
        try
        {
            lexical = Path.GetFullPath(tile.Path);
            canonical = _resolveFinalPath(lexical);
            if (!File.Exists(canonical))
                return Fail("canonical source no longer exists", out reason);
        }
        catch (Exception ex)
        {
            return Fail($"canonical path failed ({ex.Message})", out reason);
        }

        List<string> activeRoots = _currentFolderSet.Count > 0 ? _currentFolderSet : _currentFolder is null ? [] : [_currentFolder];
        if (activeRoots.Count == 0)
            return Fail("no active source root", out reason);
        foreach (string root in activeRoots)
        {
            try
            {
                string lexicalRoot = Path.GetFullPath(root);
                string canonicalRoot = _resolveFinalPath(lexicalRoot);
                if (IsPathInside(lexical, lexicalRoot) && IsPathInside(canonical, canonicalRoot))
                {
                    reason = "";
                    return true;
                }
            }
            catch
            {
                // A broken root cannot authorize an Explorer FileDrop payload.
            }
        }

        return Fail("source is outside the active root", out reason);
    }

    private void OpenModal()
    {
        if (SelectedTile() is not Tile t) return;
        bool opening = Modal.Visibility != Visibility.Visible;
        if (!string.Equals(_modalTransformPath, t.Path, StringComparison.OrdinalIgnoreCase))
            ResetModalTransform(t.Path);
        if (!string.Equals(_modalSourceTilePath, t.Path, StringComparison.OrdinalIgnoreCase))
        {
            _modalSourceTilePath = t.Path;
            _modalShowingEnhanced = false;
        }
        var watch = Stopwatch.StartNew();
        _modalCts?.Cancel();
        _modalDecodeCompletion?.TrySetCanceled();
        var cts = new CancellationTokenSource();
        _modalCts = cts;
        var decodeCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _modalDecodeCompletion = decodeCompletion;

        bool canShowEnhanced = TryGetModalEnhancedOutput(t, out string? enhancedPath);
        if (!canShowEnhanced)
            _modalShowingEnhanced = false;
        string displayPath = _modalShowingEnhanced && enhancedPath is not null ? enhancedPath : t.Path;
        _modalDisplayPath = displayPath;
        UpdateModalEnhancedControls(canShowEnhanced);

        var immediate = _modalShowingEnhanced ? null : PreviewBitmap.Source as BitmapSource ?? t.Thumbnail;
        ModalBitmap.Source = immediate;
        ModalBitmap.Visibility = immediate is null ? Visibility.Collapsed : Visibility.Visible;
        ModalArtBase.Visibility = immediate is null ? Visibility.Visible : Visibility.Collapsed;
        ModalArtGlow.Visibility = immediate is null ? Visibility.Visible : Visibility.Collapsed;
        ModalArtBase.Fill = t.ArtBase;
        ModalArtGlow.Fill = t.ArtGlow;
        SetModalMetadataSidebarVisible(false);
        Modal.Visibility = Visibility.Visible;
        Modal.UpdateLayout();
        UpdateModalFit();
        ScheduleModalFitUpdate();
        if (opening)
            SetModalChromeVisible(true, showFeedback: false);
        SyncModalMetadataSidebar();
        watch.Stop();

        if (LastLoadMetrics is not null)
        {
            LastLoadMetrics.ModalOpenMs = watch.ElapsedMilliseconds;
            LastLoadMetrics.ModalImmediateSource = immediate is not null;
            LastLoadMetrics.ModalDeferredDecode = t.IsRealFile;
        }

        if (t.IsRealFile && File.Exists(displayPath))
            _ = LoadModalBitmapAsync(displayPath, t.Path, cts.Token, decodeCompletion);
        else
            decodeCompletion.TrySetResult(immediate is not null);
    }

    private bool TryGetModalEnhancedOutput(Tile tile, out string? outputPath)
    {
        outputPath = tile.EnhancedOutputPath;
        return tile.IsRealFile
            && tile.Enhanced
            && !string.IsNullOrWhiteSpace(outputPath)
            && File.Exists(outputPath);
    }

    private void UpdateModalEnhancedControls(bool canShowEnhanced)
    {
        if (ModalEnhancedToggleButton is not null)
        {
            ModalEnhancedToggleButton.IsEnabled = canShowEnhanced;
            ModalEnhancedToggleButton.ToolTip = canShowEnhanced
                ? _modalShowingEnhanced ? "Show original (E)" : "Show enhanced output (E)"
                : "No enhanced output available";
        }

        if (ModalEnhancedToggleLabel is not null)
            ModalEnhancedToggleLabel.Text = _modalShowingEnhanced ? "UP" : "OR";
        if (ModalSourceLabel is not null)
            ModalSourceLabel.Text = _modalShowingEnhanced ? "Enhanced output" : "Original";
    }

    private async Task LoadModalBitmapAsync(string displayPath, string selectedPath, CancellationToken token, TaskCompletionSource<bool> completion)
    {
        BitmapSource? bitmap;
        try
        {
            bitmap = await Task.Run(() => LoadBitmap(displayPath, 1400), token);
        }
        catch (OperationCanceledException)
        {
            completion.TrySetCanceled(token);
            return;
        }
        catch
        {
            completion.TrySetResult(false);
            return;
        }

        if (token.IsCancellationRequested || bitmap is null)
        {
            completion.TrySetCanceled(token);
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    if (token.IsCancellationRequested || Modal.Visibility != Visibility.Visible)
                        return;
                    if (SelectedTile()?.Path != selectedPath)
                        return;
                    if (!string.Equals(_modalDisplayPath, displayPath, StringComparison.OrdinalIgnoreCase))
                        return;

                    ModalBitmap.Source = bitmap;
                    ModalBitmap.Visibility = Visibility.Visible;
                    ModalArtBase.Visibility = Visibility.Collapsed;
                    ModalArtGlow.Visibility = Visibility.Collapsed;
                    completion.TrySetResult(true);
                },
                DispatcherPriority.Background,
                token);
        }
        catch (OperationCanceledException)
        {
            completion.TrySetCanceled(token);
        }
    }

    private void CloseModal_Click(object sender, RoutedEventArgs e) => CloseModal();

    private void ToggleModalMetadataSidebar_Click(object sender, RoutedEventArgs e)
        => SetModalMetadataSidebarVisible(ModalMetadataSidebar.Visibility != Visibility.Visible);

    private void ModalMetadataTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tab })
            SetModalMetadataTab(tab);
    }

    private void SetModalMetadataTab(string tab)
    {
        string activeTab = tab.ToLowerInvariant() switch
        {
            ModalMetadataNegativeTab => ModalMetadataNegativeTab,
            ModalMetadataSettingsTab => ModalMetadataSettingsTab,
            _ => ModalMetadataPromptTab,
        };

        ModalPromptTabButton.IsChecked = activeTab == ModalMetadataPromptTab;
        ModalNegativeTabButton.IsChecked = activeTab == ModalMetadataNegativeTab;
        ModalSettingsTabButton.IsChecked = activeTab == ModalMetadataSettingsTab;
        ModalPromptPanel.Visibility = activeTab == ModalMetadataPromptTab ? Visibility.Visible : Visibility.Collapsed;
        ModalNegativePanel.Visibility = activeTab == ModalMetadataNegativeTab ? Visibility.Visible : Visibility.Collapsed;
        ModalSettingsPanel.Visibility = activeTab == ModalMetadataSettingsTab ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetModalMetadataSidebarVisible(bool visible)
    {
        ModalMetadataSidebar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ModalMetadataSidebarToggleButton.ToolTip = visible ? "Hide metadata sidebar" : "Show metadata sidebar";
        ModalMetadataSidebarToggleLabel.Text = visible ? ">" : "<";
        ScheduleModalFitUpdate();
    }

    private void CloseModal()
    {
        CancelPendingModalSingleClick();
        EndModalPointerGesture();
        _modalCts?.Cancel();
        Modal.Visibility = Visibility.Collapsed;
        _modalShowingEnhanced = false;
        _modalSourceTilePath = null;
        _modalDisplayPath = null;
        UpdateModalEnhancedControls(false);
        SetModalChromeVisible(true, showFeedback: false);
        _modalFeedbackTimer.Stop();
        ModalInteractionFeedback.Visibility = Visibility.Collapsed;
        ResetModalTransform();
    }

    private void ToggleModalEnhanced_Click(object sender, RoutedEventArgs e) => ToggleModalEnhanced();

    private bool ToggleModalEnhanced()
    {
        if (Modal.Visibility != Visibility.Visible || SelectedTile() is not Tile tile || !TryGetModalEnhancedOutput(tile, out _))
            return false;

        _modalShowingEnhanced = !_modalShowingEnhanced;
        OpenModal();
        return true;
    }

    private void ToggleModalFlip_Click(object sender, RoutedEventArgs e) => ToggleModalFlip();

    private void ModalOneToOne_Click(object sender, RoutedEventArgs e)
    {
        if (_modalFitScale > 0)
            SetModalZoom(1 / _modalFitScale);
    }

    private bool ToggleModalFlip()
    {
        if (Modal.Visibility != Visibility.Visible)
            return false;

        _modalFlipped = !_modalFlipped;
        UpdateModalTransform();
        return true;
    }

    private bool AdjustModalZoom(double multiplier)
    {
        if (Modal.Visibility != Visibility.Visible || multiplier <= 0)
            return false;

        return SetModalZoom(_modalZoom * multiplier);
    }

    private bool SetModalZoom(double zoom)
    {
        double next = Math.Clamp(zoom, ModalZoomMin, ModalZoomMax);
        if (Math.Abs(next - _modalZoom) < 0.0001)
            return false;

        _modalZoom = next;
        ClampModalPan();
        UpdateModalTransform();
        ShowModalInteractionFeedback($"Zoom {Math.Round(_modalZoom * 100):0}%");
        return true;
    }

    private bool ResetModalTransform(string? path = null, bool showFeedback = false)
    {
        bool changed = Math.Abs(_modalZoom - 1) >= 0.0001
            || _modalFlipped
            || !string.Equals(_modalTransformPath, path, StringComparison.OrdinalIgnoreCase);
        EndModalPan();
        _modalZoom = 1;
        _modalFlipped = false;
        _modalPanX = 0;
        _modalPanY = 0;
        _modalTransformPath = path;
        UpdateModalTransform();
        ScheduleModalFitUpdate();
        if (showFeedback && changed)
            ShowModalInteractionFeedback("Zoom reset");
        return changed;
    }

    private bool SetModalPan(double x, double y)
    {
        if (Modal.Visibility != Visibility.Visible || _modalZoom <= 1)
            return false;

        (double maxX, double maxY) = ModalPanLimits();
        double nextX = Math.Clamp(x, -maxX, maxX);
        double nextY = Math.Clamp(y, -maxY, maxY);
        if (Math.Abs(nextX - _modalPanX) < 0.0001 && Math.Abs(nextY - _modalPanY) < 0.0001)
            return false;

        _modalPanX = nextX;
        _modalPanY = nextY;
        UpdateModalTransform();
        return true;
    }

    private (double MaxX, double MaxY) ModalPanLimits()
    {
        double width = ModalImage?.ActualWidth > 0 ? ModalImage.ActualWidth : 440;
        double height = ModalImage?.ActualHeight > 0 ? ModalImage.ActualHeight : 640;
        return (Math.Max(0, width * (_modalZoom - 1) / 2), Math.Max(0, height * (_modalZoom - 1) / 2));
    }

    private void ClampModalPan()
    {
        if (_modalZoom <= 1)
        {
            _modalPanX = 0;
            _modalPanY = 0;
            return;
        }

        (double maxX, double maxY) = ModalPanLimits();
        _modalPanX = Math.Clamp(_modalPanX, -maxX, maxX);
        _modalPanY = Math.Clamp(_modalPanY, -maxY, maxY);
    }

    private void UpdateModalTransform()
    {
        if (ModalVisualTransform is not null)
        {
            ModalVisualTransform.ScaleX = _modalFlipped ? -_modalZoom : _modalZoom;
            ModalVisualTransform.ScaleY = _modalZoom;
        }

        if (ModalPanTransform is not null)
        {
            ModalPanTransform.X = _modalPanX;
            ModalPanTransform.Y = _modalPanY;
        }

        if (ModalZoomLabel is not null)
            ModalZoomLabel.Text = $"{Math.Round(_modalFitScale * _modalZoom * 100):0}%";
    }

    private void ScheduleModalFitUpdate()
    {
        if (_modalFitUpdateQueued || Modal.Visibility != Visibility.Visible)
            return;
        _modalFitUpdateQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _modalFitUpdateQueued = false;
            UpdateModalFit();
        }, DispatcherPriority.Loaded);
    }

    private void UpdateModalFit()
    {
        if (Modal.Visibility != Visibility.Visible || SelectedTile() is not Tile tile)
            return;
        double sourceWidth = tile.ImagePixelWidth > 0 ? tile.ImagePixelWidth : ModalBitmap.Source?.Width ?? 440;
        double sourceHeight = tile.ImagePixelHeight > 0 ? tile.ImagePixelHeight : ModalBitmap.Source?.Height ?? 640;
        double oldEffectiveScale = _modalFitScale * _modalZoom;
        bool preserveUserZoom = Math.Abs(_modalZoom - 1) > 0.0001;
        double metadataWidth = ModalMetadataSidebar.Visibility == Visibility.Visible ? ModalMetadataSidebar.Width + ModalMetadataSidebar.Margin.Right + 84 : 0;
        double availableWidth = Math.Max(240, Modal.ActualWidth - 144 - metadataWidth - 32);
        double availableHeight = Math.Max(240, Modal.ActualHeight - ModalTopBar.ActualHeight - 64);
        _modalFitScale = Math.Min(1, Math.Min(availableWidth / sourceWidth, availableHeight / sourceHeight));
        ModalImage.Width = Math.Max(1, sourceWidth * _modalFitScale);
        ModalImage.Height = Math.Max(1, sourceHeight * _modalFitScale);
        ModalMetadataSidebar.MaxHeight = availableHeight;
        if (preserveUserZoom)
            _modalZoom = Math.Clamp(oldEffectiveScale / _modalFitScale, ModalZoomMin, ModalZoomMax);
        ClampModalPan();
        UpdateModalTransform();
    }

    private bool TryHandleModalTransformKey(KeyEventArgs e)
    {
        if (Modal.Visibility != Visibility.Visible)
            return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key switch
        {
            Key.H => ToggleModalFlip(),
            Key.E => ToggleModalEnhanced(),
            Key.Add or Key.OemPlus => AdjustModalZoom(ModalZoomKeyboardStep),
            Key.Subtract or Key.OemMinus => AdjustModalZoom(1 / ModalZoomKeyboardStep),
            Key.D0 or Key.NumPad0 => ResetModalTransform(_modalTransformPath, showFeedback: true),
            _ => false,
        };
    }

    private void ModalImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CancelPendingModalSingleClick();
        if (e.ClickCount == 2 && ToggleModalMetadataSidebarFromImageDoubleClick())
        {
            EndModalPointerGesture();
            e.Handled = true;
            return;
        }

        if (Modal.Visibility != Visibility.Visible)
            return;

        Point start = e.GetPosition(ModalImage);
        _modalPointerStartPoint = start;
        _modalPointerMoved = false;
        if (_modalZoom > 1)
        {
            _modalPanStartPoint = start;
            _modalPanStartOffset = new Vector(_modalPanX, _modalPanY);
        }
        ModalImage.CaptureMouse();
        e.Handled = true;
    }

    private bool ToggleModalMetadataSidebarFromImageDoubleClick()
    {
        if (Modal.Visibility != Visibility.Visible)
            return false;

        SetModalMetadataSidebarVisible(ModalMetadataSidebar.Visibility != Visibility.Visible);
        return true;
    }

    private void ModalImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_modalPointerStartPoint.HasValue || !ModalImage.IsMouseCaptured)
            return;

        Point current = e.GetPosition(ModalImage);
        Vector delta = current - _modalPointerStartPoint.Value;
        if (Math.Abs(delta.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(delta.Y) >= SystemParameters.MinimumVerticalDragDistance)
        {
            _modalPointerMoved = true;
        }

        if (_modalZoom > 1 && _modalPanStartPoint.HasValue)
        {
            Vector panDelta = current - _modalPanStartPoint.Value;
            SetModalPan(_modalPanStartOffset.X + panDelta.X, _modalPanStartOffset.Y + panDelta.Y);
        }
        e.Handled = true;
    }

    private void ModalImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Point end = e.GetPosition(ModalImage);
        Point? start = _modalPointerStartPoint;
        bool zoomed = _modalZoom > 1;
        bool moved = _modalPointerMoved;
        EndModalPointerGesture();
        if (!zoomed && start.HasValue)
        {
            Vector delta = end - start.Value;
            if (!TryNavigateModalSwipe(delta) && !moved)
                ScheduleModalChromeToggle();
        }
        e.Handled = true;
    }

    private void ModalImage_LostMouseCapture(object sender, MouseEventArgs e) => EndModalPointerGesture();

    private void EndModalPan()
    {
        _modalPanStartPoint = null;
        if (ModalImage.IsMouseCaptured)
            ModalImage.ReleaseMouseCapture();
    }

    private void EndModalPointerGesture()
    {
        _modalPointerStartPoint = null;
        _modalPointerMoved = false;
        EndModalPan();
    }

    private bool TryNavigateModalSwipe(Vector delta)
    {
        if (Modal.Visibility != Visibility.Visible || _modalZoom > 1 || Math.Abs(delta.X) <= Math.Abs(delta.Y))
            return false;

        double threshold = Math.Clamp(ModalImage.ActualWidth * 0.16, 72, 180);
        if (Math.Abs(delta.X) < threshold)
            return false;

        return NavigateModal(delta.X < 0 ? 1 : -1);
    }

    private void ScheduleModalChromeToggle()
    {
        if (Modal.Visibility != Visibility.Visible || _modalZoom > 1)
            return;

        long generation = ++_modalSingleClickGeneration;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(180);
            if (generation == _modalSingleClickGeneration
                && Modal.Visibility == Visibility.Visible
                && _modalZoom <= 1)
            {
                SetModalChromeVisible(!_modalChromeVisible, showFeedback: true);
            }
        }, DispatcherPriority.Background);
    }

    private void CancelPendingModalSingleClick() => _modalSingleClickGeneration++;

    private void SetModalChromeVisible(bool visible, bool showFeedback)
    {
        _modalChromeVisible = visible;
        Visibility visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ModalTopBar.Visibility = visibility;
        ModalFooter.Visibility = visibility;
        ModalPreviousButton.Visibility = visibility;
        ModalNextButton.Visibility = visibility;
        if (showFeedback)
            ShowModalInteractionFeedback(visible ? "Controls shown" : "Controls hidden");
    }

    private void ShowModalInteractionFeedback(string message)
    {
        if (Modal.Visibility != Visibility.Visible || string.IsNullOrWhiteSpace(message))
            return;

        ModalInteractionFeedbackText.Text = message;
        ModalInteractionFeedback.Visibility = Visibility.Visible;
        _modalFeedbackTimer.Stop();
        _modalFeedbackTimer.Start();
    }

    private void ModalPrevious_Click(object sender, RoutedEventArgs e)
    {
        NavigateModal(-1);
    }

    private void ModalNext_Click(object sender, RoutedEventArgs e)
    {
        NavigateModal(1);
    }

    private bool NavigateModal(int delta)
    {
        if (delta == 0 || _tiles.Count == 0)
            return false;

        var selected = SelectedTile();
        int currentIndex = selected is null ? -1 : _tiles.IndexOf(selected);
        int nextIndex;
        if (currentIndex < 0)
        {
            nextIndex = delta > 0 ? 0 : _tiles.Count - 1;
        }
        else
        {
            nextIndex = ((currentIndex + delta) % _tiles.Count + _tiles.Count) % _tiles.Count;
        }
        if (nextIndex == currentIndex)
            return false;

        SelectTile(_tiles[nextIndex]);
        SaveState();

        if (Modal.Visibility == Visibility.Visible)
        {
            OpenModal();
            ShowModalInteractionFeedback(delta > 0 ? "Next image" : "Previous image");
        }

        return true;
    }

    /// <summary>Used only by the --shot --modal smoke path to capture the modal state.</summary>
    public void ShowModalForShot()
    {
        if (CardsList.SelectedItem is null && CardsList.Items.Count > 0)
            CardsList.SelectedIndex = 0;
        OpenModal();
    }

    private Tile? SelectedTile()
    {
        if (!string.IsNullOrWhiteSpace(_primarySelectedPath))
        {
            var primary = _tiles.FirstOrDefault(tile => string.Equals(tile.Path, _primarySelectedPath, StringComparison.OrdinalIgnoreCase));
            if (primary is not null)
                return primary;
        }

        return CardsList.SelectedItem as Tile ?? RowsList.SelectedItem as Tile;
    }

    private void OpenSelectedExternally_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTile() is not { IsRealFile: true } tile || !File.Exists(tile.Path))
            return;

        Process.Start(new ProcessStartInfo(tile.Path) { UseShellExecute = true });
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e) => RequestDeleteSelected();

    private void BulkDeleteSelected_Click(object sender, RoutedEventArgs e) => RequestBulkDeleteSelected();

    private void OpenAppSettings_Click(object sender, RoutedEventArgs e)
    {
        _settingsFocusBeforeDialog = Keyboard.FocusedElement;
        ConfirmBeforeDeleteCheckBox.IsChecked = _confirmBeforeDelete;
        AppSettingsDialog.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(ConfirmBeforeDeleteCheckBox.Focus, DispatcherPriority.Input);
    }

    private void CloseAppSettings_Click(object sender, RoutedEventArgs e)
    {
        AppSettingsDialog.Visibility = Visibility.Collapsed;
        RestoreOverlayFocus(_settingsFocusBeforeDialog);
    }

    private void ConfirmBeforeDelete_Changed(object sender, RoutedEventArgs e)
    {
        _confirmBeforeDelete = ConfirmBeforeDeleteCheckBox.IsChecked == true;
        SaveState();
    }

    private bool RequestDeleteSelected()
    {
        if (SelectedTile() is not Tile tile)
        {
            SetDeleteStatus("Select an image to move to Recycle Bin.");
            return false;
        }

        if (!TryValidateDelete(tile, out string reason))
        {
            SetDeleteStatus($"Move to Recycle Bin blocked: {reason}");
            return false;
        }

        if (_confirmBeforeDelete)
        {
            _pendingDeleteTile = tile;
            _pendingBulkDeleteSnapshot = null;
            ShowDeleteConfirmation($"{tile.FileName}\nThe source will be moved to the Windows Recycle Bin.", "Move selected image to Recycle Bin");
            return true;
        }

        return ExecuteDelete(tile);
    }

    private bool RequestBulkDeleteSelected()
    {
        List<Tile> selected = SelectedTiles().Where(static tile => tile.IsRealFile).ToList();
        if (selected.Count <= 1)
            return RequestDeleteSelected();

        var snapshot = new DeleteSnapshot(selected, _tiles.ToList());
        if (_confirmBeforeDelete)
        {
            _pendingDeleteTile = null;
            _pendingBulkDeleteSnapshot = snapshot;
            ShowDeleteConfirmation(
                $"{selected.Count:N0} selected images\nEach source will be moved independently to the Windows Recycle Bin. Failed images will remain available.",
                $"Move {selected.Count:N0} selected images to Recycle Bin");
            return true;
        }

        return ExecuteBulkDelete(snapshot);
    }

    private void ShowDeleteConfirmation(string message, string confirmAutomationName)
    {
        _deleteFocusBeforeDialog = Keyboard.FocusedElement;
        DeleteConfirmationText.Text = message;
        System.Windows.Automation.AutomationProperties.SetName(DeleteConfirmButton, confirmAutomationName);
        DoNotAskAgainCheckBox.IsChecked = false;
        DeleteConfirmationDialog.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(DeleteCancelButton.Focus, DispatcherPriority.Input);
    }

    private void DeleteCancel_Click(object sender, RoutedEventArgs e)
    {
        _pendingDeleteTile = null;
        _pendingBulkDeleteSnapshot = null;
        DeleteConfirmationDialog.Visibility = Visibility.Collapsed;
        SetDeleteStatus("Move to Recycle Bin cancelled.");
        RestoreOverlayFocus(_deleteFocusBeforeDialog);
    }

    private void DeleteConfirm_Click(object sender, RoutedEventArgs e)
    {
        Tile? tile = _pendingDeleteTile;
        DeleteSnapshot? bulkSnapshot = _pendingBulkDeleteSnapshot;
        _pendingDeleteTile = null;
        _pendingBulkDeleteSnapshot = null;
        DeleteConfirmationDialog.Visibility = Visibility.Collapsed;
        if (DoNotAskAgainCheckBox.IsChecked == true)
        {
            _confirmBeforeDelete = false;
            SaveState();
        }
        if (bulkSnapshot is not null)
            ExecuteBulkDelete(bulkSnapshot);
        else if (tile is not null)
            ExecuteDelete(tile);
        RestoreOverlayFocus(_deleteFocusBeforeDialog);
    }

    private bool ExecuteDelete(Tile tile)
    {
        // Revalidate immediately before the only destructive boundary.
        if (!TryValidateDelete(tile, out string reason))
        {
            SetDeleteStatus($"Move to Recycle Bin blocked: {reason}");
            return false;
        }

        List<Tile> priorFilteredOrder = _tiles.ToList();
        int oldIndex = priorFilteredOrder.IndexOf(tile);
        RecycleBinDeleteResult result = _recycleBinDelete(tile.Path);
        if (!result.Succeeded)
        {
            SetDeleteStatus($"Move to Recycle Bin failed: {result.Reason}. Retry is available.", isFailure: true, retryTile: tile);
            return false;
        }

        _allTiles.Remove(tile);
        _selectedPaths.Remove(tile.Path);
        _primarySelectedPath = null;
        ApplyFilters(selectFirst: false);

        Tile? neighbor = priorFilteredOrder
            .Skip(Math.Max(0, oldIndex + 1))
            .FirstOrDefault(_tiles.Contains)
            ?? priorFilteredOrder
                .Take(Math.Max(0, oldIndex))
                .Reverse()
                .FirstOrDefault(_tiles.Contains);
        if (neighbor is not null)
            SelectTile(neighbor);
        else
        {
            SelectTile(null);
            CloseModal();
        }

        SetDeleteStatus($"Moved {tile.FileName} to Recycle Bin.");
        SaveState();
        return true;
    }

    private bool ExecuteBulkDelete(DeleteSnapshot snapshot)
    {
        var succeeded = new List<Tile>();
        var failed = new List<(Tile Tile, string Reason)>();
        foreach (Tile tile in snapshot.Targets)
        {
            if (!TryValidateDelete(tile, out string reason))
            {
                failed.Add((tile, reason));
                continue;
            }

            RecycleBinDeleteResult result = _recycleBinDelete(tile.Path);
            if (result.Succeeded)
                succeeded.Add(tile);
            else
                failed.Add((tile, string.IsNullOrWhiteSpace(result.Reason) ? "Recycle Bin rejected the source" : result.Reason));
        }

        if (succeeded.Count == 0)
        {
            string reason = failed.FirstOrDefault().Reason ?? "No source could be moved";
            SetStatusToast($"No selected images were moved to Recycle Bin. {failed.Count:N0} failed; they remain selected. {reason}");
            return false;
        }

        RemoveDeletedTilesFromState(succeeded);
        ApplyFilters(selectFirst: false);

        var remainingSelected = snapshot.Targets
            .Where(tile => !succeeded.Contains(tile) && _tiles.Contains(tile))
            .ToList();
        Tile? neighbor = FindBulkDeleteNeighbor(snapshot.FilteredOrder, snapshot.Targets);
        if (neighbor is not null)
        {
            if (!remainingSelected.Contains(neighbor))
                remainingSelected.Add(neighbor);
            SetSelection(remainingSelected, neighbor);
        }
        else if (remainingSelected.Count > 0)
        {
            SetSelection(remainingSelected, remainingSelected[^1]);
        }
        else
        {
            SelectTile(null);
            CloseModal();
        }

        bool favoritesSaved = SaveFavorites();
        bool seenSaved = SaveSeenState();
        SaveState();
        if (failed.Count == 0 && favoritesSaved && seenSaved)
        {
            SetStatusToast($"Moved {succeeded.Count:N0} selected images to Recycle Bin.");
        }
        else
        {
            string persistence = favoritesSaved && seenSaved ? "" : " Local metadata could not be saved; retry after fixing local access.";
            string firstReason = failed.Count > 0 ? $" {failed[0].Reason}" : "";
            SetStatusToast($"Moved {succeeded.Count:N0} image(s); {failed.Count:N0} failed and remain selected.{firstReason}{persistence}");
        }
        return true;
    }

    private void RemoveDeletedTilesFromState(IEnumerable<Tile> deletedTiles)
    {
        var deletedPaths = new HashSet<string>(deletedTiles.Select(static tile => tile.Path), StringComparer.OrdinalIgnoreCase);
        foreach (Tile tile in deletedTiles)
        {
            string key = NormalizeFavoritePath(tile.Path);
            _allTiles.Remove(tile);
            _selectedPaths.Remove(tile.Path);
            _favorites.Remove(key);
            _favoriteDirtyPaths.Add(key);
            _seenPaths.Remove(key);
            _seenDirtyPaths.Add(key);
            _pinnedPreviewPaths.Remove(key);
        }

        foreach (PreviewTabView tab in _previewTabs.Where(tab => deletedPaths.Contains(tab.Path)).ToList())
            _previewTabs.Remove(tab);
        _closedPreviewTabs.RemoveAll(tile => deletedPaths.Contains(tile.Path));
        if (_activePreviewTabPath is not null && deletedPaths.Contains(_activePreviewTabPath))
            _activePreviewTabPath = null;
        if (_hoverPreviewTabPath is not null && deletedPaths.Contains(_hoverPreviewTabPath))
            HidePreviewTabHover();
        _primarySelectedPath = null;
        RefreshPreviewTabs();
    }

    private Tile? FindBulkDeleteNeighbor(IReadOnlyList<Tile> priorFilteredOrder, IReadOnlyList<Tile> targets)
    {
        int lastDeletedIndex = Enumerable.Range(0, priorFilteredOrder.Count)
            .Where(index => targets.Contains(priorFilteredOrder[index]))
            .DefaultIfEmpty(-1)
            .Max();
        if (lastDeletedIndex >= 0)
        {
            Tile? next = priorFilteredOrder.Skip(lastDeletedIndex + 1).FirstOrDefault(_tiles.Contains);
            if (next is not null)
                return next;
            return priorFilteredOrder.Take(lastDeletedIndex).Reverse().FirstOrDefault(_tiles.Contains);
        }

        return _tiles.FirstOrDefault();
    }

    private sealed record DeleteSnapshot(IReadOnlyList<Tile> Targets, IReadOnlyList<Tile> FilteredOrder);

    private bool TryValidateDelete(Tile tile, out string reason)
    {
        reason = "";
        if (!tile.IsRealFile || string.IsNullOrWhiteSpace(tile.Path))
            return Fail("not a source image", out reason);
        if (!_allTiles.Contains(tile))
            return Fail("not in the current catalog", out reason);
        if (!_tiles.Contains(tile))
            return Fail("not in the current filtered order", out reason);
        if (!Path.IsPathFullyQualified(tile.Path))
            return Fail("path is not absolute", out reason);
        if (!SupportedImageExtensions.Contains(Path.GetExtension(tile.Path)))
            return Fail("unsupported file type", out reason);
        if (!File.Exists(tile.Path))
            return Fail("source no longer exists", out reason);

        string lexical;
        string canonical;
        try
        {
            lexical = Path.GetFullPath(tile.Path);
            canonical = _resolveFinalPath(lexical);
        }
        catch (Exception ex)
        {
            return Fail($"canonical path failed ({ex.Message})", out reason);
        }

        var activeRoots = _currentFolderSet.Count > 0 ? _currentFolderSet : _currentFolder is null ? [] : [_currentFolder];
        if (activeRoots.Count == 0)
            return Fail("no active source root", out reason);
        foreach (string root in activeRoots)
        {
            try
            {
                string lexicalRoot = Path.GetFullPath(root);
                string canonicalRoot = _resolveFinalPath(lexicalRoot);
                if (IsPathInside(lexical, lexicalRoot) && IsPathInside(canonical, canonicalRoot))
                    return true;
            }
            catch
            {
                // A broken root cannot authorize a delete.
            }
        }

        return Fail("source is outside the active root", out reason);
    }

    private static bool Fail(string value, out string reason)
    {
        reason = value;
        return false;
    }

    private static bool IsPathInside(string candidate, string root)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string prefix = normalizedRoot + Path.DirectorySeparatorChar;
        return string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFinalPathCore(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ?? throw new IOException("path root is unavailable");
        string current = root;
        string tail = fullPath[root.Length..];
        foreach (string part in tail.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(current, part);
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return Path.GetFullPath(candidate);

            FileSystemInfo info = Directory.Exists(candidate) ? new DirectoryInfo(candidate) : new FileInfo(candidate);
            if (info.LinkTarget is null)
            {
                current = candidate;
                continue;
            }

            FileSystemInfo? resolved = info.ResolveLinkTarget(returnFinalTarget: true);
            if (resolved is null)
                throw new IOException($"cannot resolve link target: {candidate}");
            current = Path.GetFullPath(resolved.FullName);
        }

        return Path.GetFullPath(current);
    }

    private static RecycleBinDeleteResult SendFileToWindowsRecycleBin(string path)
    {
        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                path,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return RecycleBinDeleteResult.Success;
        }
        catch (Exception ex)
        {
            return RecycleBinDeleteResult.Failed(ex.Message);
        }
    }

    private void SetStatusToast(string status, Action? retryAction = null)
    {
        _deleteStatus = status;
        _statusRetryAction = retryAction;
        if (ScanMessage is not null)
            ScanMessage.Text = status;
        if (DeleteStatusText is not null)
        {
            DeleteStatusText.Text = status;
            DeleteStatusRetryButton.Visibility = retryAction is null ? Visibility.Collapsed : Visibility.Visible;
            DeleteStatusToast.Visibility = Visibility.Visible;
        }
    }

    private void SetDeleteStatus(string status, bool isFailure = false, Tile? retryTile = null)
        => SetStatusToast(status, isFailure && retryTile is not null
            ? () => RetryDelete(retryTile)
            : null);

    private void DismissDeleteStatus_Click(object sender, RoutedEventArgs e)
        => DeleteStatusToast.Visibility = Visibility.Collapsed;

    private void RetryDelete_Click(object sender, RoutedEventArgs e)
    {
        Action? retryAction = _statusRetryAction;
        _statusRetryAction = null;
        DeleteStatusRetryButton.Visibility = Visibility.Collapsed;
        if (retryAction is null)
        {
            SetDeleteStatus("Retry is no longer available.");
            return;
        }

        retryAction();
    }

    private void RetryDelete(Tile tile)
    {
        if (!_tiles.Contains(tile))
        {
            SetDeleteStatus("Retry is no longer available.");
            return;
        }

        SelectTile(tile);
        RequestDeleteSelected();
    }

    private void RestoreOverlayFocus(IInputElement? target)
        => Dispatcher.BeginInvoke(() =>
        {
            if (target is UIElement element && element.IsVisible && element.IsEnabled && element.Focus())
                return;
            CardsList.Focus();
        }, DispatcherPriority.Input);

    private static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhotoViewer.Wpf",
        "state.json");

    private static string ResolvedStatePath
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_STATE_PATH");
            return string.IsNullOrWhiteSpace(overridePath) ? StatePath : Path.GetFullPath(overridePath);
        }
    }

    private void RestoreState()
    {
        var state = ReadState();
        var lastFolderSet = NormalizeFolderSet(state?.LastFolderSet ?? []);
        if (lastFolderSet.Count == 0 && !string.IsNullOrWhiteSpace(state?.LastFolder))
            lastFolderSet = NormalizeFolderSet([state.LastFolder]);
        if (lastFolderSet.Count == 0)
            lastFolderSet = ReadSharedRecentFolders().Recent.LastFolderSet;

        if (lastFolderSet.Count > 0)
        {
            _currentFolderSet = lastFolderSet;
            _currentFolder = _currentFolderSet.FirstOrDefault();
            SetLandingFolderSet(lastFolderSet);
            FolderPathText.Text = FormatFolderSetSummary(lastFolderSet);
            int existing = lastFolderSet.Count(Directory.Exists);
            FolderCountText.Text = existing > 0 ? "Last folder set saved" : "Last folder set unavailable";
        }

        if (state is null)
        {
            SyncFavoriteFilterControls();
            SyncFoldersSectionControls();
            return;
        }

        _stateExtensionData = state.ExtensionData is null ? null : new Dictionary<string, JsonElement>(state.ExtensionData);

        if (state.CardWidth >= SizeSlider.Minimum && state.CardWidth <= SizeSlider.Maximum)
            SizeSlider.Value = state.CardWidth;

        _rightPanelWidth = NormalizeRightPanelWidth(state.RightPanelWidth);
        ApplyRightPanelState(state.RightPanelOpen ?? true);

        _displayStyle = NormalizeDisplayStyle(state.DisplayStyle);
        SyncDisplayStyleButtons();
        _aspectMode = NormalizeAspectMode(state.AspectMode);
        SyncAspectButtons();
        _sortBy = NormalizeSortBy(state.SortBy);
        _randomSortSeed = string.IsNullOrWhiteSpace(state.RandomSortSeed) ? "default" : state.RandomSortSeed;
        SyncSortButtons();
        RestoreDateFilter(state);
        _favoriteFilterLevels.Clear();
        if (state.FavoriteFilterLevels is { Count: > 0 })
            _favoriteFilterLevels.UnionWith(state.FavoriteFilterLevels.Where(level => level is >= MinFavoriteFilterLevel and <= MaxFavoriteFilterLevel));
        else if (state.FavoriteFilterLevel is >= MinFavoriteFilterLevel and <= MaxFavoriteFilterLevel)
            _favoriteFilterLevels.Add(state.FavoriteFilterLevel.Value); // additive migration from the scalar schema
        _showUnseenDots = state.ShowUnseenDots;
        _confirmBeforeDelete = state.ConfirmBeforeDelete;
        _foldersSectionExpanded = state.FoldersSectionExpanded ?? true;
        SyncFoldersSectionControls();
        if (ConfirmBeforeDeleteCheckBox is not null) ConfirmBeforeDeleteCheckBox.IsChecked = _confirmBeforeDelete;
        if (ShowUnseenDots is not null) ShowUnseenDots.IsChecked = _showUnseenDots;
        SetFavoriteFilterState(state.ShowFavoritesOnly, !state.ShowFavoritesOnly && state.ShowUnfavoriteOnly, apply: false, persist: false);
        _hiddenFolderBuckets.Clear();
        foreach (string folder in NormalizeFolderSet(state.HiddenFolderBuckets ?? []))
            _hiddenFolderBuckets.Add(folder);
        _selectedFolderBucketKeys.Clear();
        foreach (string folder in NormalizeFolderSet(state.SelectedFolderBucketKeys ?? []))
            _selectedFolderBucketKeys.Add(folder);
        _primarySelectedFolderBucketKey = NormalizeFolderSet([state.PrimarySelectedFolderBucketKey ?? ""]).FirstOrDefault();
        _pinnedPreviewPaths.Clear();
        foreach (string? path in (state.PinnedPreviewPaths ?? []).Select(NormalizePinnedPreviewPath))
        {
            if (path is not null)
                _pinnedPreviewPaths.Add(path);
        }

        _restoredPreviewTabPaths.Clear();
        _restoredPreviewTabPaths.AddRange(NormalizePreviewTabPaths(state.PreviewTabPaths ?? [], MaxPersistedPreviewTabs));
        _restoredActivePreviewTabPath = NormalizePinnedPreviewPath(state.ActivePreviewTabPath);
        if (_restoredActivePreviewTabPath is null
            || !_restoredPreviewTabPaths.Contains(_restoredActivePreviewTabPath, StringComparer.OrdinalIgnoreCase))
        {
            _restoredActivePreviewTabPath = _restoredPreviewTabPaths.FirstOrDefault();
        }
        _previewTabsPersistenceReady = _restoredPreviewTabPaths.Count == 0;

        if (!string.IsNullOrWhiteSpace(state.SearchQuery))
            SearchInput.Text = state.SearchQuery;

        _restoredSelectedPath = state.SelectedPath;
    }

    private ViewerState? ReadState()
    {
        if (!TryReadViewerStateFile(ResolvedStatePath, out var state))
        {
            _stateWriteBlocked = true;
            ReportPersistenceRefusal("Viewer settings", ResolvedStatePath, protectedFile: true);
            return null;
        }

        return state;
    }

    private static bool TryReadViewerStateFile(string path, out ViewerState? state)
    {
        state = null;
        if (!File.Exists(path))
            return true;

        try
        {
            string json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            state = JsonSerializer.Deserialize<ViewerState>(json);
            return state is not null && state.Version <= 2;
        }
        catch
        {
            state = null;
            return false;
        }
    }

    private static Dictionary<string, JsonElement>? CloneExtensionData(Dictionary<string, JsonElement>? source)
    {
        if (source is null || source.Count == 0)
            return null;

        var clone = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var entry in source)
            clone[entry.Key] = entry.Value.Clone();
        return clone;
    }

    private void SaveState()
    {
        if (_initializing || _suppressStateSave) return;
        if (_stateWriteBlocked)
        {
            ReportPersistenceRefusal("Viewer settings", ResolvedStatePath, protectedFile: true);
            return;
        }

        try
        {
            string path = ResolvedStatePath;
            var selectedPath = SelectedTile() is { IsRealFile: true } selected ? selected.Path : null;
            _restoredSelectedPath = selectedPath;
            List<string> previewTabPaths = _previewTabsPersistenceReady
                ? NormalizePreviewTabPaths(_previewTabs.Select(static tab => tab.Path), MaxPersistedPreviewTabs)
                : _restoredPreviewTabPaths.ToList();
            string? activePreviewTabPath = _previewTabsPersistenceReady
                ? NormalizePinnedPreviewPath(_activePreviewTabPath)
                : _restoredActivePreviewTabPath;
            if (activePreviewTabPath is null
                || !previewTabPaths.Contains(activePreviewTabPath, StringComparer.OrdinalIgnoreCase))
            {
                activePreviewTabPath = previewTabPaths.FirstOrDefault();
            }
            var state = new ViewerState
            {
                Version = 2,
                LastFolder = _currentFolder,
                LastFolderSet = _currentFolderSet.Count > 0 ? _currentFolderSet : null,
                SearchQuery = SearchInput.Text,
                CardWidth = SizeSlider.Value,
                RightPanelOpen = RightPanel.Visibility == Visibility.Visible,
                RightPanelWidth = _rightPanelWidth,
                DisplayStyle = _displayStyle,
                AspectMode = _aspectMode,
                SortBy = _sortBy,
                RandomSortSeed = _randomSortSeed,
                DatePreset = _dateFromLocal.HasValue || _dateToLocal.HasValue ? DatePresetManualValue : DatePresetNoneValue,
                DateFrom = FormatStateDate(_dateFromLocal),
                DateTo = FormatStateDate(_dateToLocal),
                ShowFavoritesOnly = FavoriteOnlyFilter?.IsChecked == true,
                ShowUnfavoriteOnly = UnfavoriteOnlyFilter?.IsChecked == true,
                FavoriteFilterLevels = _favoriteFilterLevels.Count > 0 ? _favoriteFilterLevels.OrderBy(static level => level).ToList() : null,
                ShowUnseenDots = _showUnseenDots,
                ConfirmBeforeDelete = _confirmBeforeDelete,
                FoldersSectionExpanded = _foldersSectionExpanded,
                HiddenFolderBuckets = _hiddenFolderBuckets.Count > 0 ? _hiddenFolderBuckets.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToList() : null,
                SelectedFolderBucketKeys = _selectedFolderBucketKeys.Count > 0 ? _selectedFolderBucketKeys.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToList() : null,
                PrimarySelectedFolderBucketKey = _primarySelectedFolderBucketKey,
                PinnedPreviewPaths = _pinnedPreviewPaths.Count > 0 ? _pinnedPreviewPaths.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToList() : null,
                PreviewTabPaths = previewTabPaths.Count > 0 ? previewTabPaths : null,
                ActivePreviewTabPath = activePreviewTabPath,
                SelectedPath = selectedPath,
                ExtensionData = CloneExtensionData(_stateExtensionData),
            };
            bool malformed = false;
            bool saved = TryWithPersistenceLock(path, () =>
            {
                if (!TryReadViewerStateFile(path, out var latest))
                {
                    malformed = true;
                    return false;
                }
                state.ExtensionData = CloneExtensionData(latest is null ? _stateExtensionData : latest.ExtensionData);
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                return TryWriteAtomicText(path, json);
            });
            if (!saved)
            {
                if (malformed)
                    _stateWriteBlocked = true;
                ReportPersistenceRefusal("Viewer settings", path, malformed, malformed ? null : SaveState);
                return;
            }
            _stateExtensionData = CloneExtensionData(state.ExtensionData);
            if (_currentFolderSet.Count > 0)
                SaveSharedRecentFolderSet(_currentFolderSet);
        }
        catch
        {
            ReportPersistenceRefusal("Viewer settings", ResolvedStatePath, retryAction: SaveState);
        }
    }

    private static string ResolvedSharedRecentPath
    {
        get
        {
            string? overridePath = Environment.GetEnvironmentVariable("PHOTOVIEWER_WPF_RECENT_PATH");
            return string.IsNullOrWhiteSpace(overridePath) ? ProjectCachePath("recent-folders.json") : Path.GetFullPath(overridePath);
        }
    }

    private static SharedRecentReadResult ReadSharedRecentFolders()
    {
        string path = ResolvedSharedRecentPath;
        if (!File.Exists(path))
            return new SharedRecentReadResult(true, NormalizeSharedRecentFolders(null), null);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new SharedRecentReadResult(false, NormalizeSharedRecentFolders(null), "shared recent root is not an object");
            return new SharedRecentReadResult(true, NormalizeSharedRecentFolders(document.RootElement), null);
        }
        catch (Exception ex)
        {
            return new SharedRecentReadResult(false, NormalizeSharedRecentFolders(null), ex.Message);
        }
    }

    private static SharedRecentFoldersState NormalizeSharedRecentFolders(JsonElement? root)
    {
        List<string> lastFolderSet = [];
        List<List<string>> recentFolderSets = [];
        string updatedAtUtc = DateTime.UtcNow.ToString("O");

        if (root.HasValue && root.Value.ValueKind == JsonValueKind.Object)
        {
            var element = root.Value;
            if (element.TryGetProperty("version", out var versionElement)
                && (versionElement.ValueKind != JsonValueKind.Number
                    || !versionElement.TryGetInt32(out int version)
                    || version != 1))
                throw new JsonException("version must be 1");
            if (element.TryGetProperty("lastFolderSet", out var lastFolderSetElement))
            {
                if (lastFolderSetElement.ValueKind is not (JsonValueKind.Array or JsonValueKind.String))
                    throw new JsonException("lastFolderSet must be an array or string");
                if (lastFolderSetElement.ValueKind == JsonValueKind.Array
                    && lastFolderSetElement.EnumerateArray().Any(static item => item.ValueKind != JsonValueKind.String))
                    throw new JsonException("lastFolderSet array entries must be strings");
                lastFolderSet = NormalizeFolderSet(lastFolderSetElement);
            }
            if (element.TryGetProperty("recentFolderSets", out var recentFolderSetsElement))
            {
                if (recentFolderSetsElement.ValueKind != JsonValueKind.Array)
                    throw new JsonException("recentFolderSets must be an array");
                foreach (var folderSetElement in recentFolderSetsElement.EnumerateArray())
                {
                    if (folderSetElement.ValueKind is not (JsonValueKind.Array or JsonValueKind.String))
                        throw new JsonException("recentFolderSets entries must be arrays or strings");
                    if (folderSetElement.ValueKind == JsonValueKind.Array
                        && folderSetElement.EnumerateArray().Any(static item => item.ValueKind != JsonValueKind.String))
                        throw new JsonException("recentFolderSets nested entries must be strings");
                }
                recentFolderSets = NormalizeRecentFolderSets(recentFolderSetsElement);
            }
            if (element.TryGetProperty("updatedAtUtc", out var updatedAtElement) &&
                updatedAtElement.ValueKind != JsonValueKind.String)
                throw new JsonException("updatedAtUtc must be a string");
            if (element.TryGetProperty("updatedAtUtc", out updatedAtElement) && !string.IsNullOrWhiteSpace(updatedAtElement.GetString()))
                updatedAtUtc = updatedAtElement.GetString()!;

            var extensionData = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name is "version" or "lastFolderSet" or "recentFolderSets" or "updatedAtUtc")
                    continue;
                extensionData[property.Name] = property.Value.Clone();
            }
            return new SharedRecentFoldersState
            {
                LastFolderSet = lastFolderSet,
                RecentFolderSets = recentFolderSets,
                UpdatedAtUtc = updatedAtUtc,
                ExtensionData = extensionData.Count > 0 ? extensionData : null,
            };
        }

        return new SharedRecentFoldersState
        {
            LastFolderSet = lastFolderSet,
            RecentFolderSets = recentFolderSets,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    private static List<string> NormalizeFolderSet(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return NormalizeFolderSet(element.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString() ?? ""));

        if (element.ValueKind == JsonValueKind.String)
            return NormalizeFolderSet(SplitFolderSet(element.GetString()));

        return [];
    }

    private static List<string> NormalizeFolderSet(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<string>();
        foreach (string raw in paths)
        {
            string? path = NormalizeRecentFolderPath(raw);
            if (path is null || !seen.Add(path))
                continue;
            normalized.Add(path);
        }

        return normalized;
    }

    private static IEnumerable<string> SplitFolderSet(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(["\r\n", "\n"], StringSplitOptions.None).Select(static item => item.Trim());

    private static List<List<string>> NormalizeRecentFolderSets(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return [];

        return NormalizeRecentFolderSets(element.EnumerateArray().Select(NormalizeFolderSet));
    }

    private static List<List<string>> NormalizeRecentFolderSets(IEnumerable<IReadOnlyList<string>> folderSets)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<List<string>>();
        foreach (var folderSet in folderSets)
        {
            var folders = NormalizeFolderSet(folderSet);
            if (folders.Count == 0)
                continue;

            string key = FormatRecentFolderSet(folders);
            if (!seen.Add(key))
                continue;

            normalized.Add(folders);
            if (normalized.Count >= MaxRecentFolderSets)
                break;
        }

        return normalized;
    }

    private static string FormatRecentFolderSet(IEnumerable<string> paths)
        => string.Join("\n", NormalizeFolderSet(paths));

    private static string? NormalizeRecentFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return null;
        }
    }

    private bool SaveSharedRecentFolderSet(IEnumerable<string> folders)
    {
        var folderSet = NormalizeFolderSet(folders);
        if (folderSet.Count == 0)
            return true;

        try
        {
            string path = ResolvedSharedRecentPath;
            bool malformed = false;
            bool saved = TryWithPersistenceLock(path, () =>
            {
                var current = ReadSharedRecentFolders();
                if (!current.Ok)
                {
                    malformed = true;
                    return false;
                }
                var recentFolderSets = NormalizeRecentFolderSets(
                    new[] { folderSet }.Concat(current.Recent.RecentFolderSets));
                var next = new SharedRecentFoldersState
                {
                    LastFolderSet = folderSet,
                    RecentFolderSets = recentFolderSets,
                    UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
                    ExtensionData = CloneExtensionData(current.Recent.ExtensionData),
                };
                var json = JsonSerializer.Serialize(next, SharedRecentJsonOptions);
                return TryWriteAtomicText(path, json);
            });
            if (!saved)
                ReportPersistenceRefusal("Recent folder history", path, malformed, malformed ? null : () => SaveSharedRecentFolderSet(folderSet));
            return saved;
        }
        catch
        {
            ReportPersistenceRefusal("Recent folder history", ResolvedSharedRecentPath, retryAction: () => SaveSharedRecentFolderSet(folderSet));
            return false;
        }
    }

    private bool CloseTopmostOverlay()
    {
        if (DeleteConfirmationDialog.Visibility == Visibility.Visible)
        {
            DeleteCancel_Click(this, new RoutedEventArgs());
            return true;
        }

        if (AppSettingsDialog.Visibility == Visibility.Visible)
        {
            AppSettingsDialog.Visibility = Visibility.Collapsed;
            RestoreOverlayFocus(_settingsFocusBeforeDialog);
            return true;
        }

        if (Modal.Visibility != Visibility.Visible)
            return false;

        CloseModal();
        return true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (DeleteConfirmationDialog.Visibility == Visibility.Visible || AppSettingsDialog.Visibility == Visibility.Visible)
        {
            if (key == Key.Escape)
            {
                CloseTopmostOverlay();
                e.Handled = true;
                return;
            }

            // Let dialog controls retain normal Tab/Shift+Tab/Space/Enter behavior;
            // only gallery-global shortcuts are suppressed while an overlay is active.
            base.OnPreviewKeyDown(e);
            return;
        }

        if (IsGlobalShortcutInputFocused(Keyboard.FocusedElement))
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        if (key == Key.T
            && (Keyboard.Modifiers & ModifierKeys.Shift) != 0
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Windows)) != 0
            && RestoreLastClosedPreviewTab())
        {
            e.Handled = true;
            return;
        }

        bool tabReorderChord = (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Shift)) == (ModifierKeys.Alt | ModifierKeys.Shift)
            && (Keyboard.Modifiers & ~(ModifierKeys.Alt | ModifierKeys.Shift)) == ModifierKeys.None;
        if (tabReorderChord && (key is Key.Left or Key.Right) && TryReorderFocusedPreviewTab(key == Key.Left ? -1 : 1))
        {
            e.Handled = true;
            return;
        }

        if (TryHandleModalTransformKey(e))
        {
            e.Handled = true;
            return;
        }

        if (TryHandleZoomKey(e))
        {
            e.Handled = true;
            return;
        }

        if (Modal.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Left)
            {
                NavigateModal(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                NavigateModal(1);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F)
        {
            AdjustSelectedFavorite(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.U)
        {
            AdjustSelectedFavorite(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete && RequestDeleteSelected())
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && CloseTopmostOverlay())
            e.Handled = true;
        base.OnPreviewKeyDown(e);
    }

    private static bool IsGlobalShortcutInputFocused(IInputElement? focused)
        => focused is TextBoxBase or ComboBox or DatePicker or ButtonBase;

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (Modal.Visibility == Visibility.Visible)
        {
            AdjustModalZoom(e.Delta > 0 ? ModalZoomWheelStep : 1 / ModalZoomWheelStep);
            e.Handled = true;
            return;
        }

        if (IsZoomModifierActive())
        {
            AdjustCardWidth(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseWheel(e);
    }

    // ─────────── Window chrome buttons ───────────
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        // Fake-maximize to the working area so the taskbar is never covered and
        // borderless content is not clipped by the maximized non-client frame.
        if (_fakeMaximized)
        {
            Left = _restoreBounds.Left;
            Top = _restoreBounds.Top;
            Width = _restoreBounds.Width;
            Height = _restoreBounds.Height;
            _fakeMaximized = false;
        }
        else
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
            var wa = SystemParameters.WorkArea;
            Left = wa.Left;
            Top = wa.Top;
            Width = wa.Width;
            Height = wa.Height;
            _fakeMaximized = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    public string? SelectedPathForSmoke => SelectedTile()?.Path;
    public string? SelectedFileNameForSmoke => SelectedTile()?.FileName;
    public static List<string> SupportedImageExtensionsForSmoke
        => SupportedImageExtensions.OrderBy(static extension => extension, StringComparer.OrdinalIgnoreCase).ToList();
    public string SearchQueryForSmoke => SearchInput.Text;
    public bool IsEditableTextInputFocusedForSmoke => Keyboard.FocusedElement is TextBoxBase;
    public string StatePathForSmoke => ResolvedStatePath;
    public string FavoritesPathForSmoke => ResolvedFavoritesPath;
    public string SeenPathForSmoke => ResolvedSeenPath;
    public string SharedRecentPathForSmoke => ResolvedSharedRecentPath;
    public int CatalogCountForSmoke => _allTiles.Count;
    public List<string> AllFileNamesForSmoke => _allTiles.Select(static tile => tile.FileName).ToList();
    public string DeleteStatusForSmoke => _deleteStatus;
    public bool DeleteConfirmationVisibleForSmoke => DeleteConfirmationDialog.Visibility == Visibility.Visible;
    public bool DeleteStatusVisibleForSmoke => DeleteStatusToast.Visibility == Visibility.Visible;
    public bool DeleteStatusRetryVisibleForSmoke => DeleteStatusRetryButton.Visibility == Visibility.Visible;
    public bool AppSettingsVisibleForSmoke => AppSettingsDialog.Visibility == Visibility.Visible;
    public bool SettingsFocusTrapConfiguredForSmoke
        => KeyboardNavigation.GetTabNavigation(AppSettingsDialogSurface) == KeyboardNavigationMode.Cycle;
    public bool DeleteFocusTrapConfiguredForSmoke
        => KeyboardNavigation.GetTabNavigation(DeleteConfirmationDialogSurface) == KeyboardNavigationMode.Cycle;
    public bool IsSettingsDialogFocusedForSmoke => AppSettingsDialog.IsKeyboardFocusWithin;
    public bool IsDeleteDialogFocusedForSmoke => DeleteConfirmationDialog.IsKeyboardFocusWithin;
    public bool IsAppSettingsButtonFocusedForSmoke => OpenAppSettingsButton.IsKeyboardFocused;
    public bool IsCardsListFocusedForSmoke => CardsList.IsKeyboardFocused;
    public bool LogoHasAutomationNameForSmoke
        => string.Equals(System.Windows.Automation.AutomationProperties.GetName(LogoHomeButton), "Back to folder selection", StringComparison.Ordinal);
    public bool DialogsHaveAutomationNamesForSmoke
        => !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(AppSettingsDialog))
            && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(DeleteConfirmationDialog));
    public int ModalZIndexForSmoke => Panel.GetZIndex(Modal);
    public int DeleteConfirmationZIndexForSmoke => Panel.GetZIndex(DeleteConfirmationDialog);
    public int DeleteStatusZIndexForSmoke => Panel.GetZIndex(DeleteStatusToast);
    public bool ConfirmBeforeDeleteForSmoke => _confirmBeforeDelete;

    public void SetRecycleBinDeleteBackendForSmoke(Func<string, RecycleBinDeleteResult> backend)
        => _recycleBinDelete = backend ?? throw new ArgumentNullException(nameof(backend));

    public void SetCanonicalPathResolverForSmoke(Func<string, string> resolver)
        => _resolveFinalPath = resolver ?? throw new ArgumentNullException(nameof(resolver));
    public void ResetCanonicalPathResolverForSmoke() => _resolveFinalPath = ResolveFinalPathCore;

    public void SetConfirmBeforeDeleteForSmoke(bool value) => _confirmBeforeDelete = value;
    public void FlushStateForSmoke()
    {
        _searchStateSaveTimer.Stop();
        SaveState();
    }
    public bool RequestDeleteSelectedForSmoke() => RequestDeleteSelected();
    public bool RequestBulkDeleteSelectedForSmoke() => RequestBulkDeleteSelected();
    public void CancelDeleteForSmoke() => DeleteCancel_Click(this, new RoutedEventArgs());
    public void ConfirmDeleteForSmoke(bool doNotAskAgain)
    {
        DoNotAskAgainCheckBox.IsChecked = doNotAskAgain;
        DeleteConfirm_Click(this, new RoutedEventArgs());
    }
    public void OpenAppSettingsForSmoke() => OpenAppSettings_Click(this, new RoutedEventArgs());
    public bool ActivateLogoForSmoke()
    {
        LogoHomeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return Landing.Visibility == Visibility.Visible;
    }
    public void RetryStatusForSmoke() => RetryDelete_Click(this, new RoutedEventArgs());
    public void ReportScanAccessFailureForSmoke() => ReportScanAccessFailures(1);

    public bool ValidateDeletePathForSmoke(string path, bool includeInCatalog = true, bool includeInFiltered = true)
    {
        var tile = new Tile { Path = path, FileName = Path.GetFileName(path), IsRealFile = true };
        if (includeInCatalog)
            _allTiles.Add(tile);
        if (includeInFiltered)
            _tiles.Add(tile);
        try
        {
            return TryValidateDelete(tile, out _);
        }
        finally
        {
            _allTiles.Remove(tile);
            _tiles.Remove(tile);
        }
    }
    public string EnhancementJobsPathForSmoke => ResolvedEnhancementJobsPath;
    public string? CurrentFolderForSmoke => _currentFolder;
    public List<string> CurrentFolderSetForSmoke => _currentFolderSet.ToList();
    public List<string> LandingFolderSetForSmoke => _landingFolderSet.ToList();
    public bool LandingVisibleForSmoke => Landing.Visibility == Visibility.Visible;
    public int RecentFolderSetCountForSmoke => _recentFolderSetViews.Count;
    public string LastFolderSetDisplayForSmoke => LastFolderSetText.Text;
    public int FolderBucketCountForSmoke => _folderBucketViews.Count;
    public int HiddenFolderBucketCountForSmoke => _folderBucketViews.Count(static bucket => bucket.Hidden);
    public List<string> FolderBucketKeysForSmoke => _folderBucketViews.Select(static bucket => bucket.Key).ToList();
    public List<string> HiddenFolderBucketKeysForSmoke => _folderBucketViews.Where(static bucket => bucket.Hidden).Select(static bucket => bucket.Key).ToList();
    public List<string> SelectedFolderBucketKeysForSmoke => _folderBucketViews.Where(bucket => _selectedFolderBucketKeys.Contains(bucket.Key)).Select(static bucket => bucket.Key).ToList();
    public string? PrimarySelectedFolderBucketKeyForSmoke => _primarySelectedFolderBucketKey;
    public bool FolderBucketSelectionAccessibleForSmoke
        => SidebarFolderSetList.SelectionMode == SelectionMode.Extended
            && string.Equals(System.Windows.Automation.AutomationProperties.GetName(SidebarFolderSetList), "Folder buckets", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(FoldersSectionToggle))
            && string.Equals(System.Windows.Automation.AutomationProperties.GetName(ShowSelectedFolderBucketsButton), "Show selected folder buckets", StringComparison.Ordinal)
            && string.Equals(System.Windows.Automation.AutomationProperties.GetName(HideSelectedFolderBucketsButton), "Hide selected folder buckets", StringComparison.Ordinal);
    public bool ModalVisibleForSmoke => Modal.Visibility == Visibility.Visible;
    public void CloseModalForSmoke() => CloseModal();
    public int FilteredCountForSmoke => _tiles.Count;
    public int SelectedCountForSmoke => SelectedTiles().Count;
    public List<string> SelectedFileNamesForSmoke => SelectedTiles().Select(static tile => tile.FileName).ToList();
    public FileDragOutSmokeSnapshot BuildFileDropPayloadForSmoke(string originFileName, bool originWasSelected)
    {
        Tile? origin = _allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, originFileName, StringComparison.OrdinalIgnoreCase));
        string[] paths = [];
        string reason = "origin was not found";
        bool built = origin is not null && TryBuildFileDropPayload(origin, originWasSelected, out paths, out reason);
        if (!built)
            return new FileDragOutSmokeSnapshot(false, [], false, reason, false, FileDragSurfaceContractForSmoke);

        var data = new DataObject(DataFormats.FileDrop, paths);
        return new FileDragOutSmokeSnapshot(
            true,
            paths.ToList(),
            data.GetDataPresent(DataFormats.FileDrop),
            "",
            ExceedsFileDragThreshold(new Point(0, 0), new Point(SystemParameters.MinimumHorizontalDragDistance + 0.1, 0)),
            FileDragSurfaceContractForSmoke);
    }
    public bool FileDragThresholdRejectsExactDistanceForSmoke
        => !ExceedsFileDragThreshold(new Point(0, 0), new Point(SystemParameters.MinimumHorizontalDragDistance, 0));
    public bool FileDragSurfaceContractForSmoke
        => !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetHelpText(CardsList))
            && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetHelpText(RowsList))
            && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetHelpText(PreviewImageDragSurface));
    public bool RightPreviewEmptyForSmoke
        => string.Equals(PreviewFileName.Text, "No matching image", StringComparison.Ordinal)
            && PreviewBitmap.Visibility == Visibility.Collapsed;
    public bool BulkDeleteButtonAccessibleForSmoke
        => BulkDeleteButton.IsEnabled
            && string.Equals(System.Windows.Automation.AutomationProperties.GetName(BulkDeleteButton), "Move selected images to Recycle Bin", StringComparison.Ordinal);
    public int CardsSelectedCountForSmoke => CardsList.SelectedItems.Count;
    public int RowsSelectedCountForSmoke => RowsList.SelectedItems.Count;
    public bool MultiSelectionEnabledForSmoke => CardsList.SelectionMode == SelectionMode.Extended && RowsList.SelectionMode == SelectionMode.Extended;
    public string HeaderStatsForSmoke => HeaderStats.Text;
    public bool SetListModeForSmoke()
    {
        ModeList.IsChecked = true;
        return RowsList.Visibility == Visibility.Visible;
    }

    public bool SetGridModeForSmoke()
    {
        ModeGrid.IsChecked = true;
        return CardsList.Visibility == Visibility.Visible;
    }
    public int SelectedFavoriteLevelForSmoke => SelectedTile()?.Fav ?? 0;
    public bool SelectedUnseenForSmoke => SelectedTile()?.Unseen == true;
    public int FavoriteStoreCountForSmoke => _favorites.Count(static item => item.Value > 0);
    public int SeenStoreCountForSmoke => _seenPaths.Count;
    public int EnhancedStoreCountForSmoke => _enhancedOutputs.Count;
    public int EnhancementJobsReadForSmoke => _enhancementJobsRead;
    public int EnhancedCandidateCountForSmoke => _enhancedCandidateCount;
    public bool EnhancementReadOkForSmoke => _enhancementReadOk;
    public string? EnhancementReadErrorForSmoke => _enhancementReadError;
    public int PreviewTabCountForSmoke => _previewTabs.Count;
    public int ClosedPreviewTabCountForSmoke => _closedPreviewTabs.Count;
    public string? ActivePreviewTabNameForSmoke => _previewTabs.FirstOrDefault(tab => tab.IsActive)?.FileName;
    public List<string> PreviewTabNamesForSmoke => _previewTabs.Select(static tab => tab.FileName).ToList();
    public bool PreviewTabAccessibilityForSmoke
        => string.Equals(System.Windows.Automation.AutomationProperties.GetName(PreviewTabList), "Preview tabs", StringComparison.Ordinal)
            && _previewTabs.All(tab => FindPreviewTabButton(tab) is Button button
                && string.Equals(System.Windows.Automation.AutomationProperties.GetName(button), tab.AutomationName, StringComparison.Ordinal));
    public bool FocusPreviewTabForSmoke(string fileName)
    {
        PreviewTabView? tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
            return false;

        Button? button = FindPreviewTabButton(tab);
        return button?.Focus() == true;
    }
    public bool ReorderFocusedPreviewTabForSmoke(int delta) => TryReorderFocusedPreviewTab(delta);
    public bool DragMovePreviewTabForSmoke(string fileName, int destinationIndex)
    {
        PreviewTabView? tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return MovePreviewTab(tab, destinationIndex, reportFailure: true);
    }
    public bool MiddleClosePreviewTabForSmoke(string fileName)
    {
        PreviewTabView? tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return tab is not null && ClosePreviewTab(tab.Path);
    }
    public bool InvalidPreviewTabMovePreservesStateForSmoke()
    {
        List<string> beforeOrder = PreviewTabNamesForSmoke;
        string? beforeActive = ActivePreviewTabNameForSmoke;
        List<string> beforePinned = _previewTabs.Where(static tab => tab.IsPinned).Select(static tab => tab.FileName).ToList();
        PreviewTabView? tab = _previewTabs.FirstOrDefault();
        bool moved = MovePreviewTab(tab, _previewTabs.Count + 1, reportFailure: true);
        return !moved
            && PreviewTabNamesForSmoke.SequenceEqual(beforeOrder, StringComparer.OrdinalIgnoreCase)
            && string.Equals(ActivePreviewTabNameForSmoke, beforeActive, StringComparison.OrdinalIgnoreCase)
            && _previewTabs.Where(static candidate => candidate.IsPinned).Select(static candidate => candidate.FileName).SequenceEqual(beforePinned, StringComparer.OrdinalIgnoreCase)
            && DeleteStatusVisibleForSmoke
            && DeleteStatusForSmoke.Contains("order was preserved", StringComparison.OrdinalIgnoreCase);
    }
    public bool RestorePreviewTabFocusForSmoke => Keyboard.FocusedElement == RestorePreviewTabButton;
    public bool FocusedPreviewTabForSmoke(string fileName)
        => Keyboard.FocusedElement is FrameworkElement { Tag: PreviewTabView tab }
            && string.Equals(tab.FileName, fileName, StringComparison.OrdinalIgnoreCase);
    public bool PreviewTabHoverVisibleForSmoke => PreviewTabHoverPopup?.IsOpen == true;
    public string? HoverPreviewTabNameForSmoke => string.IsNullOrWhiteSpace(_hoverPreviewTabPath)
        ? null
        : _allTiles.FirstOrDefault(tile => string.Equals(tile.Path, _hoverPreviewTabPath, StringComparison.OrdinalIgnoreCase))?.FileName;
    public string? HoverPreviewTabPathForSmoke => _hoverPreviewTabPath;
    public string? HoverPreviewTabBitmapPathForSmoke => _hoverPreviewTabBitmapPath;
    public int PreviewTabHoverDecodeStartCountForSmoke => _previewTabHoverDecodeStartCount;
    public int PreviewTabHoverDecodeFailureCountForSmoke => _previewTabHoverDecodeFailureCount;
    public bool SelectedEnhancedForSmoke => SelectedTile()?.Enhanced == true;
    public string? SelectedEnhancedOutputPathForSmoke => SelectedTile()?.EnhancedOutputPath;
    public int UnseenCountForSmoke => _allTiles.Count(static tile => tile.Unseen);
    public int VisibleUnseenDotCountForSmoke => _allTiles.Count(static tile => tile.ShowUnseenDot);
    public bool FoldersSectionExpandedForSmoke => _foldersSectionExpanded && FoldersSectionContent.Visibility == Visibility.Visible;
    public int LastInitialUnseenCountForSmoke => _lastInitialUnseenCount;
    public int GridRealizedCountForSmoke => _gridTiles.Count;
    public int GridDeferredCountForSmoke => Math.Max(0, _tiles.Count - _gridTiles.Count);
    public int GridWindowStartIndexForSmoke => _gridStartIndex;
    public int GridWindowEndIndexForSmoke => _gridStartIndex + _gridTiles.Count;
    public bool FocusSearchInputForSmoke() => SearchInput.Focus();
    public bool SearchWatermarkVisibleForSmoke => SearchWatermark.Visibility == Visibility.Visible;
    public bool SearchAutomationHelpTextForSmoke => string.Equals(
        AutomationProperties.GetHelpText(SearchInput),
        "Search filenames and prompts. Separate terms with commas.",
        StringComparison.Ordinal);
    public bool DatePickerAutomationNamesForSmoke => string.Equals(AutomationProperties.GetName(DateFromInput), "From date", StringComparison.Ordinal)
        && string.Equals(AutomationProperties.GetName(DateToInput), "To date", StringComparison.Ordinal);
    public bool FocusCardsListForSmoke() => CardsList.Focus();
    public bool FocusDateFromInputForSmoke()
    {
        if (DateFromInput.Focus())
            return true;
        return FindVisualDescendant<DatePickerTextBox>(DateFromInput)?.Focus() == true;
    }
    public bool FocusAppSettingsButtonForSmoke() => OpenAppSettingsButton.Focus();
    public bool IsGlobalShortcutInputFocusedForSmoke => IsGlobalShortcutInputFocused(Keyboard.FocusedElement);
    public bool IsGlobalShortcutSuppressedForSmoke(IInputElement focused) => IsGlobalShortcutInputFocused(focused);

    public bool InvokePreviewKeyForSmoke(Key key)
    {
        var source = PresentationSource.FromVisual(this);
        if (source is null)
            return false;

        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        OnPreviewKeyDown(args);
        return args.Handled;
    }
    public int GridMaxRealizationCountForSmoke => MaxGridRealizationCount;
    public double CardWidthForSmoke => SizeSlider.Value;
    public double ListThumbnailSizeForSmoke => _allTiles.FirstOrDefault()?.ListThumbnailSize ?? 0;
    public bool ListUsesRecyclingVirtualizationForSmoke
        => VirtualizingPanel.GetIsVirtualizing(RowsList) && VirtualizingPanel.GetVirtualizationMode(RowsList) == VirtualizationMode.Recycling;
    public int ListRealizedContainerCountForSmoke
        => Enumerable.Range(0, RowsList.Items.Count).Count(index => RowsList.ItemContainerGenerator.ContainerFromIndex(index) is not null);
    public static async Task<PersistenceLockProbe> ProbePersistenceLockForSmokeAsync(string favoritesPath, string firstPath, string secondPath)
    {
        var start = new ManualResetEventSlim(false);
        Task<bool> first = Task.Run(() => { start.Wait(); return TryMergeFavoriteForSmoke(favoritesPath, firstPath, 1); });
        Task<bool> second = Task.Run(() => { start.Wait(); return TryMergeFavoriteForSmoke(favoritesPath, secondPath, 5); });
        start.Set();
        bool[] writers = await Task.WhenAll(first, second);
        var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        bool concurrentMerged = writers.All(static value => value)
            && TryLoadFavoritesFile(favoritesPath, merged)
            && merged.TryGetValue(NormalizeFavoritePath(firstPath), out int firstLevel) && firstLevel == 1
            && merged.TryGetValue(NormalizeFavoritePath(secondPath), out int secondLevel) && secondLevel == 5;

        string lockPath = favoritesPath + ".lock";
        bool staleRecovered = await Task.Run(() =>
        {
            string stale = JsonSerializer.Serialize(new { pid = int.MaxValue, createdAtUtc = DateTimeOffset.UtcNow.ToString("O") });
            File.WriteAllText(lockPath, stale);
            File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow.Subtract(PersistenceLockStaleAfter + TimeSpan.FromSeconds(1)));
            return TryMergeFavoriteForSmoke(favoritesPath, Path.Combine(Path.GetDirectoryName(favoritesPath)!, "stale.png"), 2)
                && !File.Exists(lockPath);
        });

        bool malformedLockProtected = await Task.Run(() =>
        {
            File.WriteAllText(lockPath, "{\"pid\":\"unknown\"}");
            string beforeMalformedLock = File.ReadAllText(favoritesPath);
            bool protectedFile = !TryMergeFavoriteForSmoke(favoritesPath, Path.Combine(Path.GetDirectoryName(favoritesPath)!, "blocked.png"), 3)
                && File.ReadAllText(favoritesPath) == beforeMalformedLock
                && File.Exists(lockPath);
            try { File.Delete(lockPath); } catch { }
            return protectedFile;
        });
        return new PersistenceLockProbe(concurrentMerged, staleRecovered, malformedLockProtected);
    }

    internal static bool TryMergeSharedStateForSmoke(
        string favoritesPath,
        string favoriteKey,
        int favoriteLevel,
        string seenPath,
        string seenKey)
        => TryMergeFavoriteForSmoke(favoritesPath, favoriteKey, favoriteLevel)
            && TryMergeSeenForSmoke(seenPath, seenKey);

    private static bool TryMergeFavoriteForSmoke(string path, string key, int level)
        => TryWithPersistenceLock(path, () =>
        {
            var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!TryLoadFavoritesFile(path, merged))
                return false;
            merged[NormalizeFavoritePath(key)] = level;
            string json = JsonSerializer.Serialize(merged.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase), new JsonSerializerOptions { WriteIndented = true });
            return TryWriteAtomicText(path, json);
        });

    private static bool TryMergeSeenForSmoke(string path, string key)
        => TryWithPersistenceLock(path, () =>
        {
            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!TryLoadSeenFile(path, merged))
                return false;
            merged.Add(NormalizeFavoritePath(key));
            string json = JsonSerializer.Serialize(merged.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static item => item, static _ => true, StringComparer.OrdinalIgnoreCase), new JsonSerializerOptions { WriteIndented = true });
            return TryWriteAtomicText(path, json);
        });

    public async Task<ListVirtualizationProbe> ProbeListVirtualizationForSmokeAsync()
    {
        bool listMode = SetListModeForSmoke();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        ScrollViewer? viewer = FindVisualDescendant<ScrollViewer>(RowsList);
        if (!listMode || viewer is null || viewer.ScrollableHeight <= 0)
            return new ListVirtualizationProbe(listMode, ListUsesRecyclingVirtualizationForSmoke, false, 0, 0, 0);

        async Task<int> MeasureAtAsync(double offset)
        {
            viewer.ScrollToVerticalOffset(offset);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            return ListRealizedContainerCountForSmoke;
        }

        int first = await MeasureAtAsync(0);
        int middle = await MeasureAtAsync(viewer.ScrollableHeight / 2);
        int last = await MeasureAtAsync(viewer.ScrollableHeight);
        bool bounded = first is > 0 and <= MaxGridRealizationCount
            && middle is > 0 and <= MaxGridRealizationCount
            && last is > 0 and <= MaxGridRealizationCount;
        return new ListVirtualizationProbe(listMode, ListUsesRecyclingVirtualizationForSmoke, bounded, first, middle, last);
    }
    public double SidebarWidthForSmoke => Sidebar.ActualWidth;
    public double RightPanelWidthForSmoke => RightPanel.ActualWidth;
    public double RightPanelStoredWidthForSmoke => _rightPanelWidth;
    public bool RightPanelOpenForSmoke => RightPanel.Visibility == Visibility.Visible;
    public bool SetRightPanelWidthForSmoke(double width)
    {
        bool changed = SetRightPanelWidth(width);
        SaveState();
        return changed;
    }
    public void ToggleRightPanelForSmoke() => ToggleRight_Click(this, new RoutedEventArgs());
    public string? LastGridZoomAnchorPathForSmoke => _lastGridZoomAnchorPath;
    public double LastGridZoomAnchorDriftForSmoke => _lastGridZoomAnchorDrift;
    public string? GridViewportAnchorForSmoke => _gridTiles.Count == 0 ? null : _gridTiles[_gridTiles.Count / 2].FileName;
    public string? CaptureGridViewportAnchorForSmoke() => Path.GetFileName(CaptureGridZoomAnchor()?.Path);
    public bool GridContainsFileForSmoke(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName) && _gridTiles.Any(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase));
    public async Task<bool> ScrollGridToMiddleForSmokeAsync()
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        ScrollViewer? viewer = FindVisualDescendant<ScrollViewer>(CardsList);
        if (viewer is null || viewer.ExtentHeight <= viewer.ViewportHeight)
            return false;
        viewer.ScrollToVerticalOffset(Math.Max(0, (viewer.ExtentHeight - viewer.ViewportHeight) / 2));
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        return true;
    }
    public string DisplayStyleForSmoke => _displayStyle;
    public string AspectModeForSmoke => _aspectMode;
    public string SortByForSmoke => _sortBy;
    public string DatePresetForSmoke => _datePreset;
    public string? DateFromForSmoke => FormatStateDate(_dateFromLocal);
    public string? DateToForSmoke => FormatStateDate(_dateToLocal);
    public string DateFilterSummaryForSmoke => DateFilterSummary.Text;
    public bool ShowFavoritesOnlyForSmoke => FavoriteOnlyFilter?.IsChecked == true;
    public bool ShowUnfavoriteOnlyForSmoke => UnfavoriteOnlyFilter?.IsChecked == true;
    public List<int> FavoriteFilterLevelsForSmoke => _favoriteFilterLevels.OrderBy(static level => level).ToList();

    public bool NavigateModalForSmoke(int delta) => NavigateModal(delta);
    public bool OpenSelectedPreviewTabForSmoke() => SelectedTile() is { IsRealFile: true } tile && OpenPreviewTab(tile, makeActive: true);
    public bool ActivatePreviewTabForSmoke(string fileName)
    {
        var tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return tab is not null && ActivatePreviewTab(tab.Path);
    }

    public bool ClosePreviewTabForSmoke(string fileName)
    {
        var tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return tab is not null && ClosePreviewTab(tab.Path);
    }

    public bool RestoreLastClosedPreviewTabForSmoke() => RestoreLastClosedPreviewTab();
    public void CloseAllPreviewTabsForSmoke() => CloseAllPreviewTabs();
    public bool TogglePreviewTabPinForSmoke(string fileName)
    {
        var tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return tab is not null && TogglePreviewTabPin(tab.Path);
    }
    public bool IsPreviewTabPinnedForSmoke(string fileName)
        => _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.IsPinned == true;
    public int PinnedPreviewCountForSmoke => _pinnedPreviewPaths.Count;
    public bool ShowPreviewTabHoverForSmoke(string fileName)
    {
        var tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return tab is not null && ShowPreviewTabHover(tab, PreviewTabList);
    }

    public bool ShowPreviewTabHoverWithDecodeForSmoke(string fileName)
    {
        var tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return tab is not null && ShowPreviewTabHover(tab, PreviewTabList, forceDecode: true);
    }

    public void SetPreviewTabHoverDecodeDelayForSmoke(string fileName, int delayMilliseconds)
    {
        string? path = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Path;
        if (!string.IsNullOrWhiteSpace(path))
            _previewTabHoverDecodeDelaysForSmoke[path] = Math.Max(0, delayMilliseconds);
    }

    public Task<PreviewTabHoverDecodeCompletion> WaitForPreviewTabHoverDecodeForSmokeAsync()
        => _lastPreviewTabHoverCompletion?.Task
            ?? Task.FromResult(PreviewTabHoverDecodeCompletion.DiscardedResult);

    public bool HidePreviewTabHoverForSmoke(string? fileName = null)
    {
        string? path = null;
        if (!string.IsNullOrWhiteSpace(fileName))
            path = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Path;
        return HidePreviewTabHover(path);
    }

    public bool ToggleSelectedFavoriteForSmoke() => ToggleSelectedFavorite();
    public bool AdjustSelectedFavoriteForSmoke(int delta) => AdjustSelectedFavorite(delta);
    public bool AdjustModalFavoriteForSmoke(int delta)
        => Modal.Visibility == Visibility.Visible && AdjustSelectedFavorite(delta);
    public int ModalFavoriteLevelForSmoke
        => int.TryParse(ModalFavoriteLevelText.Text, out int level) ? level : -1;
    public bool MarkSelectedSeenForSmoke() => SelectedTile() is { IsRealFile: true } tile && MarkTileSeen(tile);
    public bool ZoomInForSmoke() => AdjustCardWidth(1);
    public bool ZoomOutForSmoke() => AdjustCardWidth(-1);
    public bool ZoomResetForSmoke() => ResetCardWidth();
    public bool SetGridZoomForSmoke(double value)
    {
        double before = SizeSlider.Value;
        SetCardWidth(value);
        return Math.Abs(before - SizeSlider.Value) > 0.01;
    }
    public async Task WaitForGridZoomAnchorForSmokeAsync()
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
    }
    public bool ZoomWheelForSmoke(int delta)
    {
        return AdjustCardWidth(delta > 0 ? 1 : -1);
    }

    public bool ZoomShortcutForSmoke(string shortcut)
    {
        return shortcut.ToLowerInvariant() switch
        {
            "plus" or "+" or "=" => AdjustCardWidth(1),
            "minus" or "-" => AdjustCardWidth(-1),
            "zero" or "0" => ResetCardWidth(),
            _ => false,
        };
    }

    public bool AllCardWidthsMatchForSmoke(double width)
        => _allTiles.All(tile => Math.Abs(tile.CardWidth - width) < 0.01);

    public bool SetDisplayStyleForSmoke(string style) => SetDisplayStyle(style);
    public bool SetAspectModeForSmoke(string aspectMode) => SetAspectMode(aspectMode);
    public bool SetSortByForSmoke(string sortBy) => SetSortBy(sortBy);
    public bool ReshuffleRandomSortForSmoke() => ReshuffleRandomSort();
    public string RandomSortSeedForSmoke => _randomSortSeed;
    public bool ClearManualDateRangeForSmoke() => SetManualDateRange(null, null);
    public bool SetManualDateRangeForSmoke(string? from, string? to) => SetManualDateRange(ParseStateDate(from), ParseStateDate(to));
    public bool SetFavoriteFilterLevelsForSmoke(params int[] levels) => SetFavoriteFilterLevels(levels);
    public void SetShowUnseenDotsForSmoke(bool enabled)
    {
        _showUnseenDots = enabled;
        ShowUnseenDots.IsChecked = enabled;
        RefreshUnseenDots();
    }
    public void ToggleFoldersSectionForSmoke() => ToggleFoldersSection_Click(this, new RoutedEventArgs());
    public bool FocusFolderBucketListForSmoke() => SidebarFolderSetList.Focus();
    public bool SelectFolderBucketRangeForSmoke(int firstIndex, int lastIndex)
    {
        if (firstIndex < 0 || lastIndex < 0 || firstIndex >= _folderBucketViews.Count || lastIndex >= _folderBucketViews.Count)
            return false;
        int start = Math.Min(firstIndex, lastIndex);
        int count = Math.Abs(lastIndex - firstIndex) + 1;
        var selected = _folderBucketViews.Skip(start).Take(count).Select(static bucket => bucket.Key).ToList();
        SetFolderBucketSelection(selected, _folderBucketViews[lastIndex].Key, persist: true);
        return true;
    }
    public bool ToggleFolderBucketSelectionForSmoke(int index)
    {
        if (index < 0 || index >= _folderBucketViews.Count)
            return false;
        string key = _folderBucketViews[index].Key;
        var selected = _selectedFolderBucketKeys.ToList();
        if (!selected.Remove(key))
            selected.Add(key);
        string? primary = selected.Contains(key, StringComparer.OrdinalIgnoreCase) ? key : _primarySelectedFolderBucketKey;
        SetFolderBucketSelection(selected, primary, persist: true);
        return true;
    }
    public bool HideSelectedFolderBucketsForSmoke() => SetSelectedFolderBucketsHidden(hidden: true);
    public bool ShowSelectedFolderBucketsForSmoke() => SetSelectedFolderBucketsHidden(hidden: false);
    public bool SetFolderBucketHiddenForSmoke(string key, bool hidden) => SetFolderBucketHidden(key, hidden);
    public void ShowAllFolderBucketsForSmoke()
    {
        _hiddenFolderBuckets.Clear();
        ApplyFolderBucketFilterChange();
    }

    public void HideAllFolderBucketsForSmoke()
    {
        foreach (var bucket in _folderBucketViews)
            _hiddenFolderBuckets.Add(bucket.Key);
        ApplyFolderBucketFilterChange();
    }

    public void InvertFolderBucketsForSmoke()
    {
        var keys = _folderBucketViews.Select(static bucket => bucket.Key).ToList();
        var next = keys.Where(key => !_hiddenFolderBuckets.Contains(key)).ToList();
        _hiddenFolderBuckets.Clear();
        foreach (string key in next)
            _hiddenFolderBuckets.Add(key);
        ApplyFolderBucketFilterChange();
    }

    public List<string> FilteredFileNamesForSmoke(int take = 20)
        => _tiles.Take(take).Select(static tile => tile.FileName).ToList();

    public bool OpenModalForSmoke()
    {
        OpenModal();
        return Modal.Visibility == Visibility.Visible;
    }

    public bool ToggleModalFlipForSmoke() => ToggleModalFlip();

    public bool ToggleModalEnhancedForSmoke() => ToggleModalEnhanced();

    public bool ModalZoomShortcutForSmoke(string shortcut)
    {
        if (Modal.Visibility != Visibility.Visible)
            return false;

        return shortcut.ToLowerInvariant() switch
        {
            "plus" or "+" or "=" => AdjustModalZoom(ModalZoomKeyboardStep),
            "minus" or "-" => AdjustModalZoom(1 / ModalZoomKeyboardStep),
            "zero" or "0" => ResetModalTransform(_modalTransformPath),
            _ => false,
        };
    }

    public bool ModalZoomWheelForSmoke(int delta)
        => AdjustModalZoom(delta > 0 ? ModalZoomWheelStep : 1 / ModalZoomWheelStep);

    public bool ResetModalTransformForSmoke() => ResetModalTransform(_modalTransformPath, showFeedback: true);

    public bool SetModalPanForSmoke(double x, double y) => SetModalPan(x, y);

    public bool ModalChromeVisibleForSmoke
        => _modalChromeVisible
            && ModalTopBar.Visibility == Visibility.Visible
            && ModalFooter.Visibility == Visibility.Visible
            && ModalPreviousButton.Visibility == Visibility.Visible
            && ModalNextButton.Visibility == Visibility.Visible;
    public bool ModalEdgeZonesAccessibleForSmoke
        => string.Equals(System.Windows.Automation.AutomationProperties.GetName(ModalPreviousButton), "Previous image edge zone", StringComparison.Ordinal)
            && string.Equals(System.Windows.Automation.AutomationProperties.GetName(ModalNextButton), "Next image edge zone", StringComparison.Ordinal);
    public bool ModalInteractionFeedbackVisibleForSmoke => ModalInteractionFeedback.Visibility == Visibility.Visible;
    public string ModalInteractionFeedbackForSmoke => ModalInteractionFeedbackText.Text;
    public void ScheduleModalChromeToggleForSmoke() => ScheduleModalChromeToggle();
    public bool ModalEdgeNavigateForSmoke(int delta) => NavigateModal(delta);
    public bool ModalSwipeForSmoke(double horizontalDelta, double verticalDelta = 0) => TryNavigateModalSwipe(new Vector(horizontalDelta, verticalDelta));
    public bool ToggleModalMetadataForSmoke()
    {
        bool beforeChrome = ModalChromeVisibleForSmoke;
        SetModalMetadataSidebarVisible(ModalMetadataSidebar.Visibility != Visibility.Visible);
        return beforeChrome == ModalChromeVisibleForSmoke;
    }
    public bool ToggleModalMetadataFromImageDoubleClickForSmoke()
    {
        bool beforeChrome = ModalChromeVisibleForSmoke;
        bool toggled = ToggleModalMetadataSidebarFromImageDoubleClick();
        return toggled && beforeChrome == ModalChromeVisibleForSmoke;
    }

    public bool CloseTopmostOverlayForSmoke() => CloseTopmostOverlay();

    public ModalTransformSnapshot ModalTransformForSmoke()
        => new(
            _modalZoom,
            _modalFlipped,
            ModalVisualTransform?.ScaleX ?? 1,
            ModalVisualTransform?.ScaleY ?? 1,
            ModalZoomLabel?.Text ?? "",
            _modalPanX,
            _modalPanY,
            ModalPanLimits().MaxX,
            ModalPanLimits().MaxY);

    public bool ModalShowingEnhancedForSmoke => _modalShowingEnhanced;
    public bool ModalEnhancedToggleAvailableForSmoke => SelectedTile() is Tile tile && TryGetModalEnhancedOutput(tile, out _);
    public string? ModalDisplayPathForSmoke => _modalDisplayPath;

    public DisplayStyleMetrics DisplayStyleMetricsForSmoke()
    {
        var tile = _allTiles.FirstOrDefault();
        return new DisplayStyleMetrics(
            _displayStyle,
            _aspectMode,
            SizeSlider.Value,
            tile?.CardWidth ?? 0,
            tile?.CardHeight ?? 0,
            tile?.ListThumbnailWidth ?? 0,
            tile?.ListThumbnailHeight ?? 0,
            tile?.ListThumbnailSize ?? 0,
            _tiles.Count);
    }

    public int AppendPastedFoldersForSmoke(string folderText)
        => AppendLandingFolders(SplitFolderSet(folderText));

    public Task<bool> AddFoldersToCurrentSetForSmokeAsync(IEnumerable<string> folders)
        => AddFoldersToCurrentSetAsync(folders);

    public void ReturnToFolderSetEditorForSmoke()
        => ReturnToFolderSetEditor();

    public void SetLandingFolderSetForSmoke(IEnumerable<string> folders)
        => SetLandingFolderSet(folders);

    public bool SetSelectedFavoriteLevelForSmoke(int level)
    {
        return SelectedTiles().Count > 1
            ? SetFavoriteLevelForSelection(level)
            : SelectedTile() is { IsRealFile: true } tile && SetFavoriteLevel(tile, level);
    }

    public List<int> SelectedFavoriteLevelsForSmoke => SelectedTiles().Select(static tile => tile.Fav).ToList();
    public bool BulkFavoritePanelVisibleForSmoke => BulkFavoritePanel.Visibility == Visibility.Visible;
    public string BulkSelectionSummaryForSmoke => BulkSelectionText.Text;
    public int FavoriteSaveAttemptCountForSmoke => _favoriteSaveAttemptCount;

    public void SetFavoriteOnlyFilterForSmoke(bool enabled)
    {
        SetFavoriteFilterState(enabled, false, apply: true, persist: true);
    }

    public void SetUnfavoriteOnlyFilterForSmoke(bool enabled)
    {
        SetFavoriteFilterState(false, enabled, apply: true, persist: true);
    }

    public void ClearFavoriteFiltersForSmoke()
    {
        SetFavoriteFilterState(false, false, apply: true, persist: true);
    }

    public void SetEnhancedOnlyFilterForSmoke(bool enabled)
    {
        EnhancedOnlyFilter.IsChecked = enabled;
        ApplyFilters();
    }

    public void SetUnseenOnlyFilterForSmoke(bool enabled)
    {
        bool changed = UnseenOnlyFilter.IsChecked != enabled;
        UnseenOnlyFilter.IsChecked = enabled;
        if (!changed)
            ApplyFiltersForCurrentFilterChange();
    }

    public bool RealizeNextGridBatchForSmoke()
    {
        int before = _gridTiles.Count;
        int beforeStart = _gridStartIndex;
        RealizeNextGridBatch();
        return _gridTiles.Count > before || _gridStartIndex > beforeStart;
    }

    public bool RealizePreviousGridBatchForSmoke()
    {
        int beforeStart = _gridStartIndex;
        RealizePreviousGridBatch();
        return _gridStartIndex < beforeStart;
    }

    public bool SelectIndexForSmoke(int index)
    {
        if (index < 0 || index >= _tiles.Count)
            return false;

        SelectTile(_tiles[index]);
        SaveState();
        return true;
    }

    public bool SelectRangeForSmoke(int firstIndex, int lastIndex)
    {
        if (firstIndex < 0 || lastIndex < 0 || firstIndex >= _tiles.Count || lastIndex >= _tiles.Count)
            return false;

        int start = Math.Min(firstIndex, lastIndex);
        int count = Math.Abs(lastIndex - firstIndex) + 1;
        var selected = _tiles.Skip(start).Take(count).ToList();
        SetSelection(selected, _tiles[lastIndex]);
        return true;
    }

    public bool ToggleSelectionForSmoke(int index)
    {
        if (index < 0 || index >= _tiles.Count)
            return false;

        Tile target = _tiles[index];
        var selected = SelectedTiles().ToList();
        if (_selectedPaths.Contains(target.Path))
            selected.RemoveAll(tile => string.Equals(tile.Path, target.Path, StringComparison.OrdinalIgnoreCase));
        else
            selected.Add(target);

        Tile? primary = selected.Any(tile => string.Equals(tile.Path, target.Path, StringComparison.OrdinalIgnoreCase))
            ? target
            : SelectedTile();
        SetSelection(selected, primary);
        return true;
    }

    public bool SelectFileNameForSmoke(string fileName)
    {
        var tile = _tiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (tile is null)
            return false;

        SelectTile(tile);
        SaveState();
        return true;
    }

    public async Task<PreviewDecodeSmokeSnapshot> SelectPreviewForSmokeAsync(string fileName)
    {
        var tile = _tiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (tile is null || !tile.IsRealFile)
            return PreviewDecodeSmokeSnapshot.NotSelected(fileName);

        var selectionWatch = Stopwatch.StartNew();
        SelectTile(tile);
        selectionWatch.Stop();

        var completion = _previewDecodeCompletion;
        if (completion is null)
            return PreviewDecodeSmokeSnapshot.NotSelected(fileName);

        var timeout = Task.Delay(TimeSpan.FromSeconds(5));
        if (await Task.WhenAny(completion.Task, timeout) != completion.Task)
            return new PreviewDecodeSmokeSnapshot(true, tile.Path, selectionWatch.ElapsedMilliseconds, _lastPreviewImmediateMs, 0, false, false, false, "preview decode timed out");

        var decoded = await completion.Task;
        await Task.Delay(125);
        bool stable = decoded.Applied
            && string.Equals(SelectedTile()?.Path, tile.Path, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_previewDecodedPath, tile.Path, StringComparison.OrdinalIgnoreCase)
            && PreviewBitmap.Source is not null;
        return new PreviewDecodeSmokeSnapshot(
            true,
            tile.Path,
            selectionWatch.ElapsedMilliseconds,
            _lastPreviewImmediateMs,
            decoded.DecodeMs,
            decoded.Applied,
            PreviewBitmap.Source is not null,
            stable,
            stable ? "preview decode completed for the latest selection" : "preview decode did not remain synchronized with the latest selection");
    }

    public async Task<PngMetadataSmokeSnapshot> SelectPngMetadataForSmokeAsync(string fileName)
    {
        var tile = _tiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (tile is null || !tile.IsRealFile)
            return PngMetadataSmokeSnapshot.NotSelected(fileName);

        SelectTile(tile);
        return await WaitForPreviewPngMetadataForSmokeAsync(fileName);
    }

    public async Task<PngMetadataSmokeSnapshot> WaitForPreviewPngMetadataForSmokeAsync(string expectedFileName)
    {
        var completion = _previewMetadataCompletion;
        if (completion is null)
            return PngMetadataSmokeSnapshot.NotSelected(expectedFileName);

        var timeout = Task.Delay(TimeSpan.FromSeconds(5));
        if (await Task.WhenAny(completion.Task, timeout) != completion.Task)
            return new PngMetadataSmokeSnapshot(false, SelectedTile()?.Path, false, "", "", "", false, "preview metadata timed out");

        PngParametersMetadata? metadata = await completion.Task;
        await Task.Delay(125);
        Tile? selected = SelectedTile();
        bool current = selected is not null && string.Equals(selected.FileName, expectedFileName, StringComparison.OrdinalIgnoreCase);
        bool applied = metadata is not null
            && current
            && string.Equals(PreviewPromptLabel.Text, "PROMPT", StringComparison.Ordinal)
            && string.Equals(PreviewPromptText.Text, metadata.Prompt, StringComparison.Ordinal);
        return new PngMetadataSmokeSnapshot(
            current,
            selected?.Path,
            applied,
            PreviewPromptText.Text,
            PreviewNegativeText.Text,
            PreviewSamplerText.Text,
            PreviewSamplerText.Visibility == Visibility.Visible,
            applied ? "PNG parameters metadata applied to the latest selection" : "PNG parameters metadata was unavailable or did not apply to the latest selection");
    }

    public async Task<bool> WaitForModalFullDecodeForSmokeAsync()
    {
        var completion = _modalDecodeCompletion;
        if (completion is null)
            return false;

        var timeout = Task.Delay(TimeSpan.FromSeconds(8));
        if (await Task.WhenAny(completion.Task, timeout) != completion.Task)
            return false;

        try
        {
            bool decoded = await completion.Task;
            if (decoded)
                await Task.Delay(125);
            return decoded;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public static bool HasPngParametersForSmoke(string path)
        => ReadPngParametersMetadata(path, CancellationToken.None) is not null;

    public MetadataCopySmokeSnapshot CopyCurrentPreviewMetadataForSmoke()
    {
        bool copied = CopyCurrentPreviewMetadata(useSystemClipboard: false);
        return new MetadataCopySmokeSnapshot(
            copied,
            CopyPreviewMetadataButton.IsEnabled,
            SelectedTile()?.Path,
            _currentPreviewMetadataPath,
            _lastMetadataCopyText);
    }

    public MetadataCopySmokeSnapshot CopyCurrentPreviewPromptForSmoke(bool negative)
    {
        bool copied = CopyCurrentPreviewMetadataValue(negative, useSystemClipboard: false);
        Button button = negative ? CopyPreviewNegativeButton : CopyPreviewPromptButton;
        return new MetadataCopySmokeSnapshot(copied, button.IsEnabled, SelectedTile()?.Path, _currentPreviewMetadataPath, _lastMetadataCopyText);
    }

    public ModalMetadataSmokeSnapshot OpenModalMetadataForSmoke()
    {
        OpenModal();
        return ModalMetadataForSmoke();
    }

    public ModalMetadataSmokeSnapshot ToggleModalMetadataSidebarForSmoke()
    {
        SetModalMetadataSidebarVisible(ModalMetadataSidebar.Visibility != Visibility.Visible);
        return ModalMetadataForSmoke();
    }

    public ModalMetadataSmokeSnapshot DoubleClickModalImageForSmoke()
    {
        ToggleModalMetadataSidebarFromImageDoubleClick();
        return ModalMetadataForSmoke();
    }

    public ModalMetadataSmokeSnapshot SelectModalMetadataTabForSmoke(string tab)
    {
        SetModalMetadataTab(tab);
        return ModalMetadataForSmoke();
    }

    public PromptTagSearchSmokeSnapshot SearchModalPromptTagForSmoke(string tag)
    {
        Button? chip = ModalPromptChips.Children
            .OfType<Button>()
            .FirstOrDefault(candidate => string.Equals(candidate.Tag as string, tag, StringComparison.OrdinalIgnoreCase));
        bool applied = chip is not null;
        if (chip is not null)
            chip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        return PromptTagSearchSnapshotForSmoke(applied);
    }

    public List<string> ModalPromptTagsForSmoke => ModalPromptChips.Children
        .OfType<Button>()
        .Select(static chip => chip.Tag as string ?? "")
        .ToList();

    public bool ModalPromptTagsAccessibilityReadyForSmoke => ModalPromptChips.Children.OfType<Button>().All(chip =>
        !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(chip))
        && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetHelpText(chip)));

    public bool ModalPromptTagFallbackVisibleForSmoke => ModalPromptEmptyText.Visibility == Visibility.Visible;

    private PromptTagSearchSmokeSnapshot PromptTagSearchSnapshotForSmoke(bool applied)
    {
        List<string> chips = ModalPromptChips.Children
            .OfType<Button>()
            .Select(static chip => chip.Tag as string ?? "")
            .ToList();
        bool accessibilityReady = ModalPromptChips.Children.OfType<Button>().All(chip =>
            !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(chip))
            && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetHelpText(chip)));
        return new PromptTagSearchSmokeSnapshot(
            applied,
            chips,
            SearchInput.Text,
            Modal.Visibility == Visibility.Visible,
            SearchInput.IsKeyboardFocusWithin,
            accessibilityReady,
            FilteredFileNamesForSmoke());
    }

    private ModalMetadataSmokeSnapshot ModalMetadataForSmoke()
    {
        bool current = SelectedTile() is Tile selected
            && string.Equals(selected.Path, _currentPreviewMetadataPath, StringComparison.OrdinalIgnoreCase);
        return new ModalMetadataSmokeSnapshot(
            Modal.Visibility == Visibility.Visible,
            ModalMetadataSidebar.Visibility == Visibility.Visible,
            current,
            ModalPromptTabButton.IsChecked == true
                ? ModalMetadataPromptTab
                : ModalNegativeTabButton.IsChecked == true
                    ? ModalMetadataNegativeTab
                    : ModalMetadataSettingsTab,
            ModalPromptPanel.Visibility == Visibility.Visible,
            ModalNegativePanel.Visibility == Visibility.Visible,
            ModalSettingsPanel.Visibility == Visibility.Visible,
            ModalMetadataStatusText.Text,
            ModalPromptText.Text,
            ModalNegativeText.Text,
            ModalSettingsText.Text,
            CopyModalMetadataButton.IsEnabled,
            CopyModalPromptButton.IsEnabled,
            CopyModalNegativeButton.IsEnabled);
    }

    public string? PathForFileNameForSmoke(string fileName)
        => _allTiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Path;

    public string? PromptForFileNameForSmoke(string fileName)
        => _allTiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Prompt;

    public bool? IsFileUnseenForSmoke(string fileName)
        => _allTiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Unseen;

    private readonly record struct PreviewDecodeResult(string Path, BitmapSource? Bitmap, int Width, int Height, long DecodeMs, bool Applied)
    {
        public static PreviewDecodeResult Canceled => new("", null, 0, 0, 0, Applied: false);
    }
}

// Lightweight persisted shell state.
public sealed class ViewerState
{
    public int Version { get; set; } = 2;
    public string? LastFolder { get; set; }
    public List<string>? LastFolderSet { get; set; }
    public string? SearchQuery { get; set; }
    public string? SelectedPath { get; set; }
    public double CardWidth { get; set; } = 190;
    public bool? RightPanelOpen { get; set; }
    public double RightPanelWidth { get; set; } = 340;
    public string? DisplayStyle { get; set; }
    public string? AspectMode { get; set; }
    public string? SortBy { get; set; }
    public string? RandomSortSeed { get; set; }
    public string? DatePreset { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public bool ShowFavoritesOnly { get; set; }
    public bool ShowUnfavoriteOnly { get; set; }
    public List<int>? FavoriteFilterLevels { get; set; }
    public bool ShowUnseenDots { get; set; }
    // Defaults to true for both fresh and pre-P0C state files.
    public bool ConfirmBeforeDelete { get; set; } = true;
    // Missing in v1 state means expanded, preserving the original sidebar behavior.
    public bool? FoldersSectionExpanded { get; set; }
    // Kept only to read pre-P0A scalar state; new writes use FavoriteFilterLevels.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? FavoriteFilterLevel { get; set; }
    public List<string>? HiddenFolderBuckets { get; set; }
    public List<string>? SelectedFolderBucketKeys { get; set; }
    public string? PrimarySelectedFolderBucketKey { get; set; }
    public List<string>? PinnedPreviewPaths { get; set; }
    public List<string>? PreviewTabPaths { get; set; }
    public string? ActivePreviewTabPath { get; set; }
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public readonly record struct RecycleBinDeleteResult(bool Succeeded, string Reason)
{
    public static RecycleBinDeleteResult Success => new(true, "");
    public static RecycleBinDeleteResult Failed(string reason) => new(false, reason);
}

public readonly record struct DisplayStyleMetrics(
    string Style,
    string AspectMode,
    double BaseWidth,
    double CardWidth,
    double CardHeight,
    double ListThumbnailWidth,
    double ListThumbnailHeight,
    double ListThumbnailSize,
    int FilteredCount);

public readonly record struct ListVirtualizationProbe(
    bool ListMode,
    bool Recycling,
    bool Bounded,
    int FirstRealized,
    int MiddleRealized,
    int LastRealized);

public readonly record struct PersistenceLockProbe(
    bool ConcurrentMerged,
    bool StaleRecovered,
    bool MalformedLockProtected);

public readonly record struct ModalTransformSnapshot(
    double Zoom,
    bool Flipped,
    double ScaleX,
    double ScaleY,
    string ZoomLabel,
    double PanX,
    double PanY,
    double MaxPanX,
    double MaxPanY);

public sealed class RecentFolderSetView
{
    public List<string> FolderSet { get; init; } = [];
    public string Display { get; init; } = "";
    public string Detail { get; init; } = "";
}

public sealed class FolderBucketView : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Path { get; init; } = "";
    public int Count { get; init; }
    public bool Hidden { get; init; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
    public string CountText => Count.ToString("N0");
    public string VisibilityText => Hidden ? "Hidden" : "Shown";
    public double Opacity => Hidden ? 0.48 : 1.0;
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class PreviewTabView : INotifyPropertyChanged
{
    private bool _isActive;
    private bool _isPinned;
    private bool _isDragOver;

    public PreviewTabView(string path, string fileName, bool isPinned = false)
    {
        Path = path;
        FileName = fileName;
        _isPinned = isPinned;
    }

    public string Path { get; }
    public string FileName { get; }
    public string ActiveMarker => IsActive ? "*" : "";
    public string PinMarker => IsPinned ? "P" : "p";
    public string PinToolTip => IsPinned ? "Unpin tab" : "Pin tab";
    public string AutomationName => $"Preview tab {FileName}";
    public string PinAutomationName => $"{(IsPinned ? "Unpin" : "Pin")} preview tab {FileName}";
    public string CloseAutomationName => $"Close preview tab {FileName}";
    public Brush Foreground => IsActive ? Brushes.White : Brushes.LightGray;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveMarker)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Foreground)));
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value) return;
            _isPinned = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPinned)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PinMarker)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PinToolTip)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PinAutomationName)));
        }
    }

    public bool IsDragOver
    {
        get => _isDragOver;
        set
        {
            if (_isDragOver == value) return;
            _isDragOver = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDragOver)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public readonly record struct FolderBucketIdentity(string Key, string Label);

public sealed class SharedRecentFoldersState
{
    public int Version { get; set; } = 1;
    public List<string> LastFolderSet { get; set; } = [];
    public List<List<string>> RecentFolderSets { get; set; } = [];
    public string UpdatedAtUtc { get; set; } = "";
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed record SharedRecentReadResult(
    bool Ok,
    SharedRecentFoldersState Recent,
    string? Error);

public sealed record FavoriteImportSummary(
    bool Ok,
    string Message,
    string? BrowserStatePath,
    string? SourceShape,
    int TotalEntries,
    int ImportedCount,
    int PreservedCount,
    int IgnoredZeroCount,
    int IgnoredInvalidCount,
    int MissingCount,
    int UnmatchedCount,
    int StoreCount)
{
    public static FavoriteImportSummary Failed(string? browserStatePath, string message, string? sourceShape = null)
        => new(false, message, browserStatePath, sourceShape, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record SeenImportSummary(
    bool Ok,
    string Message,
    string? BrowserStatePath,
    string? SourceShape,
    int TotalEntries,
    int ImportedCount,
    int PreservedCount,
    int IgnoredZeroCount,
    int IgnoredInvalidCount,
    int MissingCount,
    int UnmatchedCount,
    int SeenStoreCount)
{
    public static SeenImportSummary Failed(string? browserStatePath, string message, string? sourceShape = null)
        => new(false, message, browserStatePath, sourceShape, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed class LoadMetrics
{
    public string Folder { get; set; } = "";
    public int FileCount { get; set; }
    public long ScanMs { get; set; }
    public long MaterializeMs { get; set; }
    public long MetadataMs { get; set; }
    public int MetadataWorkers { get; set; }
    public int MetadataCompleted { get; set; }
    public long ThumbnailMs { get; set; }
    public int ThumbnailWorkers { get; set; }
    public int ThumbnailsCompleted { get; set; }
    public long PreviewMs { get; set; }
    public int PreviewUpdates { get; set; }
    public long PreviewDeferredDecodeMs { get; set; }
    public int PreviewDeferredDecodeCount { get; set; }
    public long ModalOpenMs { get; set; }
    public bool ModalImmediateSource { get; set; }
    public bool ModalDeferredDecode { get; set; }
    public int GridTotalItems { get; set; }
    public int GridRealizedItems { get; set; }
    public int GridDeferredItems { get; set; }
    public int GridInitialRealizationLimit { get; set; }
    public int GridRealizationBatchSize { get; set; }
    public int GridMaxRealizationCount { get; set; }
    public int GridWindowStartIndex { get; set; }
    public int GridWindowEndIndex { get; set; }
    public long TotalMs { get; set; }
    public string CompletedAtUtc { get; set; } = "";

    public static LoadMetrics Create(string folder, int fileCount, long scanMs, long materializeMs, long metadataMs, int metadataWorkers, int metadataCompleted, long thumbnailMs, int thumbnailWorkers, int thumbnailsCompleted, long previewMs, int previewUpdates, long previewDeferredDecodeMs, int previewDeferredDecodeCount, long totalMs)
        => new()
        {
            Folder = folder,
            FileCount = fileCount,
            ScanMs = scanMs,
            MaterializeMs = materializeMs,
            MetadataMs = metadataMs,
            MetadataWorkers = metadataWorkers,
            MetadataCompleted = metadataCompleted,
            ThumbnailMs = thumbnailMs,
            ThumbnailWorkers = thumbnailWorkers,
            ThumbnailsCompleted = thumbnailsCompleted,
            PreviewMs = previewMs,
            PreviewUpdates = previewUpdates,
            PreviewDeferredDecodeMs = previewDeferredDecodeMs,
            PreviewDeferredDecodeCount = previewDeferredDecodeCount,
            TotalMs = totalMs,
            CompletedAtUtc = DateTime.UtcNow.ToString("O"),
        };
}

public sealed record PreviewDecodeSmokeSnapshot(
    bool Selected,
    string? ExpectedPath,
    long SelectionImmediateMs,
    long PreviewImmediateMs,
    long DeferredDecodeMs,
    bool DeferredDecodeApplied,
    bool PreviewSourcePresent,
    bool StableLatestSelection,
    string Message)
{
    public static PreviewDecodeSmokeSnapshot NotSelected(string fileName)
        => new(false, null, 0, 0, 0, false, false, false, $"fixture image was not selected: {fileName}");
}

public sealed record PngParametersMetadata(
    string Prompt,
    string NegativePrompt,
    IReadOnlyDictionary<string, string> Settings,
    string Raw)
{
    public string? Setting(string name)
        => Settings.TryGetValue(name, out string? value) ? value : null;
}

public sealed record MetadataCopySmokeSnapshot(
    bool Copied,
    bool CopyEnabled,
    string? SelectedPath,
    string? MetadataPath,
    string CopyText);

public sealed record ModalMetadataSmokeSnapshot(
    bool ModalVisible,
    bool SidebarVisible,
    bool MetadataCurrent,
    string ActiveTab,
    bool PromptPanelVisible,
    bool NegativePanelVisible,
    bool SettingsPanelVisible,
    string Status,
    string Prompt,
    string NegativePrompt,
    string Settings,
    bool CopyMetadataEnabled,
    bool CopyPromptEnabled,
    bool CopyNegativeEnabled);

public sealed record PromptTagSearchSmokeSnapshot(
    bool Applied,
    List<string> Chips,
    string SearchQuery,
    bool ModalVisible,
    bool SearchFocused,
    bool AccessibilityReady,
    List<string> FilteredNames);

public sealed record FileDragOutSmokeSnapshot(
    bool Built,
    List<string> Paths,
    bool FileDropFormatPresent,
    string Reason,
    bool ExceedsThreshold,
    bool SurfaceContractReady);

public sealed record PngMetadataSmokeSnapshot(
    bool Selected,
    string? SelectedPath,
    bool MetadataApplied,
    string Prompt,
    string NegativePrompt,
    string Sampler,
    bool SamplerVisible,
    string Message)
{
    public static PngMetadataSmokeSnapshot NotSelected(string fileName)
        => new(false, null, false, "", "", "", false, $"fixture image was not selected: {fileName}");
}

public readonly record struct ImageDimensions(int Width, int Height);

public readonly record struct ImageMetadataLoadMetrics(
    IReadOnlyDictionary<string, ImageDimensions> Dimensions,
    IReadOnlyDictionary<string, string> Prompts,
    int Workers,
    int Completed,
    long ElapsedMs,
    int DecodeFailures)
{
    public static ImageMetadataLoadMetrics Empty => new(
        new Dictionary<string, ImageDimensions>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        0,
        0,
        0,
        0);
}

public readonly record struct ThumbnailLoadMetrics(int Total, int Workers, int Completed, long ElapsedMs);

internal readonly record struct GridZoomAnchor(string Path, double ViewportY, double CenterDistance);

internal readonly record struct DecodedThumbnail(Tile Tile, BitmapSource? Thumbnail);

// ─────────── Tile view model ───────────
public sealed class Tile : INotifyPropertyChanged
{
    public Brush? ArtBase { get; set; }
    public Brush? ArtGlow { get; set; }
    public string FileName { get; set; } = "";
    public string Group { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsRealFile { get; set; }
    public string FolderBucketKey { get; set; } = "";
    public string FolderBucketLabel { get; set; } = "";
    public bool Enhanced { get; set; }
    public string? EnhancedOutputPath { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public int ImagePixelWidth { get; set; }
    public int ImagePixelHeight { get; set; }
    public string SizeText { get; set; } = "";
    public string ModifiedText { get; set; } = "";

    private int _fav;
    private bool _unseen;

    public bool Unseen
    {
        get => _unseen;
        set
        {
            if (_unseen == value) return;
            _unseen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Unseen)));
        }
    }

    private bool _showUnseenDot;
    public bool ShowUnseenDot
    {
        get => _showUnseenDot;
        set
        {
            if (_showUnseenDot == value) return;
            _showUnseenDot = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowUnseenDot)));
        }
    }

    public int Fav
    {
        get => _fav;
        set
        {
            int clamped = Math.Clamp(value, 0, 5);
            if (_fav == clamped) return;
            _fav = clamped;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fav)));
        }
    }

    private BitmapSource? _thumbnail;
    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value)) return;
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }

    private double _cardWidth = 190;
    public double CardWidth
    {
        get => _cardWidth;
        set
        {
            if (Math.Abs(_cardWidth - value) < 0.01) return;
            _cardWidth = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardWidth)));
        }
    }

    private double _cardHeight = 285;
    public double CardHeight
    {
        get => _cardHeight;
        set
        {
            if (Math.Abs(_cardHeight - value) < 0.01) return;
            _cardHeight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardHeight)));
        }
    }

    private double _listThumbnailSize = 52;
    public double ListThumbnailSize
    {
        get => _listThumbnailSize;
        set
        {
            if (Math.Abs(_listThumbnailSize - value) < 0.01) return;
            _listThumbnailSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ListThumbnailSize)));
        }
    }

    private double _listThumbnailWidth = 52;
    public double ListThumbnailWidth
    {
        get => _listThumbnailWidth;
        set
        {
            if (Math.Abs(_listThumbnailWidth - value) < 0.01) return;
            _listThumbnailWidth = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ListThumbnailWidth)));
        }
    }

    private double _listThumbnailHeight = 52;
    public double ListThumbnailHeight
    {
        get => _listThumbnailHeight;
        set
        {
            if (Math.Abs(_listThumbnailHeight - value) < 0.01) return;
            _listThumbnailHeight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ListThumbnailHeight)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
