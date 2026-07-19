using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
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
    private static readonly HttpClient ModalEnhancementHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp", ".gif", ".tif", ".tiff",
    };
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    private const int MinParallelThumbnailCount = 32;
    private const int MaxThumbnailDecodeWorkers = 12;
    private const int MaxMetadataReadWorkers = 4;
    private const int MaxPngMetadataChunkBytes = 4 * 1024 * 1024;
    private const int MaxDecodedPixelCount = 10_000_000;
    private const int MaxDecodedLongEdge = 16_384;
    private const int DecodePixelBudgetMultiplier = 5;
    private const int DecodeLongEdgeMultiplier = 8;
    private const int SearchFilterDebounceMilliseconds = 150;
    private const int SearchStateSaveDebounceMilliseconds = 300;
    private const int MaxVirtualizedContainerSmokeCount = 512;
    private const int MaxMaterializedSelectionVisualItems = 2_048;
    private const int MaxRecentFolderSets = 12;
    private const int PersistenceLockTimeoutMilliseconds = 2_000;
    private const int PersistenceLockRetryMilliseconds = 25;
    private const long AsyncSharedStoreThresholdBytes = 1_048_576;
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
    private readonly ResettableObservableCollection<Tile> _tiles = new();
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
    private readonly Dictionary<string, FavoritePendingMutation> _pendingFavoriteMutations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SeenPendingMutation> _pendingSeenMutations = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _hiddenFolderBuckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedFolderBucketKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _enhancedOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _restoredPreviewTabPaths = [];
    private readonly SemaphoreSlim _thumbnailDecodeGate = new(MaxThumbnailDecodeWorkers, MaxThumbnailDecodeWorkers);
    private readonly ConcurrentDictionary<string, byte> _thumbnailLoadsInFlight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _thumbnailDecodeFailures = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _thumbnailViewportCts;
    private VirtualizingWrapPanel? _galleryVirtualizingPanel;
    private int _thumbnailBrowserCacheHits;
    private int _lastInitialUnseenCount;
    private int _enhancementJobsRead;
    private int _enhancedCandidateCount;
    private int _favoriteSaveAttemptCount;
    private int _sharedRecentCommitAttemptCount;
    private int _sharedRecentCommitSuccessCount;
    private bool _enhancementReadOk = true;
    private string? _enhancementReadError;
    private Rect _restoreBounds;
    private bool _fakeMaximized;
    private Func<Rect> _currentMonitorWorkArea = null!;
    private bool _initializing = true;
    private bool _suppressStateSave;
    private bool _favoritesWriteBlocked;
    private bool _seenWriteBlocked;
    private SharedStoreWriter<FavoriteDelta>? _favoriteWriter;
    private SharedStoreWriter<SeenDelta>? _seenWriter;
    private IReadOnlyList<FavoriteDelta>? _failedFavoriteBatch;
    private IReadOnlyList<SeenDelta>? _failedSeenBatch;
    private long _favoriteMutationGeneration;
    private long _seenMutationGeneration;
    private bool _favoriteWriterAdopted;
    private bool _seenWriterAdopted;
    private bool _favoritePumpScheduled;
    private bool _seenPumpScheduled;
    private bool _forceSharedWritersForSmoke;
    private ManualResetEventSlim? _favoriteWriterEnteredForSmoke;
    private ManualResetEventSlim? _favoriteWriterGateForSmoke;
    private ManualResetEventSlim? _seenWriterEnteredForSmoke;
    private ManualResetEventSlim? _seenWriterGateForSmoke;
    private ManualResetEventSlim? _favoriteReloadDrainStartedForSmoke;
    private ManualResetEventSlim? _seenReloadDrainStartedForSmoke;
    private int _failNextFavoriteWriterForSmoke;
    private int _failNextSeenWriterForSmoke;
    private bool _stateWriteBlocked;
    private double _rightPanelWidth = DefaultRightPanelWidth;
    private Dictionary<string, JsonElement>? _stateExtensionData;
    private bool _syncingSelection;
    private long _selectionVisualSyncGeneration;
    private long _queuedGridSelectionVisualSyncGeneration = -1;
    private long _queuedListSelectionVisualSyncGeneration = -1;
    private long _queuedSparseGridSelectionVisualSyncGeneration = -1;
    private long _queuedSparseListSelectionVisualSyncGeneration = -1;
    private bool _syncingFavoriteFilterControls;
    private bool _syncingDateControls;
    private bool _dateFilterMigrationPending;
    private FileDragSession? _fileDragSession;
    private bool _suppressPreviewClickAfterFileDrag;
    private bool _settingSearchQuery;
    private string? _currentFolder;
    private List<string> _currentFolderSet = [];
    private List<string> _lastFolderSet = [];
    private string? _lastSuccessfulSharedRecentFolderSetKey;
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
    private readonly DispatcherTimer _modalEnhancementPollTimer;
    private bool _modalEnhancementPolling;
    private bool _modalEnhancementRequestPending;
    private string? _modalEnhancementJobId;
    private string? _modalEnhancementJobStatus;
    private int _modalEnhancementProgress;
    private string? _modalEnhancementError;
    private long _modalEnhancementGeneration;
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _modalEnhancementSender
        = static (request, token) => ModalEnhancementHttpClient.SendAsync(request, token);
    private Func<bool>? _confirmLargeEnhancementForSmoke;
    private Func<bool>? _confirmEnhancedOutputDeleteForSmoke;
    private CancellationTokenSource? _loadCts;
    private long _loadGeneration;
    private bool _scanCancelable;
    private string _loadPhase = "idle";
    private int _scanEnumerationDelayForSmokeMs;
    private int _scanMetadataDelayForSmokeMs;
    private string? _metadataIndexPath;
    private string _metadataIndexStatus = "idle";
    private int _metadataIndexProgress;
    private int _metadataIndexCompleted;
    private int _metadataIndexTotal;
    private int _metadataIndexCacheHits;
    private int _metadataIndexCacheMisses;
    private int _catalogPreparationBatchSizeForSmoke = 256;
    private Action<string, int>? _catalogPreparationBatchHookForSmoke;
    private Action? _beforeMaterializeFilesForSmoke;
    private int _previewDecodeDelayForSmokeMs;
    private int _modalDecodeDelayForSmokeMs;
    private int _loadCtsCreatedCount;
    private int _loadCtsRetiredCount;
    private CancellationTokenSource? _modalCts;
    private TaskCompletionSource<bool>? _modalDecodeCompletion;
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _previewMetadataCts;
    private TaskCompletionSource<PreviewDecodeResult>? _previewDecodeCompletion;
    private TaskCompletionSource<PngParametersMetadata?>? _previewMetadataCompletion;
    private PngParametersMetadata? _currentPreviewMetadata;
    private string? _currentPreviewMetadataPath;
    private string _lastMetadataCopyText = "";
    private Action<string> _diagnosticsClipboardWriter = Clipboard.SetText;
    private Func<ProcessStartInfo, bool> _explorerLauncher = static startInfo =>
    {
        Process.Start(startInfo);
        return true;
    };
    private Func<ProcessStartInfo, bool> _externalFileLauncher = static startInfo
        => Process.Start(startInfo) is not null;
    private string _lastDiagnosticsCopyText = "";
    private int _previewUpdateCount;
    private long _lastCardsSelectionSyncMs;
    private long _lastRowsSelectionSyncMs;
    private long _lastEnsureGridSelectionMs;
    private long _lastCardsScrollSelectionMs;
    private long _lastRowsScrollSelectionMs;
    private long _lastPreviewSelectionMs;
    private long _lastSeenSelectionMs;
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
    private bool _syncingUnseenDotsControls;
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
    private Dictionary<ViewerKeyAction, KeyChord> _keyBindings = KeyBindingSettings.CreateDefaults();
    private Dictionary<ViewerKeyAction, KeyChord> _draftKeyBindings = KeyBindingSettings.CreateDefaults();
    private Dictionary<string, JsonElement>? _keyBindingUnknownEntries;
    private readonly Dictionary<ViewerKeyAction, Button> _keyBindingButtons = [];
    private readonly Dictionary<ViewerKeyAction, TextBlock> _keyBindingConflictTexts = [];
    private ViewerKeyAction? _recordingKeyAction;
    private string? _keyBindingCaptureError;
    private Func<ModifierKeys> _shortcutModifierProvider = static () => Keyboard.Modifiers;
    private bool _shutdownPersistenceFlushed;
    private int _shutdownPersistenceFlushCount;
    private bool _closingDrainInProgress;
    private bool _allowCloseAfterSharedDrain;
    private bool _sharedActionsDisabled;
    private int _sharedReloadBarrierDepth;
    private Tile? _pendingDeleteTile;
    private DeleteSnapshot? _pendingBulkDeleteSnapshot;
    private readonly Dictionary<string, long> _sourceRecycleGenerationByPath = new(StringComparer.OrdinalIgnoreCase);
    private long _sourceRecycleGeneration;
    private Func<string, RecycleBinDeleteResult> _recycleBinDelete = SendFileToWindowsRecycleBin;
    private Func<string, string> _resolveFinalPath = ResolveFinalPathCore;
    private Func<IReadOnlyList<string>> _protectedDeleteRoots = ResolveProtectedDeleteRoots;
    private string _deleteStatus = "";
    private Action? _statusRetryAction;
    private IInputElement? _deleteFocusBeforeDialog;
    private IInputElement? _settingsFocusBeforeDialog;
    private IInputElement? _modalFocusBeforeOverlay;
    public LoadMetrics? LastLoadMetrics { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        InitializeKeyBindingEditor();
        _currentMonitorWorkArea = ResolveCurrentMonitorWorkArea;
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
        _modalEnhancementPollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _modalEnhancementPollTimer.Tick += ModalEnhancementPollTimer_Tick;
        LandingFolderSetList.ItemsSource = _landingFolderSet;
        SidebarFolderSetList.ItemsSource = _folderBucketViews;
        RecentFolderSetList.ItemsSource = _recentFolderSetViews;
        PreviewTabList.ItemsSource = _previewTabs;
        RestoreState();
        BuildSampleTiles();
        _allTiles.AddRange(_tiles);
        _tiles.Clear();
        ApplyCardLayoutToAllTiles();

        ConfigureGalleryItemsSources();
        CardsList.ItemContainerGenerator.StatusChanged += SelectionItemContainerGenerator_StatusChanged;
        RowsList.ItemContainerGenerator.StatusChanged += SelectionItemContainerGenerator_StatusChanged;
        RowsList.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(RowsList_ScrollChanged));
        ApplyFilters(selectFirst: false);

        Loaded += (_, _) =>
        {
            AttachGalleryVirtualizationPanel();
            if (CardsList.Items.Count > 0)
                CardsList.SelectedIndex = 0;
        };
        Closing += MainWindow_Closing;
        Closed += (_, _) =>
        {
            CancelPreviewTabHoverDecode();
            CancelThumbnailViewportLoading();
            _modalEnhancementPollTimer.Stop();
            _modalEnhancementGeneration++;
        };
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

    private static System.ComponentModel.ICollectionView BuildGalleryView(ObservableCollection<Tile> source, bool groupByDate)
    {
        var cvs = new CollectionViewSource { Source = source };
        if (groupByDate)
            cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Tile.Group)));
        return cvs.View;
    }

    private bool UsesDateGrouping
        => _sortBy is SortCreatedNewestValue or SortCreatedOldestValue;

    private void ConfigureGalleryItemsSources()
    {
        bool grouped = UsesDateGrouping;
        bool previousSync = _syncingSelection;
        _syncingSelection = true;
        try
        {
            ItemsPanelTemplate cardPanel = (ItemsPanelTemplate)FindResource(
                grouped ? "GroupedVirtualizedCardItemsPanel" : "VirtualizedCardItemsPanel");
            if (!ReferenceEquals(CardsList.ItemsPanel, cardPanel))
                CardsList.ItemsPanel = cardPanel;
            // The grid always owns the complete filtered catalog. Date sections
            // are drawn by the same virtualizing panel, so grouping never turns
            // into a capped secondary collection or a 100k-container layout.
            if (!ReferenceEquals(CardsList.ItemsSource, _tiles))
                CardsList.ItemsSource = _tiles;
            ConfigureRowsItemsSource(forceGroupedView: RowsList.Visibility == Visibility.Visible);
        }
        finally
        {
            _syncingSelection = previousSync;
        }

        Dispatcher.BeginInvoke(AttachGalleryVirtualizationPanel, DispatcherPriority.Loaded);
    }

    private void ConfigureRowsItemsSource(bool forceGroupedView)
    {
        // Group construction in WPF's stock ListBox walks the complete view.
        // Keep the hidden List surface cheap; build its Created groups only when
        // the user actually switches to List mode.
        RowsList.ItemsSource = forceGroupedView && UsesDateGrouping
            ? BuildGalleryView(_tiles, groupByDate: true)
            : _tiles;
    }

    private void AttachGalleryVirtualizationPanel()
    {
        VirtualizingWrapPanel? panel = FindVisualDescendant<VirtualizingWrapPanel>(CardsList);
        if (ReferenceEquals(panel, _galleryVirtualizingPanel))
            return;

        if (_galleryVirtualizingPanel is not null)
            _galleryVirtualizingPanel.RealizedRangeChanged -= GalleryVirtualizingPanel_RealizedRangeChanged;
        _galleryVirtualizingPanel = panel;
        if (_galleryVirtualizingPanel is null)
            return;

        _galleryVirtualizingPanel.RealizedRangeChanged += GalleryVirtualizingPanel_RealizedRangeChanged;
        ScheduleThumbnailViewportRange(
            _galleryVirtualizingPanel.FirstVisibleIndex,
            _galleryVirtualizingPanel.LastVisibleIndex,
            _galleryVirtualizingPanel.FirstRealizedIndex,
            _galleryVirtualizingPanel.LastRealizedIndex);
    }

    private void GalleryVirtualizingPanel_RealizedRangeChanged(
        object? sender,
        VirtualizingWrapPanelRangeChangedEventArgs e)
    {
        ScheduleThumbnailViewportRange(
            e.FirstVisibleIndex,
            e.LastVisibleIndex,
            e.FirstRealizedIndex,
            e.LastRealizedIndex);
        QueueSparseSelectionVisualSync(CardsList, grid: true);
    }

    private void SelectionItemContainerGenerator_StatusChanged(object? sender, EventArgs e)
    {
        if (sender is not ItemContainerGenerator generator
            || generator.Status != GeneratorStatus.ContainersGenerated)
        {
            return;
        }

        if (ReferenceEquals(generator, CardsList.ItemContainerGenerator))
            QueueSparseSelectionVisualSync(CardsList, grid: true);
        else if (ReferenceEquals(generator, RowsList.ItemContainerGenerator))
            QueueSparseSelectionVisualSync(RowsList, grid: false);
    }

    private void ScheduleThumbnailViewportRange(
        int firstVisibleIndex,
        int lastVisibleIndex,
        int firstRealizedIndex,
        int lastRealizedIndex)
    {
        if (_tiles.Count == 0)
            return;

        int firstVisible = Math.Clamp(firstVisibleIndex, 0, _tiles.Count - 1);
        int lastVisible = Math.Clamp(lastVisibleIndex, firstVisible, _tiles.Count - 1);
        int firstRealized = Math.Clamp(firstRealizedIndex, 0, _tiles.Count - 1);
        int lastRealized = Math.Clamp(lastRealizedIndex, firstRealized, _tiles.Count - 1);
        if (firstVisibleIndex < 0 || lastVisibleIndex < firstVisibleIndex)
        {
            firstVisible = firstRealized;
            lastVisible = lastRealized;
        }

        var candidates = new List<Tile>(Math.Max(0, lastRealized - firstRealized + 1));
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = firstVisible; index <= lastVisible; index++)
        {
            Tile tile = _tiles[index];
            if (tile.IsRealFile && tile.Thumbnail is null && added.Add(tile.Path))
                candidates.Add(tile);
        }
        for (int index = firstRealized; index <= lastRealized; index++)
        {
            Tile tile = _tiles[index];
            if (tile.IsRealFile && tile.Thumbnail is null && added.Add(tile.Path))
                candidates.Add(tile);
        }

        if (candidates.Count == 0)
            return;

        ScheduleThumbnailCandidates(candidates);
    }

    private void ScheduleThumbnailCandidates(IReadOnlyList<Tile> candidates)
    {
        if (candidates.Count == 0)
            return;

        CancelThumbnailViewportLoading();
        CancellationTokenSource cts = _loadCts is { } loadCts
            ? CancellationTokenSource.CreateLinkedTokenSource(loadCts.Token)
            : new CancellationTokenSource();
        _thumbnailViewportCts = cts;
        _ = LoadThumbnailViewportAsync(candidates, _loadGeneration, cts);
    }

    private void RowsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (RowsList.Visibility == Visibility.Visible)
        {
            ScheduleListThumbnailViewport();
            QueueSparseSelectionVisualSync(RowsList, grid: false);
        }
    }

    private void ScheduleListThumbnailViewport()
    {
        if (RowsList.Visibility != Visibility.Visible)
            return;

        ScrollViewer? viewer = FindVisualDescendant<ScrollViewer>(RowsList);
        if (viewer is null)
            return;

        var candidates = new List<(Tile Tile, bool Visible, double Distance)>();
        foreach (ListBoxItem item in FindVisualDescendants<ListBoxItem>(RowsList))
        {
            if (item.DataContext is not Tile { IsRealFile: true, Thumbnail: null } tile)
                continue;
            try
            {
                double top = item.TransformToAncestor(viewer).Transform(new Point(0, 0)).Y;
                double bottom = top + item.ActualHeight;
                bool visible = bottom >= 0 && top <= viewer.ViewportHeight;
                double distance = Math.Abs(((top + bottom) / 2) - (viewer.ViewportHeight / 2));
                candidates.Add((tile, visible, distance));
            }
            catch (InvalidOperationException)
            {
            }
        }

        ScheduleThumbnailCandidates(candidates
            .OrderByDescending(static candidate => candidate.Visible)
            .ThenBy(static candidate => candidate.Distance)
            .Select(static candidate => candidate.Tile)
            .DistinctBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private async Task LoadThumbnailViewportAsync(
        IReadOnlyList<Tile> candidates,
        long generation,
        CancellationTokenSource cts)
    {
        try
        {
            await LoadThumbnailCandidatesAsync(candidates, generation, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            bool wasCurrent = ReferenceEquals(_thumbnailViewportCts, cts);
            if (wasCurrent)
                _thumbnailViewportCts = null;
            cts.Dispose();
            if (wasCurrent && candidates.Any(tile =>
                    tile.Thumbnail is null && !_thumbnailDecodeFailures.ContainsKey(tile.Path)))
                _ = RetryCurrentThumbnailViewportAsync(generation);
        }
    }

    private async Task RetryCurrentThumbnailViewportAsync(long generation)
    {
        await Task.Delay(120);
        if (generation != _loadGeneration
            || _thumbnailViewportCts is not null
            || _galleryVirtualizingPanel is not { } panel)
        {
            return;
        }

        if (RowsList.Visibility == Visibility.Visible)
            ScheduleListThumbnailViewport();
        else
            ScheduleThumbnailViewportRange(
                panel.FirstVisibleIndex,
                panel.LastVisibleIndex,
                panel.FirstRealizedIndex,
                panel.LastRealizedIndex);
    }

    private void CancelThumbnailViewportLoading()
    {
        CancellationTokenSource? cts = _thumbnailViewportCts;
        _thumbnailViewportCts = null;
        cts?.Cancel();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (HasUnresolvedSharedFailures)
        {
            e.Cancel = true;
            SetStatusToast(
                "Favorite or Seen changes still need Retry. The window stayed open so the recovery intent is not lost.",
                RetryFailedSharedBatches);
            return;
        }

        if (!_allowCloseAfterSharedDrain && HasPendingSharedWrites())
        {
            e.Cancel = true;
            if (!_closingDrainInProgress)
                _ = DrainThenCloseAsync();
            return;
        }

        FlushViewerStateForCloseOnce();
    }

    private bool HasPendingSharedWrites()
        => _favoriteWriter?.HasPendingOrInFlight == true
            || _seenWriter?.HasPendingOrInFlight == true;

    private bool HasUnresolvedSharedFailures
        => _failedFavoriteBatch is { Count: > 0 }
            || _failedSeenBatch is { Count: > 0 };

    private async Task DrainThenCloseAsync()
    {
        _closingDrainInProgress = true;
        _sharedActionsDisabled = true;
        SetStatusToast("Saving Favorite and Seen changes before closing...");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
        try
        {
            Task<SharedWriteStatus> favorite = _favoriteWriter is { } favoriteWriter
                ? favoriteWriter.DrainAsync(timeout.Token)
                : Task.FromResult(SharedWriteStatus.Succeeded);
            Task<SharedWriteStatus> seen = _seenWriter is { } seenWriter
                ? seenWriter.DrainAsync(timeout.Token)
                : Task.FromResult(SharedWriteStatus.Succeeded);
            SharedWriteStatus[] statuses = await Task.WhenAll(favorite, seen);
            if (statuses.All(static status => status == SharedWriteStatus.Succeeded) && !HasPendingSharedWrites())
            {
                _allowCloseAfterSharedDrain = true;
                _sharedActionsDisabled = false;
                _closingDrainInProgress = false;
                Close();
                return;
            }

            SetStatusToast("Favorite or Seen changes could not finish saving. The window stayed open; use Retry after fixing the local store.",
                RetryFailedSharedBatches);
        }
        catch (OperationCanceledException)
        {
            SetStatusToast("Favorite or Seen changes are still saving. The window stayed open; close again after the save finishes.");
        }
        finally
        {
            if (!_allowCloseAfterSharedDrain)
            {
                _sharedActionsDisabled = false;
                _closingDrainInProgress = false;
            }
        }
    }

    private void FlushViewerStateForCloseOnce()
    {
        if (_shutdownPersistenceFlushed)
            return;

        _shutdownPersistenceFlushed = true;
        _shutdownPersistenceFlushCount++;
        _searchFilterTimer.Stop();
        _searchStateSaveTimer.Stop();
        CancelPendingSearchFilter(completePending: true);
        _loadCts?.Cancel();
        _previewCts?.Cancel();
        _previewDecodeCompletion?.TrySetResult(PreviewDecodeResult.Canceled);
        _previewMetadataCts?.Cancel();
        _previewMetadataCompletion?.TrySetResult(null);
        _modalCts?.Cancel();
        _modalDecodeCompletion?.TrySetResult(false);
        _modalSingleClickGeneration++;
        _modalFeedbackTimer.Stop();
        CancelPreviewTabHoverDecode();

        // Closing flushes only the viewer state. Folder recents, favorites,
        // seen data, enhancement jobs, and source files are separate stores
        // and must not be rewritten merely because the window is closing.
        ForceSaveStateForClose();
    }

    private void ForceSaveStateForClose()
    {
        // Catalog publication suppresses incidental state writes while it
        // builds a replacement snapshot. Closing can interleave only at its
        // explicit dispatcher yields, where the current state is still stable;
        // bypass that temporary suppression so the one shutdown flush is real.
        bool previousSuppress = _suppressStateSave;
        _suppressStateSave = false;
        try
        {
            SaveState();
        }
        finally
        {
            _suppressStateSave = previousSuppress;
        }
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

    private sealed record DroppedFolderSet(List<string> Folders, int RejectedCount, string RejectionReason);

    private sealed record ModalEnhancementJobSnapshot(
        string Id,
        string SourcePath,
        string Status,
        int Progress,
        string? OutputPath,
        string? ErrorMessage);

    private sealed record EnhancementApiResponse(bool Ok, int StatusCode, JsonElement? Payload, string Error);

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

    public async Task LoadFolderSetAsync(IEnumerable<string> folders, bool commitRecent = true)
    {
        var totalWatch = Stopwatch.StartNew();
        LastLoadMetrics = null;
        _previewUpdateCount = 0;
        _previewMs = 0;
        _previewDeferredDecodeCount = 0;
        _previewDeferredDecodeMs = 0;
        CancelThumbnailViewportLoading();
        _thumbnailDecodeFailures.Clear();
        _thumbnailBrowserCacheHits = 0;
        // A fresh enumeration is allowed to discover a newly-created file at a
        // path recycled by an earlier operation. Only recycles that happen
        // after this load starts can invalidate this load's captured file list.
        _sourceRecycleGenerationByPath.Clear();
        long sourceRecycleGenerationAtStart = _sourceRecycleGeneration;
        bool modalWasVisibleBeforePublish = Modal.Visibility == Visibility.Visible;
        bool modalHadFocusBeforePublish = Modal.IsKeyboardFocusWithin;
        string? focusedPreviewTabPathBeforePublish = TryGetFocusedPreviewTab(out PreviewTabView? focusedPreviewTab)
            ? focusedPreviewTab?.Path
            : null;
        var requestedFolderSet = NormalizeFolderSet(folders);
        // Accept every explicit folder-set request as a new load intent before
        // checking availability. An all-unavailable request must still retire
        // older work; otherwise that older scan can publish after Landing has
        // already been updated for the newer request.
        CancellationTokenSource? supersededCts = _loadCts;
        _loadCts = null;
        long generation = ++_loadGeneration;
        supersededCts?.Cancel();
        var existingFolderSet = requestedFolderSet.Where(Directory.Exists).ToList();
        var unavailableFolderSet = requestedFolderSet
            .Except(existingFolderSet, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (existingFolderSet.Count == 0)
        {
            SetLandingFolderSet(requestedFolderSet);
            LandingFolderStatusText.Text = requestedFolderSet.Count == 0
                ? "No folders selected yet."
                : "Selected folders are unavailable.";
            SetPhase(landing: true);
            return;
        }

        string resolvedFolderSummary = FormatFolderSetSummary(requestedFolderSet);

        var cts = new CancellationTokenSource();
        _loadCtsCreatedCount++;
        _loadCts = cts;
        int enumerationDelayForSmokeMs = _scanEnumerationDelayForSmokeMs;
        int metadataDelayForSmokeMs = _scanMetadataDelayForSmokeMs;
        SetLandingFolderSet(requestedFolderSet);

        try
        {
        Landing.Visibility = Visibility.Visible;
        LandingPanel.IsEnabled = false;
        ScanPanel.Visibility = Visibility.Visible;
        _scanCancelable = true;
        _loadPhase = "enumeration";
        CancelScanButton.Visibility = Visibility.Visible;
        CancelScanButton.IsEnabled = true;
        ScanBar.Value = 0;
        ScanPercent.Text = "";
        ScanLabel.Text = "Scanning folders...";
        ScanMessage.Text = resolvedFolderSummary;

        IReadOnlyList<FileInfo> files;
        var scanAccessFailures = new ConcurrentQueue<string>();
        var scanBoundarySkips = new ConcurrentQueue<string>();
        var scanWatch = Stopwatch.StartNew();
        try
        {
            files = await Task.Run(
                () =>
                {
                    if (enumerationDelayForSmokeMs > 0)
                        Thread.Sleep(enumerationDelayForSmokeMs);
                    cts.Token.ThrowIfCancellationRequested();
                    var discovered = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
                    int rootWorkers = Math.Max(1, Math.Min(4, existingFolderSet.Count));
                    Parallel.ForEach(
                        existingFolderSet,
                        new ParallelOptions
                        {
                            CancellationToken = cts.Token,
                            MaxDegreeOfParallelism = rootWorkers,
                        },
                        folder =>
                        {
                            foreach (string path in EnumerateImageFiles(
                                folder,
                                scanAccessFailures,
                                scanBoundarySkips,
                                cts.Token))
                            {
                                discovered.TryAdd(path, 0);
                            }
                        });
                    return discovered.Keys
                        .Select(path => new FileInfo(path))
                        .OrderByDescending(file => file.LastWriteTimeUtc)
                        .ToList();
                },
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        scanWatch.Stop();

        if (!IsCurrentLoad(generation, cts))
            return;
        string scanTraversalWarning = BuildScanWarning(
            scanAccessFailures.Count,
            scanBoundarySkips.Count,
            unavailableFolderSet.Count,
            decodeFailureCount: 0);
        if (!string.IsNullOrWhiteSpace(scanTraversalWarning))
            SetStatusToast(scanTraversalWarning);

        // The complete file identity/order is the first usable product.  PNG
        // prompt extraction and bitmap dimension probes are intentionally not
        // on the critical path: on a 100k catalog they used to keep Landing
        // visible for many seconds after enumeration had already completed.
        // Metadata is streamed into the published tiles below while the user
        // can already scroll to any position in the catalog.
        ImageMetadataLoadMetrics metadata = ImageMetadataLoadMetrics.Empty;

        if (!IsCurrentLoad(generation, cts))
            return;
        // Enumeration already returns paths that existed while their directory
        // entry was visited. MakeFileTile guards every FileInfo access and the
        // pre-commit snapshot below removes files that disappear afterwards.
        // A second full File.Exists pass here only duplicated 100k disk probes
        // before the first usable catalog could be published.
        if (!IsCurrentLoad(generation, cts))
            return;
        string? selectedPathAfterConcurrentRecycle = _sourceRecycleGeneration > sourceRecycleGenerationAtStart
            ? SelectedTile()?.Path
            : null;
        _loadPhase = "publishing";
        ScanBar.Value = 0;
        ScanPercent.Text = "0%";
        ScanLabel.Text = $"Preparing 0 / {files.Count:N0} catalog entries";

        var materializeWatch = Stopwatch.StartNew();
        var preparedTiles = new List<Tile>(files.Count);
        long catalogPrepareMs = 0;
        long catalogPublishOtherMs = 0;
        long folderBucketViewMs = 0;
        long initialFilterMs = 0;
        long catalogStatsMs = 0;
        long catalogReadyMs = 0;
        Tile? restoredActivePreviewTile = null;
        Tile? concurrentRecycleSelectionTile = null;
        bool previousSuppress = _suppressStateSave;
        _suppressStateSave = true;
        _sharedReloadBarrierDepth++;
        try
        {
            // Keep the user's complete folder set, including temporarily
            // unavailable roots.  The catalog is built only from roots that
            // were available for this run, while Refresh can retry the same
            // explicit set after a drive reconnects or permissions change.
            // Keep interactive Favorite/Seen mutations outside the complete
            // reload transaction, including async tile preparation. Prepared
            // tiles capture these stores, so the barrier must remain active
            // until the replacement catalog and its views are committed.
            bool favoritesReady = await DrainFavoriteWriterForReloadAsync();
            bool seenReady = await DrainSeenWriterForReloadAsync();
            if (!IsCurrentLoad(generation, cts))
                return;
            if (favoritesReady)
                LoadFavorites();
            if (seenReady)
                LoadSeenState();
            LoadEnhancedState();
            double width = SizeSlider?.Value ?? 190;
            IReadOnlyDictionary<string, ImageDimensions> emptyDimensions =
                new Dictionary<string, ImageDimensions>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, string> emptyPrompts =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var folderBucketCache = new Dictionary<string, FolderBucketIdentity>(StringComparer.OrdinalIgnoreCase);
            Action? beforeMaterialize = _beforeMaterializeFilesForSmoke;
            _beforeMaterializeFilesForSmoke = null;
            beforeMaterialize?.Invoke();
            var catalogPrepareWatch = Stopwatch.StartNew();
            for (int index = 0; index < files.Count; index++)
            {
                FileInfo file = files[index];
                try
                {
                    // The source can disappear or become inaccessible after the
                    // background existence snapshot. FileInfo access is guarded,
                    // and a second background snapshot immediately before commit
                    // removes paths that disappeared during preparation.
                    preparedTiles.Add(MakeFileTile(
                        file,
                        width,
                        emptyDimensions,
                        emptyPrompts,
                        requestedFolderSet,
                        folderBucketCache));
                }
                catch (Exception ex) when (ex is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or NotSupportedException
                    or System.Security.SecurityException)
                {
                    scanAccessFailures.Enqueue(file.FullName);
                }

                int processed = index + 1;
                int batchSize = Math.Max(1, _catalogPreparationBatchSizeForSmoke);
                if (processed % batchSize == 0 || processed == files.Count)
                {
                    UpdateCatalogPreparationProgress(processed, files.Count);
                    _catalogPreparationBatchHookForSmoke?.Invoke(file.FullName, processed);
                    if (processed < files.Count)
                    {
                        await Dispatcher.Yield(DispatcherPriority.Background);
                        if (!IsCurrentLoad(generation, cts))
                            return;
                    }
                }
            }
            catalogPrepareWatch.Stop();
            catalogPrepareMs = catalogPrepareWatch.ElapsedMilliseconds;
            if (!IsCurrentLoad(generation, cts))
                return;

            var catalogPublishWatch = Stopwatch.StartNew();
            HashSet<string> existingPreparedPaths;
            try
            {
                existingPreparedPaths = await Task.Run(
                    () => SnapshotExistingPaths(preparedTiles.Select(static tile => tile.Path), cts.Token),
                    cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (!IsCurrentLoad(generation, cts))
                return;
            if (_sourceRecycleGeneration > sourceRecycleGenerationAtStart)
                selectedPathAfterConcurrentRecycle = SelectedTile()?.Path;

            var publishableTiles = new List<Tile>(preparedTiles.Count);
            foreach (Tile tile in preparedTiles)
            {
                if (WasSourceRecycledAfter(tile.Path, sourceRecycleGenerationAtStart))
                    continue;
                if (!existingPreparedPaths.Contains(tile.Path))
                {
                    scanAccessFailures.Enqueue(tile.Path);
                    continue;
                }
                publishableTiles.Add(tile);
            }
            _currentFolderSet = requestedFolderSet;
            _currentFolder = existingFolderSet.FirstOrDefault();
            SetLandingFolderSet(_currentFolderSet);
            _allTiles.Clear();
            _allTiles.AddRange(publishableTiles);
            ReportScanPublicationWarning(BuildScanWarning(
                scanAccessFailures.Count,
                scanBoundarySkips.Count,
                unavailableFolderSet.Count,
                metadata.DecodeFailures));
            _lastInitialUnseenCount = _allTiles.Count(static tile => tile.Unseen);
            PruneHiddenFolderBucketsToCurrentSet();
            var folderBucketViewWatch = Stopwatch.StartNew();
            RefreshFolderBucketViews();
            folderBucketViewWatch.Stop();
            folderBucketViewMs = folderBucketViewWatch.ElapsedMilliseconds;

            FolderPathText.Text = resolvedFolderSummary;
            var initialFilterWatch = Stopwatch.StartNew();
            ApplyFilters(selectFirst: false);
            initialFilterWatch.Stop();
            initialFilterMs = initialFilterWatch.ElapsedMilliseconds;
            restoredActivePreviewTile = ReconcilePreviewTabsWithCurrentCatalog();
            if (!string.IsNullOrWhiteSpace(selectedPathAfterConcurrentRecycle))
            {
                concurrentRecycleSelectionTile = _allTiles.FirstOrDefault(tile =>
                    string.Equals(tile.Path, selectedPathAfterConcurrentRecycle, StringComparison.OrdinalIgnoreCase));
            }
            var catalogStatsWatch = Stopwatch.StartNew();
            UpdateFolderStats();
            catalogStatsWatch.Stop();
            catalogStatsMs = catalogStatsWatch.ElapsedMilliseconds;
            catalogPublishWatch.Stop();
            catalogPublishOtherMs = Math.Max(
                0,
                catalogPublishWatch.ElapsedMilliseconds - folderBucketViewMs - initialFilterMs - catalogStatsMs);
        }
        finally
        {
            _sharedReloadBarrierDepth = Math.Max(0, _sharedReloadBarrierDepth - 1);
            _suppressStateSave = previousSuppress;
        }
        materializeWatch.Stop();
        int publishedFileCount = _allTiles.Count;

        if (publishedFileCount == 0)
        {
            ReconcileOpenSurfacesAfterCatalogReload(
                modalWasVisibleBeforePublish,
                modalHadFocusBeforePublish,
                focusedPreviewTabPathBeforePublish);
            SaveState();
            if (commitRecent)
                CommitSharedRecentFolderSet(_currentFolderSet);
            totalWatch.Stop();
            LastLoadMetrics = LoadMetrics.Create(
                resolvedFolderSummary,
                publishedFileCount,
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
            LastLoadMetrics.CatalogPrepareMs = catalogPrepareMs;
            LastLoadMetrics.CatalogPublishOtherMs = catalogPublishOtherMs;
            LastLoadMetrics.FolderBucketViewMs = folderBucketViewMs;
            LastLoadMetrics.InitialFilterMs = initialFilterMs;
            LastLoadMetrics.CatalogStatsMs = catalogStatsMs;
            UpdateGridMetrics(LastLoadMetrics);
            LandingPanel.IsEnabled = true;
            ScanBar.Value = 0;
            ScanPercent.Text = "0%";
            ScanLabel.Text = "No images found";
            ScanMessage.Text = "Choose another folder set.";
            FinishCurrentLoad(generation, cts);
            return;
        }

        SetPhase(landing: false);
        catalogReadyMs = totalWatch.ElapsedMilliseconds;
        if (concurrentRecycleSelectionTile is not null)
            _restoredSelectedPath = concurrentRecycleSelectionTile.Path;
        else if (restoredActivePreviewTile is not null)
            _restoredSelectedPath = restoredActivePreviewTile.Path;
        SelectRestoredOrFirst();
        ReconcileOpenSurfacesAfterCatalogReload(
            modalWasVisibleBeforePublish,
            modalHadFocusBeforePublish,
            focusedPreviewTabPathBeforePublish);
        SaveState();
        if (commitRecent)
            CommitSharedRecentFolderSet(_currentFolderSet);

        _loadPhase = "background-metadata";
        // Do not keep the 100k FileInfo staging catalog alive throughout the
        // lower-priority metadata pass. The Tile catalog now owns every value
        // the viewer needs after publication.
        files = Array.Empty<FileInfo>();
        preparedTiles.Clear();
        preparedTiles.TrimExcess();

        Task<ImageMetadataLoadMetrics> metadataTask = LoadImageMetadataProgressivelyAsync(
            _allTiles.ToList(),
            unavailableFolderSet,
            generation,
            cts,
            metadataDelayForSmokeMs);
        Task<ThumbnailLoadMetrics> thumbnailTask = LoadThumbnailsAsync(cts.Token);

        try
        {
            metadata = await metadataTask;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!IsCurrentLoad(generation, cts))
            return;
        if (metadata.DecodeFailures > 0)
        {
            ReportScanPublicationWarning(BuildScanWarning(
                scanAccessFailures.Count,
                scanBoundarySkips.Count,
                unavailableFolderSet.Count,
                metadata.DecodeFailures));
        }

        ThumbnailLoadMetrics thumbnails;
        try
        {
            thumbnails = await thumbnailTask;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!IsCurrentLoad(generation, cts))
            return;
        totalWatch.Stop();
        LastLoadMetrics = LoadMetrics.Create(
            resolvedFolderSummary,
            publishedFileCount,
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
        LastLoadMetrics.CatalogPrepareMs = catalogPrepareMs;
        LastLoadMetrics.CatalogPublishOtherMs = catalogPublishOtherMs;
        LastLoadMetrics.FolderBucketViewMs = folderBucketViewMs;
        LastLoadMetrics.InitialFilterMs = initialFilterMs;
        LastLoadMetrics.CatalogStatsMs = catalogStatsMs;
        LastLoadMetrics.CatalogReadyMs = catalogReadyMs;
        LastLoadMetrics.MetadataCacheHits = metadata.CacheHits;
        LastLoadMetrics.MetadataCacheMisses = metadata.CacheMisses;
        LastLoadMetrics.MetadataIndexReadMs = metadata.IndexReadMs;
        LastLoadMetrics.MetadataIndexWriteMs = metadata.IndexWriteMs;
        LastLoadMetrics.MetadataIndexLoadState = metadata.IndexLoadState;
        LastLoadMetrics.MetadataIndexSaveSucceeded = metadata.IndexSaveSucceeded;
        LastLoadMetrics.MetadataIndexWritten = metadata.IndexWritten;
        LastLoadMetrics.MetadataIndexSaveError = metadata.IndexSaveError;
        UpdateGridMetrics(LastLoadMetrics);
        FinishCurrentLoad(generation, cts);
        }
        finally
        {
            RetireLoad(generation, cts);
        }
    }

    private bool IsCurrentLoad(long generation, CancellationTokenSource cts)
        => generation == _loadGeneration
            && ReferenceEquals(_loadCts, cts)
            && !cts.IsCancellationRequested;

    private void FinishCurrentLoad(long generation, CancellationTokenSource cts)
    {
        if (generation != _loadGeneration || !ReferenceEquals(_loadCts, cts))
            return;

        _scanCancelable = false;
        _loadPhase = "idle";
        CancelScanButton.Visibility = Visibility.Collapsed;
        CancelScanButton.IsEnabled = false;
        _loadCts = null;
    }

    private void RetireLoad(long generation, CancellationTokenSource cts)
    {
        // Every return path (success, explicit cancel, supersession, close, or
        // exception) owns exactly one disposal. A stale run must not touch UI.
        if (generation == _loadGeneration && ReferenceEquals(_loadCts, cts))
        {
            _scanCancelable = false;
            _loadCts = null;
        }
        cts.Dispose();
        _loadCtsRetiredCount++;
    }

    private bool CancelActiveScan()
    {
        if (!_scanCancelable || _loadCts is null || _loadCts.IsCancellationRequested)
            return false;

        CancellationTokenSource canceled = _loadCts;
        bool canceledMetadata = string.Equals(_loadPhase, "background-metadata", StringComparison.Ordinal);
        _scanCancelable = false;
        _loadPhase = "canceled";
        _loadGeneration++;
        _loadCts = null;
        canceled.Cancel();
        CancelScanButton.Visibility = Visibility.Collapsed;
        CancelScanButton.IsEnabled = false;
        Landing.Visibility = Visibility.Visible;
        LandingPanel.IsEnabled = true;
        ScanPanel.Visibility = Visibility.Visible;
        ScanBar.Value = 0;
        ScanPercent.Text = "";
        ScanLabel.Text = "Scan canceled";
        ScanMessage.Text = "Folder set kept. Ready to scan again.";
        if (canceledMetadata)
        {
            _metadataIndexStatus = "canceled";
            RenderMetadataIndexProgress(
                "Prompt metadata canceled - the last complete index was kept.",
                _metadataIndexProgress,
                showProgress: false);
        }
        RefreshLandingFolderSetUi();
        Dispatcher.BeginInvoke(OpenFolderSetButton.Focus, DispatcherPriority.Input);
        return true;
    }

    private void ReportScanAccessFailures(int skippedCount)
        => ReportScanTraversalWarnings(skippedCount, 0, 0);

    private void ReportScanTraversalWarnings(int accessFailureCount, int boundarySkipCount, int unavailableRootCount)
    {
        string warning = BuildScanWarning(accessFailureCount, boundarySkipCount, unavailableRootCount, decodeFailureCount: 0);
        if (!string.IsNullOrWhiteSpace(warning))
            SetStatusToast(warning);
    }

    private static string BuildScanWarning(int accessFailureCount, int boundarySkipCount, int unavailableRootCount, int decodeFailureCount)
    {
        var messages = new List<string>();
        if (unavailableRootCount > 0)
            messages.Add($"{unavailableRootCount:N0} selected root(s) were unavailable and skipped. The folder set was kept so Refresh can retry them.");
        if (accessFailureCount > 0)
            messages.Add($"Some folders could not be scanned. {accessFailureCount:N0} location(s) became unavailable or access was denied; fix access and refresh the folder.");
        if (boundarySkipCount > 0)
            messages.Add($"{boundarySkipCount:N0} junction or symbolic-link location(s) were not followed outside the selected folder tree.");
        if (decodeFailureCount > 0)
            messages.Add($"{decodeFailureCount:N0} image file(s) could not be decoded. They remain listed; refresh after fixing the files.");
        return string.Join(" ", messages);
    }

    private void ReportScanPublicationWarning(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return;

        bool preservePersistenceRecovery = _deleteStatus.Contains("could not be saved", StringComparison.OrdinalIgnoreCase)
            || _deleteStatus.Contains("is busy in another PhotoViewer window", StringComparison.OrdinalIgnoreCase);
        if (preservePersistenceRecovery)
        {
            SetStatusToast($"{_deleteStatus} {warning}", _statusRetryAction);
            return;
        }

        SetStatusToast(warning);
    }

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
        List<Tile> snapshot;
        if (_galleryVirtualizingPanel is { FirstRealizedIndex: >= 0 } panel)
        {
            int first = Math.Clamp(panel.FirstRealizedIndex, 0, Math.Max(0, _tiles.Count - 1));
            int last = Math.Clamp(panel.LastRealizedIndex, first, Math.Max(first, _tiles.Count - 1));
            snapshot = _tiles.Skip(first).Take(last - first + 1)
                .Where(static tile => tile.IsRealFile && tile.Thumbnail is null)
                .ToList();
        }
        else
        {
            // The first layout pass has not reported its range yet. Prime one
            // viewport-sized slice; the range event immediately supersedes it
            // if the user jumps elsewhere.
            snapshot = _tiles.Take(64)
                .Where(static tile => tile.IsRealFile && tile.Thumbnail is null)
                .ToList();
        }

        return await LoadThumbnailCandidatesAsync(snapshot, _loadGeneration, token);
    }

    private async Task<ThumbnailLoadMetrics> LoadThumbnailCandidatesAsync(
        IReadOnlyList<Tile> candidates,
        long generation,
        CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        int total = candidates.Count;
        int completed = 0;
        int workers = Math.Max(1, Math.Min(MaxThumbnailDecodeWorkers, total));
        if (total == 0)
            return new ThumbnailLoadMetrics(0, 0, 0, 0);

        try
        {
            Task[] loads = candidates.Select(async tile =>
            {
                if (tile.Thumbnail is not null
                    || _thumbnailDecodeFailures.ContainsKey(tile.Path)
                    || !_thumbnailLoadsInFlight.TryAdd(tile.Path, 0))
                {
                    return;
                }
                try
                {
                    await _thumbnailDecodeGate.WaitAsync(token);
                    BitmapSource? thumbnail;
                    try
                    {
                        int decodeWidth = (int)Math.Clamp(tile.CardWidth * 1.4, 180, 520);
                        thumbnail = await Task.Run(() => LoadThumbnailBitmap(tile, decodeWidth), token);
                    }
                    finally
                    {
                        _thumbnailDecodeGate.Release();
                    }

                    token.ThrowIfCancellationRequested();
                    if (generation != _loadGeneration)
                        return;

                    if (thumbnail is null)
                        _thumbnailDecodeFailures.TryAdd(tile.Path, 0);
                    else
                        tile.Thumbnail = thumbnail;
                    Interlocked.Increment(ref completed);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    _thumbnailDecodeFailures.TryAdd(tile.Path, 0);
                    Interlocked.Increment(ref completed);
                }
                finally
                {
                    _thumbnailLoadsInFlight.TryRemove(tile.Path, out _);
                }
            }).ToArray();

            await Task.WhenAll(loads);
        }
        catch (OperationCanceledException)
        {
            return new ThumbnailLoadMetrics(total, workers, completed, watch.ElapsedMilliseconds);
        }
        finally
        {
            watch.Stop();
        }

        UpdateThumbnailProgress(completed, total);
        return new ThumbnailLoadMetrics(total, workers, completed, watch.ElapsedMilliseconds);
    }

    private BitmapSource? LoadThumbnailBitmap(Tile tile, int decodePixelWidth)
    {
        try
        {
            foreach (string cachePath in GetBrowserThumbnailCachePaths(tile))
            {
                if (!File.Exists(cachePath))
                    continue;
                BitmapSource? cached = LoadBitmap(cachePath, decodePixelWidth);
                if (cached is not null)
                {
                    Interlocked.Increment(ref _thumbnailBrowserCacheHits);
                    // The shared browser cache is immutable/versioned by source
                    // mtime, so reading it never mutates user cache or source.
                    return cached;
                }
            }
        }
        catch
        {
            // A missing/unsupported WebP codec or malformed cache entry must
            // fall back to the source without deleting the browser cache.
        }

        return LoadBitmap(tile.Path, decodePixelWidth);
    }

    private static IReadOnlyList<string> GetBrowserThumbnailCachePaths(Tile tile)
    {
        DateTime modifiedUtc = tile.ModifiedUtc.Kind == DateTimeKind.Utc
            ? tile.ModifiedUtc
            : tile.ModifiedUtc.ToUniversalTime();
        double preciseMilliseconds = (modifiedUtc.Ticks - DateTime.UnixEpoch.Ticks) / (double)TimeSpan.TicksPerMillisecond;
        string preciseVersion = preciseMilliseconds.ToString("R", CultureInfo.InvariantCulture);
        string integerVersion = new DateTimeOffset(modifiedUtc).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        string resolvedPath = Path.GetFullPath(tile.Path);
        string cacheRoot = Path.Combine(ResolveSharedProjectRoot(), ".cache", "thumbs");
        var paths = new List<string>(2)
        {
            Path.Combine(cacheRoot, $"{Base64Url($"{resolvedPath}|{preciseVersion}")}.webp"),
        };
        if (!string.Equals(preciseVersion, integerVersion, StringComparison.Ordinal))
            paths.Add(Path.Combine(cacheRoot, $"{Base64Url($"{resolvedPath}|{integerVersion}")}.webp"));
        return paths;
    }

    private static string Base64Url(string key)
    {
        string base64Url = Convert.ToBase64String(Encoding.UTF8.GetBytes(key))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return base64Url;
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

    private void UpdateCatalogPreparationProgress(int done, int total)
    {
        if (total <= 0)
            return;

        double progress = done * 100.0 / total;
        ScanBar.Value = progress;
        ScanPercent.Text = $"{(int)progress}%";
        ScanLabel.Text = $"Preparing {done:N0} / {total:N0} catalog entries";
    }

    private static HashSet<string> SnapshotExistingPaths(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
                existing.Add(path);
        }
        return existing;
    }

    private static IEnumerable<string> EnumerateImageFiles(
        string root,
        ConcurrentQueue<string>? accessFailures = null,
        ConcurrentQueue<string>? boundarySkips = null,
        CancellationToken cancellationToken = default)
    {
        var pending = new Stack<string>();
        var images = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string scanRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        pending.Push(scanRoot);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string folder;
            try
            {
                folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(pending.Pop()));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                accessFailures?.Enqueue(root);
                continue;
            }
            if (!IsPathInside(folder, scanRoot))
            {
                boundarySkips?.Enqueue(folder);
                continue;
            }
            if (!visited.Add(folder))
                continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!SupportedImageExtensions.Contains(Path.GetExtension(file)))
                        continue;
                    try
                    {
                        string filePath = Path.GetFullPath(file);
                        FileAttributes attributes = File.GetAttributes(filePath);
                        if ((attributes & FileAttributes.ReparsePoint) != 0 || !IsPathInside(filePath, scanRoot))
                        {
                            boundarySkips?.Enqueue(filePath);
                            continue;
                        }
                        images.Add(filePath);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
                    {
                        accessFailures?.Enqueue(file);
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                accessFailures?.Enqueue(folder);
            }
            try
            {
                foreach (var child in Directory.EnumerateDirectories(folder))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        string childPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(child));
                        FileAttributes attributes = File.GetAttributes(childPath);
                        if ((attributes & FileAttributes.ReparsePoint) != 0 || !IsPathInside(childPath, scanRoot))
                        {
                            boundarySkips?.Enqueue(childPath);
                            continue;
                        }
                        pending.Push(childPath);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
                    {
                        accessFailures?.Enqueue(child);
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                accessFailures?.Enqueue(folder);
            }
        }

        return images;
    }

    public static ScanBoundarySmokeSnapshot EnumerateImageFilesForSmoke(string root)
    {
        var accessFailures = new ConcurrentQueue<string>();
        var boundarySkips = new ConcurrentQueue<string>();
        var watch = Stopwatch.StartNew();
        List<string> images = EnumerateImageFiles(root, accessFailures, boundarySkips)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        watch.Stop();
        return new ScanBoundarySmokeSnapshot(images, accessFailures.ToList(), boundarySkips.ToList(), watch.ElapsedMilliseconds);
    }

    private Tile MakeFileTile(
        FileInfo file,
        double width,
        IReadOnlyDictionary<string, ImageDimensions> dimensions,
        IReadOnlyDictionary<string, string> prompts,
        IReadOnlyList<string> folderSet,
        IDictionary<string, FolderBucketIdentity> folderBucketCache)
    {
        var modified = file.LastWriteTime;
        var created = file.CreationTime;
        int paletteIndex = file.FullName.GetHashCode(StringComparison.OrdinalIgnoreCase) & int.MaxValue;
        bool enhanced = TryGetEnhancedOutputForPath(file.FullName, out string? enhancedOutputPath);
        dimensions.TryGetValue(file.FullName, out var imageSize);
        prompts.TryGetValue(file.FullName, out string? indexedPrompt);
        string folderBucketCacheKey = file.DirectoryName ?? file.FullName;
        if (!folderBucketCache.TryGetValue(folderBucketCacheKey, out FolderBucketIdentity folderBucket))
        {
            folderBucket = ResolveFolderBucket(file.FullName, folderSet);
            folderBucketCache[folderBucketCacheKey] = folderBucket;
        }
        var tile = new Tile
        {
            ArtBase = MakeBaseBrush(paletteIndex),
            ArtGlow = MakeGlowBrush(paletteIndex),
            FileName = file.Name,
            Fav = FavoriteLevelForPath(file.FullName),
            Unseen = !SeenStateContains(file.FullName),
            ShowUnseenDot = _showUnseenDots && !SeenStateContains(file.FullName),
            // Date sections are enabled only for Created sorts, so their label
            // must use the same timestamp as the Created order/filter.
            Group = FormatGroup(created),
            CardWidth = width,
            ModifiedUtc = file.LastWriteTimeUtc,
            CreatedUtc = file.CreationTimeUtc,
            SourceLength = file.Length,
            SourceLastWriteUtcTicks = file.LastWriteTimeUtc.Ticks,
            SourceCreationUtcTicks = file.CreationTimeUtc.Ticks,
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

    private static FolderBucketIdentity ResolveFolderBucket(string filePath, IReadOnlyList<string> folderSet)
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
        foreach (string root in folderSet)
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

            return ProjectCachePath("favorites.json");
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

    /// <summary>
    /// Resolve the checkout that owns the shared browser/WPF stores. A Codex
    /// linked worktree has its own project.toml but its .git file points back
    /// to the main checkout. Using the worktree-local .cache silently forks
    /// favorites, seen state, recent folders, and enhancement history.
    /// </summary>
    private static string ResolveSharedProjectRoot()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            string? projectRoot = FindProjectRoot(start);
            if (projectRoot is null)
                continue;

            return ResolveMainCheckoutRoot(projectRoot) ?? projectRoot;
        }

        return Environment.CurrentDirectory;
    }

    private static string? ResolveMainCheckoutRoot(string projectRoot)
    {
        try
        {
            string dotGitPath = Path.Combine(projectRoot, ".git");
            if (Directory.Exists(dotGitPath))
                return projectRoot;
            if (!File.Exists(dotGitPath))
                return null;

            string gitDirPointer = File.ReadLines(dotGitPath).FirstOrDefault()?.Trim() ?? "";
            const string gitDirPrefix = "gitdir:";
            if (!gitDirPointer.StartsWith(gitDirPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            string gitDirValue = gitDirPointer[gitDirPrefix.Length..].Trim();
            string gitDir = Path.GetFullPath(Path.IsPathRooted(gitDirValue)
                ? gitDirValue
                : Path.Combine(projectRoot, gitDirValue));
            string commonDirPath = Path.Combine(gitDir, "commondir");
            if (!File.Exists(commonDirPath))
                return null;

            string commonDirValue = File.ReadLines(commonDirPath).FirstOrDefault()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(commonDirValue))
                return null;
            string commonGitDir = Path.GetFullPath(Path.IsPathRooted(commonDirValue)
                ? commonDirValue
                : Path.Combine(gitDir, commonDirValue));
            string? candidate = Directory.GetParent(commonGitDir)?.FullName;
            if (candidate is not null
                && File.Exists(Path.Combine(candidate, "project.toml"))
                && Directory.Exists(Path.Combine(candidate, "local-native")))
            {
                return candidate;
            }
        }
        catch
        {
            // A malformed or unavailable git administration file must not
            // prevent startup. The caller falls back to the current checkout.
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
                    TryCleanupPersistenceTempResidue(targetPath);
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
                // A zero-timeout UI write still gets one immediate create-new
                // retry when it successfully removes a >30 s crash orphan.
                // Without this, the first user action only deleted the stale
                // lock and then reported a false busy failure.
                if (TryRemoveStalePersistenceLock(lockPath))
                    continue;
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

    private static bool TryRemoveStalePersistenceLock(string lockPath)
    {
        try
        {
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(lockPath) <= PersistenceLockStaleAfter)
                return false;

            File.Delete(lockPath);
            return !File.Exists(lockPath);
        }
        catch
        {
            // A fresh or unreadable lock is authoritative until the shared stale limit.
            return false;
        }
    }

    private static void TryCleanupPersistenceTempResidue(string targetPath)
    {
        try
        {
            string? directory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            string fileName = Path.GetFileName(targetPath);
            string browserPrefix = Path.GetFileNameWithoutExtension(targetPath) + "-";
            foreach (string candidate in Directory.EnumerateFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly))
            {
                string candidateName = Path.GetFileName(candidate);
                bool wpfResidue = candidateName.StartsWith($".{fileName}.", StringComparison.OrdinalIgnoreCase);
                bool browserResidue = candidateName.StartsWith(browserPrefix, StringComparison.OrdinalIgnoreCase);
                if (!wpfResidue && !browserResidue)
                    continue;
                try { File.Delete(candidate); } catch { }
            }
        }
        catch
        {
            // Residue cleanup is best effort; the guarded atomic write remains authoritative.
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

    private sealed record FavoritePendingMutation(int DurableLevel, int DesiredLevel, long Generation);
    private sealed record SeenPendingMutation(bool DurableSeen, bool WasUnseen, bool ShowedUnseenDot, long Generation);
    private enum SharedStoreKind { Favorite, Seen }

    private static long SafeStoreLength(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch { return 0; }
    }

    private bool ShouldUseFavoriteWriter()
    {
        if (_favoriteWriterAdopted)
            return true;
        if (!_forceSharedWritersForSmoke && SafeStoreLength(ResolvedFavoritesPath) < AsyncSharedStoreThresholdBytes)
            return false;

        _favoriteWriterAdopted = true;
        EnsureFavoriteWriter();
        return true;
    }

    private bool ShouldUseSeenWriter()
    {
        if (_seenWriterAdopted)
            return true;
        if (!_forceSharedWritersForSmoke && SafeStoreLength(ResolvedSeenPath) < AsyncSharedStoreThresholdBytes)
            return false;

        _seenWriterAdopted = true;
        EnsureSeenWriter();
        return true;
    }

    private SharedStoreWriter<FavoriteDelta> EnsureFavoriteWriter()
    {
        return _favoriteWriter ??= new SharedStoreWriter<FavoriteDelta>(
            WriteFavoriteBatchForActor,
            result => Dispatcher.InvokeAsync(() => ApplyFavoriteWriteResult(result), DispatcherPriority.Background).Task);
    }

    private SharedStoreWriter<SeenDelta> EnsureSeenWriter()
    {
        return _seenWriter ??= new SharedStoreWriter<SeenDelta>(
            WriteSeenBatchForActor,
            result => Dispatcher.InvokeAsync(() => ApplySeenWriteResult(result), DispatcherPriority.Background).Task);
    }

    private SharedWriteResult<FavoriteDelta> WriteFavoriteBatchForActor(IReadOnlyList<FavoriteDelta> batch)
    {
        _favoriteWriterEnteredForSmoke?.Set();
        if (_favoriteWriterGateForSmoke is { } gate && !gate.Wait(TimeSpan.FromSeconds(10)))
            return new SharedWriteResult<FavoriteDelta>(SharedWriteStatus.Failed, batch, "favorite smoke gate timed out");
        if (Interlocked.Exchange(ref _failNextFavoriteWriterForSmoke, 0) != 0)
            return new SharedWriteResult<FavoriteDelta>(SharedWriteStatus.Failed, batch, "injected Favorite write failure");
        return WriteFavoriteBatch(ResolvedFavoritesPath, batch);
    }

    private SharedWriteResult<SeenDelta> WriteSeenBatchForActor(IReadOnlyList<SeenDelta> batch)
    {
        _seenWriterEnteredForSmoke?.Set();
        if (_seenWriterGateForSmoke is { } gate && !gate.Wait(TimeSpan.FromSeconds(10)))
            return new SharedWriteResult<SeenDelta>(SharedWriteStatus.Failed, batch, "seen smoke gate timed out");
        if (Interlocked.Exchange(ref _failNextSeenWriterForSmoke, 0) != 0)
            return new SharedWriteResult<SeenDelta>(SharedWriteStatus.Failed, batch, "injected Seen write failure");
        return WriteSeenBatch(ResolvedSeenPath, batch);
    }

    private static SharedWriteResult<FavoriteDelta> WriteFavoriteBatch(string path, IReadOnlyList<FavoriteDelta> batch)
    {
        using PersistenceLockLease? lease = TryAcquirePersistenceLock(path, PersistenceLockTimeoutMilliseconds);
        if (lease is null)
        {
            return new SharedWriteResult<FavoriteDelta>(
                File.Exists(path + ".lock") ? SharedWriteStatus.Busy : SharedWriteStatus.Failed,
                batch,
                "Favorite persistence lock was unavailable");
        }

        var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!TryLoadFavoritesFile(path, merged))
            return new SharedWriteResult<FavoriteDelta>(SharedWriteStatus.Protected, batch, "Favorite JSON is invalid or unsupported");

        foreach (FavoriteDelta delta in batch)
        {
            if (delta.DesiredLevel > 0)
                merged[delta.Path] = Math.Clamp(delta.DesiredLevel, 1, 5);
            else
                merged.Remove(delta.Path);
        }

        var ordered = merged
            .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase);
        string json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
        return TryWriteAtomicText(path, json)
            ? new SharedWriteResult<FavoriteDelta>(SharedWriteStatus.Succeeded, batch)
            : new SharedWriteResult<FavoriteDelta>(SharedWriteStatus.Failed, batch, "Favorite atomic replacement failed");
    }

    private static SharedWriteResult<SeenDelta> WriteSeenBatch(string path, IReadOnlyList<SeenDelta> batch)
    {
        using PersistenceLockLease? lease = TryAcquirePersistenceLock(path, PersistenceLockTimeoutMilliseconds);
        if (lease is null)
        {
            return new SharedWriteResult<SeenDelta>(
                File.Exists(path + ".lock") ? SharedWriteStatus.Busy : SharedWriteStatus.Failed,
                batch,
                "Seen persistence lock was unavailable");
        }

        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!TryLoadSeenFile(path, merged))
            return new SharedWriteResult<SeenDelta>(SharedWriteStatus.Protected, batch, "Seen JSON is invalid or unsupported");

        foreach (SeenDelta delta in batch)
            merged.Add(delta.Path);

        var ordered = merged
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static item => item, static _ => true, StringComparer.OrdinalIgnoreCase);
        string json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
        return TryWriteAtomicText(path, json)
            ? new SharedWriteResult<SeenDelta>(SharedWriteStatus.Succeeded, batch)
            : new SharedWriteResult<SeenDelta>(SharedWriteStatus.Failed, batch, "Seen atomic replacement failed");
    }

    private void ScheduleFavoriteWriterPump()
    {
        if (_favoritePumpScheduled)
            return;
        _favoritePumpScheduled = true;
        _ = Dispatcher.InvokeAsync(() =>
        {
            _favoritePumpScheduled = false;
            _ = RunFavoriteWriterPumpAsync();
        }, DispatcherPriority.SystemIdle);
    }

    private void ScheduleSeenWriterPump()
    {
        if (_seenPumpScheduled)
            return;
        _seenPumpScheduled = true;
        _ = Dispatcher.InvokeAsync(() =>
        {
            _seenPumpScheduled = false;
            _ = RunSeenWriterPumpAsync();
        }, DispatcherPriority.SystemIdle);
    }

    private async Task RunFavoriteWriterPumpAsync()
    {
        SharedStoreWriter<FavoriteDelta>? writer = _favoriteWriter;
        if (writer is null)
            return;
        try
        {
            SharedWriteStatus status = await writer.DrainAsync(CancellationToken.None);
            if (status != SharedWriteStatus.Succeeded && writer.HasPendingOrInFlight)
                ScheduleFavoriteWriterPump();
        }
        catch (Exception ex) { SetStatusToast($"Favorites could not be saved: {ex.Message}", RetryFailedFavoriteBatch); }
    }

    private async Task RunSeenWriterPumpAsync()
    {
        SharedStoreWriter<SeenDelta>? writer = _seenWriter;
        if (writer is null)
            return;
        try
        {
            SharedWriteStatus status = await writer.DrainAsync(CancellationToken.None);
            if (status != SharedWriteStatus.Succeeded && writer.HasPendingOrInFlight)
                ScheduleSeenWriterPump();
        }
        catch (Exception ex) { SetStatusToast($"Seen state could not be saved: {ex.Message}", RetryFailedSeenBatch); }
    }

    private async Task<bool> DrainFavoriteWriterForReloadAsync()
    {
        _favoriteReloadDrainStartedForSmoke?.Set();
        if (_favoriteWriter is not { HasPendingOrInFlight: true } writer)
            return true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
        try
        {
            return await writer.DrainAsync(timeout.Token) == SharedWriteStatus.Succeeded;
        }
        catch (OperationCanceledException)
        {
            SetStatusToast("Favorites are still saving. The current in-memory Favorite state was kept.", RetryFailedFavoriteBatch);
            return false;
        }
    }

    private async Task<bool> DrainSeenWriterForReloadAsync()
    {
        _seenReloadDrainStartedForSmoke?.Set();
        if (_seenWriter is not { HasPendingOrInFlight: true } writer)
            return true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
        try
        {
            return await writer.DrainAsync(timeout.Token) == SharedWriteStatus.Succeeded;
        }
        catch (OperationCanceledException)
        {
            SetStatusToast("Seen state is still saving. The current in-memory Seen state was kept.", RetryFailedSeenBatch);
            return false;
        }
    }

    private void ApplyFavoriteWriteResult(SharedWriteResult<FavoriteDelta> result)
    {
        if (result.Status == SharedWriteStatus.Succeeded)
        {
            bool committedCurrent = false;
            foreach (FavoriteDelta delta in result.Batch)
            {
                if (!_pendingFavoriteMutations.TryGetValue(delta.Path, out FavoritePendingMutation? current))
                    continue;
                if (current.Generation == delta.Generation)
                {
                    _pendingFavoriteMutations.Remove(delta.Path);
                    committedCurrent = true;
                }
                else if (current.Generation > delta.Generation)
                    _pendingFavoriteMutations[delta.Path] = current with { DurableLevel = delta.DesiredLevel };
            }
            if (committedCurrent && _pendingFavoriteMutations.Count == 0)
                SetStatusToast("Favorites saved.");
            return;
        }

        var failed = new List<FavoriteDelta>();
        foreach (FavoriteDelta delta in result.Batch)
        {
            if (!_pendingFavoriteMutations.TryGetValue(delta.Path, out FavoritePendingMutation? current)
                || current.Generation != delta.Generation)
            {
                continue;
            }

            if (current.DurableLevel > 0)
                _favorites[delta.Path] = current.DurableLevel;
            else
                _favorites.Remove(delta.Path);
            SetTileFavoriteLevel(delta.Path, current.DurableLevel);
            _pendingFavoriteMutations.Remove(delta.Path);
            failed.Add(delta with { DurableBefore = current.DurableLevel });
        }

        if (failed.Count > 0)
        {
            ApplyFilters();
            SyncSelectionActionSurface();
            RefreshModalFavoriteSurface();
        }

        if (result.Status == SharedWriteStatus.Protected)
        {
            _failedFavoriteBatch = null;
            _favoritesWriteBlocked = true;
            ReportPersistenceRefusal("Favorites", ResolvedFavoritesPath, protectedFile: true);
        }
        else
        {
            _failedFavoriteBatch = failed;
            ReportPersistenceRefusal("Favorites", ResolvedFavoritesPath, retryAction: failed.Count > 0 ? RetryFailedFavoriteBatch : null);
        }
    }

    private void ApplySeenWriteResult(SharedWriteResult<SeenDelta> result)
    {
        if (result.Status == SharedWriteStatus.Succeeded)
        {
            bool applyUnseenFilter = false;
            foreach (SeenDelta delta in result.Batch)
            {
                if (!_pendingSeenMutations.TryGetValue(delta.Path, out SeenPendingMutation? current))
                    continue;
                if (current.Generation == delta.Generation)
                {
                    _pendingSeenMutations.Remove(delta.Path);
                    applyUnseenFilter = true;
                }
                else if (current.Generation > delta.Generation)
                {
                    _pendingSeenMutations[delta.Path] = current with { DurableSeen = true };
                }
            }

            if (applyUnseenFilter && UnseenOnlyFilter?.IsChecked == true)
            {
                Tile? selected = SelectedTile();
                ApplyFilters(selectFirst: false);
                if (selected is not null && !_tiles.Contains(selected))
                    SelectTile(null);
            }
            else if (applyUnseenFilter)
            {
                UpdateHeaderStats();
            }
            return;
        }

        var failed = new List<SeenDelta>();
        foreach (SeenDelta delta in result.Batch)
        {
            if (!_pendingSeenMutations.TryGetValue(delta.Path, out SeenPendingMutation? current)
                || current.Generation != delta.Generation)
            {
                continue;
            }

            if (current.DurableSeen)
                _seenPaths.Add(delta.Path);
            else
                _seenPaths.Remove(delta.Path);
            RestoreTileSeenState(delta.Path, current.WasUnseen, current.ShowedUnseenDot);
            _pendingSeenMutations.Remove(delta.Path);
            failed.Add(delta with
            {
                DurableSeenBefore = current.DurableSeen,
                WasUnseen = current.WasUnseen,
                ShowedUnseenDot = current.ShowedUnseenDot,
            });
        }
        UpdateHeaderStats();

        if (result.Status == SharedWriteStatus.Protected)
        {
            _failedSeenBatch = null;
            _seenWriteBlocked = true;
            ReportPersistenceRefusal("Seen state", ResolvedSeenPath, protectedFile: true);
        }
        else
        {
            _failedSeenBatch = failed;
            ReportPersistenceRefusal("Seen state", ResolvedSeenPath, retryAction: failed.Count > 0 ? RetryFailedSeenBatch : null);
        }
    }

    private void RetryFailedFavoriteBatch()
    {
        if (!CanStartSharedStateAction(SharedStoreKind.Favorite, RetryFailedFavoriteBatch, allowFailedRecovery: true))
            return;

        IReadOnlyList<FavoriteDelta>? failed = _failedFavoriteBatch;
        _failedFavoriteBatch = null;
        if (failed is null || failed.Count == 0 || _favoritesWriteBlocked)
        {
            SetStatusToast("Favorite retry is no longer available.");
            return;
        }

        SharedStoreWriter<FavoriteDelta> writer = EnsureFavoriteWriter();
        foreach (FavoriteDelta previous in failed)
        {
            int durable = FavoriteLevelForPath(previous.Path);
            long generation = ++_favoriteMutationGeneration;
            _pendingFavoriteMutations[previous.Path] = new FavoritePendingMutation(durable, previous.DesiredLevel, generation);
            if (previous.DesiredLevel > 0)
                _favorites[previous.Path] = previous.DesiredLevel;
            else
                _favorites.Remove(previous.Path);
            SetTileFavoriteLevel(previous.Path, previous.DesiredLevel);
            writer.Enqueue(new FavoriteDelta(previous.Path, durable, previous.DesiredLevel, generation));
        }
        ApplyFilters();
        SyncSelectionActionSurface();
        RefreshModalFavoriteSurface();
        ScheduleFavoriteWriterPump();
    }

    private void RetryFailedSeenBatch()
    {
        if (!CanStartSharedStateAction(SharedStoreKind.Seen, RetryFailedSeenBatch, allowFailedRecovery: true))
            return;

        IReadOnlyList<SeenDelta>? failed = _failedSeenBatch;
        _failedSeenBatch = null;
        if (failed is null || failed.Count == 0 || _seenWriteBlocked)
        {
            SetStatusToast("Seen-state retry is no longer available.");
            return;
        }

        SharedStoreWriter<SeenDelta> writer = EnsureSeenWriter();
        foreach (SeenDelta previous in failed)
        {
            bool durable = _seenPaths.Contains(previous.Path);
            long generation = ++_seenMutationGeneration;
            _pendingSeenMutations[previous.Path] = new SeenPendingMutation(durable, previous.WasUnseen, previous.ShowedUnseenDot, generation);
            _seenPaths.Add(previous.Path);
            RestoreTileSeenState(previous.Path, unseen: false, showDot: false);
            writer.Enqueue(new SeenDelta(previous.Path, durable, previous.WasUnseen, previous.ShowedUnseenDot, generation));
        }
        UpdateHeaderStats();
        ScheduleSeenWriterPump();
    }

    private void RetryFailedSharedBatches()
    {
        if (!CanStartSharedStateAction(store: null, retryAfterReload: RetryFailedSharedBatches, allowFailedRecovery: true))
            return;

        bool retryFavorite = _failedFavoriteBatch is { Count: > 0 };
        bool retrySeen = _failedSeenBatch is { Count: > 0 };
        if (!retryFavorite && !retrySeen)
        {
            SetStatusToast("Shared-state retry is no longer available.");
            return;
        }

        if (retryFavorite)
            RetryFailedFavoriteBatch();
        if (retrySeen)
            RetryFailedSeenBatch();
    }

    private void SetTileFavoriteLevel(string path, int level)
    {
        foreach (Tile tile in _allTiles)
        {
            if (string.Equals(tile.Path, path, StringComparison.OrdinalIgnoreCase))
                tile.Fav = Math.Clamp(level, 0, 5);
        }
    }

    private void RestoreTileSeenState(string path, bool unseen, bool showDot)
    {
        foreach (Tile tile in _allTiles)
        {
            if (!string.Equals(tile.Path, path, StringComparison.OrdinalIgnoreCase))
                continue;
            tile.Unseen = unseen;
            tile.ShowUnseenDot = showDot;
        }
    }

    private bool CanStartSharedStateAction(
        SharedStoreKind? store,
        Action? retryAfterReload = null,
        bool allowFailedRecovery = false)
    {
        if (_sharedReloadBarrierDepth > 0)
        {
            SetStatusToast(
                "Folder reload is synchronizing Favorite and Seen state. Try again after it finishes.",
                retryAfterReload);
            return false;
        }
        if (_sharedActionsDisabled)
            return false;

        bool unresolvedFailure = store switch
        {
            SharedStoreKind.Favorite => _failedFavoriteBatch is { Count: > 0 },
            SharedStoreKind.Seen => _failedSeenBatch is { Count: > 0 },
            _ => false,
        };
        if (!allowFailedRecovery && unresolvedFailure)
        {
            SetStatusToast(
                $"A previous {store} change still needs Retry before another {store} change can start.",
                RetryFailedSharedBatches);
            return false;
        }

        return true;
    }

    private void RefreshModalFavoriteSurface()
    {
        if (Modal.Visibility != Visibility.Visible)
            return;
        if (SelectedTile() is null)
            Modal.Visibility = Visibility.Collapsed;
        else
            OpenModal();
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
        return Path.Combine(ResolveSharedProjectRoot(), ".cache", fileName);
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

        if (!CanStartSharedStateAction(SharedStoreKind.Seen))
            return false;

        if (ShouldUseSeenWriter())
            return QueueTileSeen(tile);

        string key = NormalizeFavoritePath(tile.Path);
        bool wasUnseen = tile.Unseen;
        bool showedUnseenDot = tile.ShowUnseenDot;
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
            tile.ShowUnseenDot = showedUnseenDot;
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

    private bool QueueTileSeen(Tile tile)
    {
        if (_seenWriteBlocked)
        {
            ReportPersistenceRefusal("Seen state", ResolvedSeenPath, protectedFile: true);
            return false;
        }

        string key = NormalizeFavoritePath(tile.Path);
        if (_pendingSeenMutations.ContainsKey(key))
            return true;

        bool durableSeen = _seenPaths.Contains(key);
        if (durableSeen && !tile.Unseen)
            return true;

        long generation = ++_seenMutationGeneration;
        var pending = new SeenPendingMutation(durableSeen, tile.Unseen, tile.ShowUnseenDot, generation);
        _pendingSeenMutations[key] = pending;
        _seenPaths.Add(key);
        tile.Unseen = false;
        tile.ShowUnseenDot = false;

        EnsureSeenWriter().Enqueue(new SeenDelta(
            key,
            durableSeen,
            pending.WasUnseen,
            pending.ShowedUnseenDot,
            generation));
        UpdateHeaderStats();
        ScheduleSeenWriterPump();
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
            BitmapDecodePlan decodePlan = BuildBitmapDecodePlan(path, decodePixelWidth);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodePlan.PixelWidth > 0)
                image.DecodePixelWidth = decodePlan.PixelWidth;
            else if (decodePlan.PixelHeight > 0)
                image.DecodePixelHeight = decodePlan.PixelHeight;
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

    private static BitmapDecodePlan BuildBitmapDecodePlan(string path, int requestedPixelWidth)
    {
        if (requestedPixelWidth <= 0 || !TryReadBitmapSize(path, out int sourceWidth, out int sourceHeight))
            return new BitmapDecodePlan(Math.Max(0, requestedPixelWidth), 0);

        // BitmapImage will upscale a narrow source to DecodePixelWidth. For a
        // very tall image that can turn a small, valid file into hundreds of
        // megabytes (for example 256 x 16,384 -> 1,400 x 89,600). Start from a
        // no-upscale fit, then bound both total pixels and the long edge. These
        // limits are still above the pixels the corresponding surface can show
        // at its existing zoom range.
        double scale = Math.Min(1d, requestedPixelWidth / (double)sourceWidth);
        double targetWidth = sourceWidth * scale;
        double targetHeight = sourceHeight * scale;
        long surfacePixelBudget = Math.Min(
            MaxDecodedPixelCount,
            Math.Max(1L, (long)requestedPixelWidth * requestedPixelWidth * DecodePixelBudgetMultiplier));
        double targetPixels = targetWidth * targetHeight;
        if (targetPixels > surfacePixelBudget)
        {
            double pixelScale = Math.Sqrt(surfacePixelBudget / targetPixels);
            targetWidth *= pixelScale;
            targetHeight *= pixelScale;
        }

        int surfaceLongEdge = (int)Math.Min(
            MaxDecodedLongEdge,
            Math.Max((long)requestedPixelWidth, (long)requestedPixelWidth * DecodeLongEdgeMultiplier));
        double targetLongEdge = Math.Max(targetWidth, targetHeight);
        if (targetLongEdge > surfaceLongEdge)
        {
            double edgeScale = surfaceLongEdge / targetLongEdge;
            targetWidth *= edgeScale;
            targetHeight *= edgeScale;
        }

        // With extreme aspect ratios the bounded width can fall below one
        // pixel. In that case DecodePixelHeight is the only way to keep WIC
        // from decoding the full unbounded long edge.
        if (targetWidth < 1d)
            return new BitmapDecodePlan(0, Math.Max(1, (int)Math.Floor(targetHeight)));

        return new BitmapDecodePlan(Math.Max(1, (int)Math.Floor(targetWidth)), 0);
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

    private async Task<ImageMetadataLoadMetrics> LoadImageMetadataProgressivelyAsync(
        IReadOnlyList<Tile> tiles,
        IReadOnlyList<string> unavailableFolderSet,
        long generation,
        CancellationTokenSource cts,
        int initialDelayMilliseconds)
    {
        if (tiles.Count == 0)
            return ImageMetadataLoadMetrics.Empty;

        long sourceRecycleGenerationAtStart = _sourceRecycleGeneration;
        string metadataIndexPath = MetadataIndexStore.ResolvePath(_currentFolderSet, ResolvedStatePath);
        _metadataIndexPath = metadataIndexPath;
        BeginMetadataIndexProgress(tiles.Count);

        // Give the first visible thumbnail batch a short uncontended head
        // start. Bulk metadata reads are lower priority than pixels currently
        // on screen.
        await Task.Delay(Math.Max(250, initialDelayMilliseconds), cts.Token);

        var watch = Stopwatch.StartNew();
        var indexReadWatch = Stopwatch.StartNew();
        MetadataIndexLoadResult index = await Task.Run(
            () => MetadataIndexStore.Load(metadataIndexPath, cts.Token),
            cts.Token);
        indexReadWatch.Stop();

        var decoded = new ConcurrentQueue<DecodedImageMetadata>();
        int metadataWorkerBudget = tiles.Count >= 10_000 ? 2 : MaxMetadataReadWorkers;
        int workers = Math.Max(1, Math.Min(Math.Min(metadataWorkerBudget, Environment.ProcessorCount), tiles.Count));
        var refreshedEntries = new ConcurrentDictionary<string, MetadataIndexEntry>(
            workers,
            tiles.Count,
            StringComparer.OrdinalIgnoreCase);
        int completed = 0;
        int decodeFailures = 0;
        int cacheHits = 0;
        int cacheMisses = 0;
        int uiBatchSize = Math.Clamp(tiles.Count / 128, 96, 512);

        await Parallel.ForEachAsync(
            tiles,
            new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = workers,
            },
            async (tile, itemToken) =>
            {
                itemToken.ThrowIfCancellationRequested();
                bool cacheHit = index.Entries.TryGetValue(tile.Path, out MetadataIndexEntry? cached)
                    && cached.Matches(tile);
                int width;
                int height;
                string prompt;
                if (cacheHit)
                {
                    width = cached!.Width;
                    height = cached.Height;
                    prompt = cached.Prompt;
                    refreshedEntries[tile.Path] = cached;
                    Interlocked.Increment(ref cacheHits);
                }
                else
                {
                    Interlocked.Increment(ref cacheMisses);
                    // Bulk prompt/dimension indexing is never allowed to race
                    // a viewport thumbnail batch for disk access. Continuous
                    // scrolling can postpone this background work; visible
                    // pixels are the interactive product priority.
                    while (!_thumbnailLoadsInFlight.IsEmpty)
                        await Task.Delay(16, itemToken);

                    bool sizeRead = TryReadCatalogImageMetadata(
                        tile.Path,
                        itemToken,
                        out width,
                        out height,
                        out PngParametersMetadata? pngMetadata);
                    if (!sizeRead)
                    {
                        Interlocked.Increment(ref decodeFailures);
                    }
                    else
                    {
                        refreshedEntries[tile.Path] = new MetadataIndexEntry(
                            tile.Path,
                            tile.SourceLength,
                            tile.SourceLastWriteUtcTicks,
                            tile.SourceCreationUtcTicks,
                            width,
                            height,
                            pngMetadata?.Prompt ?? "");
                    }
                    prompt = pngMetadata?.Prompt ?? "";
                }

                decoded.Enqueue(new DecodedImageMetadata(tile, new ImageDimensions(width, height), prompt));
                int done = Interlocked.Increment(ref completed);
                if (done % uiBatchSize == 0 || done == tiles.Count)
                {
                    await Dispatcher.InvokeAsync(
                        () =>
                        {
                            if (!IsCurrentLoad(generation, cts))
                                return;
                            DrainDecodedImageMetadata(decoded);
                            UpdateMetadataIndexProgress(
                                done,
                                tiles.Count,
                                Volatile.Read(ref cacheHits),
                                Volatile.Read(ref cacheMisses));
                        },
                        DispatcherPriority.Background,
                        itemToken);
                }
            });

        await Dispatcher.InvokeAsync(
            () =>
            {
                if (!IsCurrentLoad(generation, cts))
                    return;

                DrainDecodedImageMetadata(decoded);
                UpdateMetadataIndexProgress(completed, tiles.Count, cacheHits, cacheMisses, saving: true);
                // Prompt-only matches become available after the background
                // index is complete. Filename/date/favorite filtering was
                // already usable from the first published catalog frame.
                if (!string.IsNullOrWhiteSpace(SearchInput.Text))
                    ApplyFilters(selectFirst: false);
            },
            DispatcherPriority.Background,
            cts.Token);

        MetadataIndexSaveResult save;
        var indexWriteWatch = Stopwatch.StartNew();
        bool catalogChanged = _sourceRecycleGeneration != sourceRecycleGenerationAtStart
            || _allTiles.Count != tiles.Count;
        MetadataIndexSnapshotPlan? snapshotPlan = null;
        bool completeMetadataSet = !catalogChanged
            && decodeFailures == 0
            && refreshedEntries.Count == tiles.Count;
        bool exactLoadedWarmSnapshot = completeMetadataSet
            && index.State == MetadataIndexLoadState.Loaded
            && cacheMisses == 0
            && unavailableFolderSet.Count == 0
            && index.Entries.Count == tiles.Count;
        if (index.State != MetadataIndexLoadState.Unsupported
            && completeMetadataSet
            && !exactLoadedWarmSnapshot)
        {
            snapshotPlan = await Task.Run(
                () => BuildMetadataIndexSnapshot(
                    unavailableFolderSet,
                    index.Entries,
                    refreshedEntries,
                    cts.Token),
                cts.Token);
        }
        if (index.State == MetadataIndexLoadState.Unsupported)
        {
            save = MetadataIndexSaveResult.Preserved(
                metadataIndexPath,
                refreshedEntries.Count,
                "a newer metadata index version was preserved",
                MetadataIndexSaveDisposition.Protected);
        }
        else if (catalogChanged)
        {
            save = MetadataIndexSaveResult.Preserved(
                metadataIndexPath,
                index.Entries.Count,
                "the catalog changed during indexing; the last complete index was preserved",
                MetadataIndexSaveDisposition.CatalogChanged);
        }
        else if (decodeFailures > 0 || refreshedEntries.Count != tiles.Count)
        {
            save = MetadataIndexSaveResult.Preserved(
                metadataIndexPath,
                index.Entries.Count,
                $"{decodeFailures:N0} source metadata read(s) failed; the last complete index was preserved",
                MetadataIndexSaveDisposition.Incomplete);
        }
        else if (index.State == MetadataIndexLoadState.Loaded
            && cacheMisses == 0
            && decodeFailures == 0
            && refreshedEntries.Count == tiles.Count
            && (exactLoadedWarmSnapshot || snapshotPlan?.DurableEntrySetExact == true))
        {
            // A pure warm hit is already the exact durable snapshot. Keeping
            // its bytes and timestamp unchanged makes restart reuse observable
            // and avoids an unnecessary 100k-entry rewrite.
            save = MetadataIndexSaveResult.Preserved(
                metadataIndexPath,
                refreshedEntries.Count,
                "the complete index was reused byte-for-byte");
        }
        else
        {
            MetadataIndexEntry[] snapshot = snapshotPlan?.Entries
                ?? throw new InvalidOperationException("metadata index snapshot was unavailable");
            save = await Task.Run(
                () => MetadataIndexStore.Save(metadataIndexPath, snapshot, cts.Token),
                cts.Token);
        }
        indexWriteWatch.Stop();
        watch.Stop();

        await Dispatcher.InvokeAsync(
            () =>
            {
                if (IsCurrentLoad(generation, cts))
                    CompleteMetadataIndexProgress(tiles.Count, cacheHits, cacheMisses, index.State, save);
            },
            DispatcherPriority.Background,
            cts.Token);

        return new ImageMetadataLoadMetrics(
            new Dictionary<string, ImageDimensions>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            workers,
            completed,
            watch.ElapsedMilliseconds,
            decodeFailures,
            cacheHits,
            cacheMisses,
            indexReadWatch.ElapsedMilliseconds,
            indexWriteWatch.ElapsedMilliseconds,
            index.State.ToString(),
            save.Ok,
            save.Written,
            save.Error);
    }

    private static MetadataIndexSnapshotPlan BuildMetadataIndexSnapshot(
        IReadOnlyList<string> unavailableFolderSet,
        IReadOnlyDictionary<string, MetadataIndexEntry> priorEntries,
        ConcurrentDictionary<string, MetadataIndexEntry> refreshedEntries,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string[] normalizedUnavailableRoots = unavailableFolderSet
            .Select(static root => Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToArray();
        var unavailablePriorEntries = new List<MetadataIndexEntry>();
        int checkedEntries = 0;
        foreach (MetadataIndexEntry entry in priorEntries.Values)
        {
            if ((checkedEntries++ & 255) == 0)
                token.ThrowIfCancellationRequested();
            if (refreshedEntries.ContainsKey(entry.Path))
                continue;

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(entry.Path);
            }
            catch
            {
                continue;
            }

            foreach (string root in normalizedUnavailableRoots)
            {
                if (string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase)
                    || normalizedPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || normalizedPath.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    unavailablePriorEntries.Add(entry);
                    break;
                }
            }
        }
        token.ThrowIfCancellationRequested();
        bool durableEntrySetExact = priorEntries.Count == refreshedEntries.Count + unavailablePriorEntries.Count;
        var snapshotByPath = new Dictionary<string, MetadataIndexEntry>(
            refreshedEntries,
            StringComparer.OrdinalIgnoreCase);
        // A selected drive/root can be temporarily unavailable. Keep its
        // still-derived entries so reconnecting the same explicit folder set
        // remains warm, while available-root removals are pruned by the exact
        // current catalog snapshot.
        foreach (MetadataIndexEntry prior in unavailablePriorEntries)
            snapshotByPath.TryAdd(prior.Path, prior);
        token.ThrowIfCancellationRequested();
        return new MetadataIndexSnapshotPlan(snapshotByPath.Values.ToArray(), durableEntrySetExact);
    }

    private void BeginMetadataIndexProgress(int total)
    {
        _metadataIndexStatus = "loading";
        _metadataIndexProgress = 0;
        _metadataIndexCompleted = 0;
        _metadataIndexTotal = total;
        _metadataIndexCacheHits = 0;
        _metadataIndexCacheMisses = 0;
        RenderMetadataIndexProgress(
            $"Prompt metadata: checking saved index for {total:N0} images...",
            0,
            showProgress: true);
    }

    private void UpdateMetadataIndexProgress(int completed, int total, int cacheHits, int cacheMisses, bool saving = false)
    {
        int percent = total <= 0 ? 100 : (int)Math.Clamp(completed * 100L / total, 0, 100);
        _metadataIndexStatus = saving ? "saving" : "loading";
        _metadataIndexProgress = percent;
        _metadataIndexCompleted = completed;
        _metadataIndexTotal = total;
        _metadataIndexCacheHits = cacheHits;
        _metadataIndexCacheMisses = cacheMisses;
        string text = saving
            ? $"Prompt metadata: saving safe index ({completed:N0} / {total:N0})..."
            : $"Prompt metadata: {completed:N0} / {total:N0} ({percent}%) - {cacheHits:N0} reused";
        RenderMetadataIndexProgress(text, percent, showProgress: true);
    }

    private void CompleteMetadataIndexProgress(
        int total,
        int cacheHits,
        int cacheMisses,
        MetadataIndexLoadState loadState,
        MetadataIndexSaveResult save)
    {
        _metadataIndexStatus = save.Disposition switch
        {
            MetadataIndexSaveDisposition.Protected => "protected",
            MetadataIndexSaveDisposition.Incomplete => "incomplete",
            MetadataIndexSaveDisposition.CatalogChanged => "superseded",
            MetadataIndexSaveDisposition.Failed => "save-failed",
            _ => "ready",
        };
        _metadataIndexProgress = 100;
        _metadataIndexCompleted = total;
        _metadataIndexTotal = total;
        _metadataIndexCacheHits = cacheHits;
        _metadataIndexCacheMisses = cacheMisses;
        string rebuilt = loadState == MetadataIndexLoadState.Invalid ? " - damaged index rebuilt safely" : "";
        bool hadCompleteIndex = loadState == MetadataIndexLoadState.Loaded;
        string text = save.Disposition == MetadataIndexSaveDisposition.Protected
            ? $"Prompt metadata ready - newer saved index preserved - {cacheMisses:N0} read from source"
            : save.Disposition == MetadataIndexSaveDisposition.Incomplete
                ? hadCompleteIndex
                    ? $"Prompt metadata incomplete - {cacheMisses:N0} refreshed, unread files will retry - last complete index kept"
                    : $"Prompt metadata incomplete - {cacheMisses:N0} refreshed, index not written - unread files will retry"
            : save.Disposition == MetadataIndexSaveDisposition.CatalogChanged
                ? "Prompt metadata changed during indexing - saved index update skipped; refreshing current catalog"
            : save.Ok
                ? $"Prompt metadata ready - {total:N0} indexed - {cacheHits:N0} reused, {cacheMisses:N0} refreshed{rebuilt}"
                : $"Prompt metadata ready - cache save failed - {cacheMisses:N0} read from source";
        RenderMetadataIndexProgress(text, 100, showProgress: false);
    }

    private void RenderMetadataIndexProgress(string text, int percent, bool showProgress)
    {
        MetadataIndexStatusText.Text = text;
        MetadataIndexStatusText.Visibility = Visibility.Visible;
        MetadataIndexProgressBar.Value = percent;
        MetadataIndexProgressBar.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        MetadataIndexFooterStatusText.Text = text;
        MetadataIndexFooterProgressBar.Value = percent;
        MetadataIndexFooterProgressBar.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
        MetadataIndexFooterPanel.Visibility = Visibility.Visible;
    }

    private static bool TryReadCatalogImageMetadata(
        string path,
        CancellationToken token,
        out int width,
        out int height,
        out PngParametersMetadata? pngMetadata)
    {
        pngMetadata = null;
        if (string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)
            && TryReadPngCatalogHeader(path, token, out width, out height, out pngMetadata))
        {
            return width > 0 && height > 0;
        }

        // Non-PNG files still use WIC for dimensions. A malformed PNG also
        // gets the old fallback so its recoverable decode-warning semantics do
        // not change.
        bool sizeRead = TryReadBitmapSize(path, out width, out height);
        if (string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            pngMetadata = ReadPngParametersMetadata(path, token);
        return sizeRead;
    }

    private static bool TryReadPngCatalogHeader(
        string path,
        CancellationToken token,
        out int width,
        out int height,
        out PngParametersMetadata? metadata)
    {
        width = 0;
        height = 0;
        metadata = null;
        bool parametersChunkSeen = false;

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                4096,
                FileOptions.SequentialScan);
            Span<byte> signature = stackalloc byte[8];
            stream.ReadExactly(signature);
            if (!signature.SequenceEqual(PngSignature))
                return false;

            Span<byte> chunkHeader = stackalloc byte[8];
            Span<byte> imageHeader = stackalloc byte[13];
            while (stream.Position + 12 <= stream.Length)
            {
                token.ThrowIfCancellationRequested();
                stream.ReadExactly(chunkHeader);

                int length = (chunkHeader[0] << 24) | (chunkHeader[1] << 16) | (chunkHeader[2] << 8) | chunkHeader[3];
                if (length < 0 || length > MaxPngMetadataChunkBytes || stream.Position + length + 4 > stream.Length)
                    return false;

                bool isHeader = chunkHeader[4] == (byte)'I'
                    && chunkHeader[5] == (byte)'H'
                    && chunkHeader[6] == (byte)'D'
                    && chunkHeader[7] == (byte)'R';
                if (isHeader)
                {
                    if (length != 13)
                        return false;
                    stream.ReadExactly(imageHeader);
                    if (!TrySkip(stream, 4))
                        return false;
                    width = (imageHeader[0] << 24) | (imageHeader[1] << 16) | (imageHeader[2] << 8) | imageHeader[3];
                    height = (imageHeader[4] << 24) | (imageHeader[5] << 16) | (imageHeader[6] << 8) | imageHeader[7];
                    continue;
                }

                // The existing metadata reader intentionally stops before
                // image payload. Preserve that bound while reusing the same
                // stream that supplied PNG dimensions.
                bool isImageData = chunkHeader[4] == (byte)'I'
                    && chunkHeader[5] == (byte)'D'
                    && chunkHeader[6] == (byte)'A'
                    && chunkHeader[7] == (byte)'T';
                if (isImageData)
                    return width > 0 && height > 0;

                bool isText = chunkHeader[4] == (byte)'t'
                    && chunkHeader[5] == (byte)'E'
                    && chunkHeader[6] == (byte)'X'
                    && chunkHeader[7] == (byte)'t';
                if (isText)
                {
                    var data = new byte[length];
                    if (!TryReadExactly(stream, data) || !TrySkip(stream, 4))
                        return false;

                    int separator = Array.IndexOf(data, (byte)0);
                    if (separator > 0
                        && !parametersChunkSeen
                        && string.Equals(Encoding.Latin1.GetString(data, 0, separator), "parameters", StringComparison.Ordinal))
                    {
                        parametersChunkSeen = true;
                        string raw = Encoding.UTF8.GetString(data, separator + 1, data.Length - separator - 1);
                        // Match the established metadata reader and product
                        // contract: the first parameters chunk owns the image.
                        // Later duplicate chunks must not make catalog search
                        // disagree with Preview/Modal metadata.
                        metadata = ParsePngParameters(raw);
                    }
                    continue;
                }

                if (!TrySkip(stream, length + 4))
                    return false;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return width > 0 && height > 0;
    }

    private void DrainDecodedImageMetadata(ConcurrentQueue<DecodedImageMetadata> decoded)
    {
        string? selectedPath = SelectedTile()?.Path;
        bool selectedMetadataArrived = false;
        while (decoded.TryDequeue(out DecodedImageMetadata item))
        {
            item.Tile.ImagePixelWidth = item.Dimensions.Width;
            item.Tile.ImagePixelHeight = item.Dimensions.Height;
            item.Tile.Prompt = item.Prompt;
            selectedMetadataArrived |= !string.IsNullOrWhiteSpace(selectedPath)
                && string.Equals(item.Tile.Path, selectedPath, StringComparison.OrdinalIgnoreCase);
        }

        if (!selectedMetadataArrived
            || SelectedTile() is not Tile selected
            || !string.Equals(selected.Path, selectedPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // The catalog is intentionally published before full metadata. A file
        // may be replaced after that first frame but before its metadata probe.
        // Re-decode only the selected surfaces when that probe arrives so an
        // early preview/modal cannot keep presenting the pre-Refresh bytes.
        UpdatePreview(selected);
        if (Modal.Visibility == Visibility.Visible
            && string.Equals(_modalSourceTilePath, selected.Path, StringComparison.OrdinalIgnoreCase))
        {
            OpenModal();
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

    // Placeholder art is visible only until a real thumbnail arrives. Its
    // palette repeats every 10 items and its two glow coordinates repeat every
    // 70 / 60 items, so 420 variants preserve the existing visual cycle while
    // avoiding two new frozen gradient objects for every catalog entry.
    private const int ArtGlowVariantCount = 420;
    private static readonly Brush[] BaseBrushCache = Enumerable.Range(0, Palettes.Length)
        .Select(CreateBaseBrush)
        .ToArray();
    private static readonly Brush[] GlowBrushCache = Enumerable.Range(0, ArtGlowVariantCount)
        .Select(CreateGlowBrush)
        .ToArray();

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

    private static Brush MakeBaseBrush(int i) => BaseBrushCache[i % BaseBrushCache.Length];

    private static Brush CreateBaseBrush(int i)
    {
        var p = Palettes[i % Palettes.Length];
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        b.GradientStops.Add(new GradientStop(Hex(p[2]), 0));
        b.GradientStops.Add(new GradientStop(Hex(p[1]), 0.55));
        b.GradientStops.Add(new GradientStop(Hex(p[2]), 1));
        b.Freeze();
        return b;
    }

    private static Brush MakeGlowBrush(int i) => GlowBrushCache[i % GlowBrushCache.Length];

    private static Brush CreateGlowBrush(int i)
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

        bool collapseSparseSelection = _selectedPaths.Count > MaxMaterializedSelectionVisualItems
            && (_shortcutModifierProvider() & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None;
        if (collapseSparseSelection)
            _selectedPaths.Clear();

        foreach (Tile tile in e.RemovedItems.OfType<Tile>())
            _selectedPaths.Remove(tile.Path);
        foreach (Tile tile in e.AddedItems.OfType<Tile>())
            _selectedPaths.Add(tile.Path);

        Tile? primary = lb.SelectedItem as Tile;
        if (collapseSparseSelection && primary is not null)
            _selectedPaths.Add(primary.Path);
        if (primary is null || !_selectedPaths.Contains(primary.Path))
            primary = e.AddedItems.OfType<Tile>().LastOrDefault()
                ?? SelectedTiles().LastOrDefault();
        _primarySelectedPath = primary?.Path;
        _selectionVisualSyncGeneration++;

        SynchronizeSelectionControls();
        ApplyPrimarySelection(primary);
    }

    private IReadOnlyList<Tile> SelectedTiles()
        => _tiles.Where(tile => _selectedPaths.Contains(tile.Path)).ToList();

    private void SetSelection(IEnumerable<Tile> selectedTiles, Tile? primary)
    {
        _selectedPaths.Clear();
        if (selectedTiles is IReadOnlyCollection<Tile> collection
            && collection.Count > MaxMaterializedSelectionVisualItems)
        {
            var availablePaths = new HashSet<string>(
                _tiles.Select(static tile => tile.Path),
                StringComparer.OrdinalIgnoreCase);
            foreach (Tile tile in selectedTiles)
            {
                if (availablePaths.Contains(tile.Path))
                    _selectedPaths.Add(tile.Path);
            }
        }
        else
        {
            foreach (Tile tile in selectedTiles.Where(tile => _tiles.Contains(tile)))
                _selectedPaths.Add(tile.Path);
        }

        Tile? effectivePrimary = primary is not null && _selectedPaths.Contains(primary.Path)
            ? primary
            : SelectedTiles().LastOrDefault();
        _primarySelectedPath = effectivePrimary?.Path;
        _selectionVisualSyncGeneration++;

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
            bool sparseVisuals = _selectedPaths.Count > MaxMaterializedSelectionVisualItems;
            List<Tile>? materializedSelections = sparseVisuals
                ? null
                : _tiles.Where(tile => _selectedPaths.Contains(tile.Path)).ToList();
            var cardsWatch = Stopwatch.StartNew();
            if (sparseVisuals)
                SynchronizeRealizedSelectionControl(CardsList);
            else
                SynchronizeSelectionControl(CardsList, materializedSelections!);
            cardsWatch.Stop();
            _lastCardsSelectionSyncMs = cardsWatch.ElapsedMilliseconds;
            var rowsWatch = Stopwatch.StartNew();
            if (sparseVisuals)
                SynchronizeRealizedSelectionControl(RowsList);
            else
                SynchronizeSelectionControl(RowsList, materializedSelections!);
            rowsWatch.Stop();
            _lastRowsSelectionSyncMs = rowsWatch.ElapsedMilliseconds;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private static void SynchronizeSelectionControl(ListBox listBox, IReadOnlyList<Tile> selectedTiles)
    {
        // The hidden surface is reconciled when it becomes visible. Touching a
        // grouped 20k-item ListBox here can force full index resolution even
        // though no visual state is observable.
        if (listBox.Visibility != Visibility.Visible)
            return;

        listBox.SelectedItems.Clear();
        if (selectedTiles.Count == 1)
        {
            // Never ask grouped ListBox.SelectedItems to resolve a distant
            // single item. Select an existing visible container directly, or
            // let the Render-priority resync apply it after ScrollIntoView.
            if (listBox.ItemContainerGenerator.ContainerFromItem(selectedTiles[0]) is ListBoxItem container)
                container.IsSelected = true;
            return;
        }
        foreach (Tile tile in selectedTiles)
            listBox.SelectedItems.Add(tile);
    }

    private void SynchronizeRealizedSelectionControl(ListBox listBox)
    {
        if (listBox.Visibility != Visibility.Visible)
            return;

        // Canonical selection lives in _selectedPaths. WPF SelectedItems is
        // deliberately only a bounded visual projection for very large sets;
        // asking it to own 100k entries defeats virtualization and can hang UI.
        listBox.SelectedItems.Clear();
        foreach (ListBoxItem container in FindVisualDescendants<ListBoxItem>(listBox))
        {
            bool selected = container.DataContext is Tile tile && _selectedPaths.Contains(tile.Path);
            if (container.IsSelected != selected)
                container.IsSelected = selected;
        }
    }

    private void QueueSparseSelectionVisualSync(ListBox listBox, bool grid)
    {
        if (_selectedPaths.Count <= MaxMaterializedSelectionVisualItems
            || listBox.Visibility != Visibility.Visible)
        {
            return;
        }

        long generation = _selectionVisualSyncGeneration;
        if (grid)
        {
            if (_queuedSparseGridSelectionVisualSyncGeneration == generation)
                return;
            _queuedSparseGridSelectionVisualSyncGeneration = generation;
        }
        else
        {
            if (_queuedSparseListSelectionVisualSyncGeneration == generation)
                return;
            _queuedSparseListSelectionVisualSyncGeneration = generation;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (grid && _queuedSparseGridSelectionVisualSyncGeneration == generation)
                _queuedSparseGridSelectionVisualSyncGeneration = -1;
            if (!grid && _queuedSparseListSelectionVisualSyncGeneration == generation)
                _queuedSparseListSelectionVisualSyncGeneration = -1;
            if (generation != _selectionVisualSyncGeneration
                || _selectedPaths.Count <= MaxMaterializedSelectionVisualItems
                || listBox.Visibility != Visibility.Visible)
            {
                return;
            }

            bool wasSyncingSelection = _syncingSelection;
            _syncingSelection = true;
            try
            {
                SynchronizeRealizedSelectionControl(listBox);
            }
            finally
            {
                _syncingSelection = wasSyncingSelection;
            }
        }, DispatcherPriority.Render);
    }

    private void ApplyPrimarySelection(Tile? primary)
    {
        if (primary is null)
        {
            ClearPreview();
            return;
        }

        _lastEnsureGridSelectionMs = 0;
        var cardsScrollWatch = Stopwatch.StartNew();
        if (CardsList.Visibility == Visibility.Visible)
        {
            CardsList.ScrollIntoView(primary);
            QueueGridSelectionVisualSync(primary);
        }
        cardsScrollWatch.Stop();
        _lastCardsScrollSelectionMs = cardsScrollWatch.ElapsedMilliseconds;
        var rowsScrollWatch = Stopwatch.StartNew();
        if (RowsList.Visibility == Visibility.Visible)
        {
            RowsList.ScrollIntoView(primary);
            SelectRealizedContainer(RowsList, primary);
            QueueListSelectionVisualSync(primary);
        }
        rowsScrollWatch.Stop();
        _lastRowsScrollSelectionMs = rowsScrollWatch.ElapsedMilliseconds;
        var previewWatch = Stopwatch.StartNew();
        UpdatePreview(primary);
        previewWatch.Stop();
        _lastPreviewSelectionMs = previewWatch.ElapsedMilliseconds;
        if (primary.IsRealFile)
        {
            var seenWatch = Stopwatch.StartNew();
            MarkTileSeen(primary);
            seenWatch.Stop();
            _lastSeenSelectionMs = seenWatch.ElapsedMilliseconds;
            SaveState();
        }
    }

    private void SelectRealizedContainer(ListBox listBox, Tile tile)
    {
        if (listBox.ItemContainerGenerator.ContainerFromItem(tile) is not ListBoxItem container || container.IsSelected)
            return;

        bool wasSyncingSelection = _syncingSelection;
        _syncingSelection = true;
        try
        {
            container.IsSelected = true;
        }
        finally
        {
            _syncingSelection = wasSyncingSelection;
        }
    }

    private void QueueGridSelectionVisualSync(Tile tile)
    {
        long generation = _selectionVisualSyncGeneration;
        if (_queuedGridSelectionVisualSyncGeneration == generation)
            return;

        _queuedGridSelectionVisualSyncGeneration = generation;
        string path = tile.Path;
        Dispatcher.BeginInvoke(() =>
        {
            if (_queuedGridSelectionVisualSyncGeneration == generation)
                _queuedGridSelectionVisualSyncGeneration = -1;

            if (generation != _selectionVisualSyncGeneration
                || CardsList.Visibility != Visibility.Visible
                || !_selectedPaths.Contains(path)
                || !string.Equals(_primarySelectedPath, path, StringComparison.OrdinalIgnoreCase)
                || !_tiles.Contains(tile))
            {
                return;
            }

            // ScrollIntoView queues container generation. Reapply only the
            // canonical primary container after render; never enumerate the
            // catalog, move focus, or trigger another scroll from this callback.
            SelectRealizedContainer(CardsList, tile);
        }, DispatcherPriority.Render);
    }

    private void QueueListSelectionVisualSync(Tile tile)
    {
        long generation = _selectionVisualSyncGeneration;
        if (_queuedListSelectionVisualSyncGeneration == generation)
            return;

        _queuedListSelectionVisualSyncGeneration = generation;
        string path = tile.Path;
        Dispatcher.BeginInvoke(() =>
        {
            if (_queuedListSelectionVisualSyncGeneration == generation)
                _queuedListSelectionVisualSyncGeneration = -1;

            if (generation != _selectionVisualSyncGeneration
                || RowsList.Visibility != Visibility.Visible
                || !_selectedPaths.Contains(path)
                || !string.Equals(_primarySelectedPath, path, StringComparison.OrdinalIgnoreCase)
                || !_tiles.Contains(tile))
            {
                return;
            }

            SelectRealizedContainer(RowsList, tile);
        }, DispatcherPriority.Render);
    }

    private void UpdatePreview(Tile t)
    {
        RightPreviewContent.Visibility = Visibility.Visible;
        RightPreviewEmptyState.Visibility = Visibility.Collapsed;
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
                    if (_previewDecodeDelayForSmokeMs > 0)
                        Thread.Sleep(_previewDecodeDelayForSmokeMs);
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
                    else
                    {
                        // The immediate source may be a thumbnail decoded before
                        // an external truncate, lock, or replacement. A failed
                        // full decode must not leave that stale bitmap presented
                        // as the current file.
                        PreviewBitmap.Source = null;
                        PreviewBitmap.Visibility = Visibility.Collapsed;
                        PreviewArtBase.Visibility = Visibility.Visible;
                        PreviewArtGlow.Visibility = Visibility.Visible;
                        PreviewSizeText.Text = "Could not decode";
                        ReportCurrentImageDecodeFailure();
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
        if (_initializing || _syncingUnseenDotsControls) return;
        bool enabled = sender is CheckBox checkBox && checkBox.IsChecked == true;
        SetShowUnseenDots(enabled, persist: true);
    }

    private void SetShowUnseenDots(bool enabled, bool persist)
    {
        _showUnseenDots = enabled;
        _syncingUnseenDotsControls = true;
        try
        {
            if (ShowUnseenDots is not null)
                ShowUnseenDots.IsChecked = enabled;
            if (AppSettingsUnseenDotsCheckBox is not null)
                AppSettingsUnseenDotsCheckBox.IsChecked = enabled;
        }
        finally
        {
            _syncingUnseenDotsControls = false;
        }
        RefreshUnseenDots();
        if (persist && !_initializing)
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
        FoldersSectionToggle.IsExpanded = _foldersSectionExpanded;
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

        if (!CanStartSharedStateAction(SharedStoreKind.Favorite))
            return false;
        if (ShouldUseFavoriteWriter())
        {
            return QueueFavoriteLevels(
                selected.Select(tile => (Tile: tile, Level: Math.Clamp(levelSelector(tile), 0, 5))).ToList(),
                string.Format(CultureInfo.InvariantCulture, successMessageFormat, selected.Count));
        }

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

        int selectedCount = 0;
        int firstLevel = 0;
        bool mixedLevels = false;
        foreach (Tile tile in _tiles)
        {
            if (!tile.IsRealFile || !_selectedPaths.Contains(tile.Path))
                continue;
            if (selectedCount == 0)
                firstLevel = tile.Fav;
            else if (tile.Fav != firstLevel)
                mixedLevels = true;
            selectedCount++;
        }

        bool bulk = selectedCount > 1;
        BulkFavoritePanel.Visibility = bulk ? Visibility.Visible : Visibility.Collapsed;
        SingleSelectionActions.Visibility = bulk ? Visibility.Collapsed : Visibility.Visible;
        if (!bulk)
            return;

        string levelSummary = mixedLevels ? "mixed levels" : $"Lv {firstLevel}";
        BulkSelectionText.Text = $"{selectedCount:N0} images selected · {levelSummary}";
    }

    private bool SetFavoriteLevel(Tile tile, int level)
    {
        if (!tile.IsRealFile)
            return false;

        if (!CanStartSharedStateAction(SharedStoreKind.Favorite))
            return false;

        int clamped = Math.Clamp(level, 0, 5);
        if (ShouldUseFavoriteWriter())
            return QueueFavoriteLevels([(tile, clamped)], $"Set favorite level {clamped}.");

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

    private bool QueueFavoriteLevels(IReadOnlyList<(Tile Tile, int Level)> changes, string successMessage)
    {
        if (changes.Count == 0)
            return false;
        if (_favoritesWriteBlocked)
        {
            ReportPersistenceRefusal("Favorites", ResolvedFavoritesPath, protectedFile: true);
            return false;
        }

        _favoriteSaveAttemptCount++;
        SharedStoreWriter<FavoriteDelta> writer = EnsureFavoriteWriter();
        foreach ((Tile tile, int requestedLevel) in changes)
        {
            int desired = Math.Clamp(requestedLevel, 0, 5);
            string key = NormalizeFavoritePath(tile.Path);
            int displayedBefore = FavoriteLevelForPath(key);
            int durable = _pendingFavoriteMutations.TryGetValue(key, out FavoritePendingMutation? existing)
                ? existing.DurableLevel
                : displayedBefore;
            long generation = ++_favoriteMutationGeneration;
            _pendingFavoriteMutations[key] = new FavoritePendingMutation(durable, desired, generation);

            if (desired > 0)
                _favorites[key] = desired;
            else
                _favorites.Remove(key);
            tile.Fav = desired;
            writer.Enqueue(new FavoriteDelta(key, durable, desired, generation));
        }

        ApplyFilters();
        SyncSelectionActionSurface();
        RefreshModalFavoriteSurface();
        SaveState();
        SetStatusToast($"{successMessage} Saving...");
        ScheduleFavoriteWriterPump();
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

        bool hasPinsInCurrentRoots = _pinnedPreviewPaths.Any(path =>
            _currentFolderSet.Any(root => IsPathWithinRoot(path, root)));
        if (requestedPaths.Count == 0 && _closedPreviewTabs.Count == 0 && !hasPinsInCurrentRoots)
        {
            // The common first-open path has no tab identity to reconcile.
            // Avoid building a normalized 100k path dictionary just to prove
            // that three already-empty collections are empty.
            _previewTabs.Clear();
            _activePreviewTabPath = null;
            _restoredPreviewTabPaths.Clear();
            _restoredActivePreviewTabPath = null;
            _previewTabsPersistenceReady = true;
            RefreshPreviewTabs();
            return null;
        }

        // Preview tabs are a catalog session, not a projection of the current
        // search/filter result. A tab hidden by an exact Favorite or search
        // filter must survive reload and become visible again when filters clear.
        var currentByPath = _allTiles
            .Where(static tile => tile.IsRealFile)
            .ToDictionary(tile => NormalizeFavoritePath(tile.Path), StringComparer.OrdinalIgnoreCase);

        // Pin identity belongs to a source path. Keep pins from other folder
        // sets, but remove paths that disappeared from the roots just scanned.
        _pinnedPreviewPaths.RemoveWhere(path =>
            _currentFolderSet.Any(root => IsPathWithinRoot(path, root))
            && !currentByPath.ContainsKey(path));

        // Closed tabs hold Tile instances from the previous catalog. Rebind
        // surviving paths to the freshly materialized Tile objects so Refresh
        // does not erase valid close history merely because object identity
        // changed. Missing/renamed sources are intentionally discarded.
        var reboundClosedTabs = new List<Tile>();
        var reboundClosedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Tile closed in _closedPreviewTabs)
        {
            string path = NormalizeFavoritePath(closed.Path);
            if (currentByPath.TryGetValue(path, out Tile? current)
                && reboundClosedPaths.Add(path))
            {
                reboundClosedTabs.Add(current);
            }
        }
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
        _closedPreviewTabs.Clear();
        _closedPreviewTabs.AddRange(reboundClosedTabs.Take(30));
        RefreshPreviewTabs();
        return active;
    }

    private void ReconcileOpenSurfacesAfterCatalogReload(
        bool modalWasVisible,
        bool modalHadFocus,
        string? focusedPreviewTabPath)
    {
        if (modalWasVisible)
        {
            if (SelectedTile() is not null)
            {
                OpenModal();
                if (modalHadFocus)
                    Dispatcher.BeginInvoke(ModalCloseBtn.Focus, DispatcherPriority.Input);
            }
            else
            {
                CloseModal();
                if (modalHadFocus)
                    Dispatcher.BeginInvoke(OpenFolderSetButton.Focus, DispatcherPriority.Input);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(focusedPreviewTabPath))
            return;

        string? nextFocusPath = _previewTabs
            .FirstOrDefault(tab => string.Equals(tab.Path, focusedPreviewTabPath, StringComparison.OrdinalIgnoreCase))?.Path
            ?? _activePreviewTabPath;
        if (!string.IsNullOrWhiteSpace(nextFocusPath))
        {
            FocusPreviewTab(nextFocusPath);
        }
        else if (_closedPreviewTabs.Count > 0)
        {
            FocusRestorePreviewTabButton();
        }
        else
        {
            Dispatcher.BeginInvoke(CardsList.Focus, DispatcherPriority.Input);
        }
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

        if (_selectedPaths.Count > 0)
        {
            var availablePaths = new HashSet<string>(filtered.Select(static tile => tile.Path), StringComparer.OrdinalIgnoreCase);
            _selectedPaths.IntersectWith(availablePaths);
        }

        bool wasSyncingSelection = _syncingSelection;
        _syncingSelection = true;
        try
        {
            _tiles.ReplaceAll(filtered);
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
        _galleryVirtualizingPanel?.InvalidateItemLayout();
        UpdateFolderStats();

        if (preferred is not null)
        {
            if (_selectedPaths.Count == 0)
                _selectedPaths.Add(preferred.Path);
            SetSelection(SelectedTiles(), preferred);
        }
        else if (filtered.Count == 0)
            SelectTile(null);

        ReconcileOpenSurfacesAfterFilterChange();
    }

    private void ReconcileOpenSurfacesAfterFilterChange()
    {
        Tile? selected = SelectedTile();
        if (!string.IsNullOrWhiteSpace(_activePreviewTabPath)
            && !_tiles.Any(tile => string.Equals(tile.Path, _activePreviewTabPath, StringComparison.OrdinalIgnoreCase)))
        {
            // Open tabs belong to the full catalog and must survive filters, but
            // an active marker may not claim that its filtered-out image is the
            // current right-preview selection.  The tab can be activated again
            // as soon as the filter admits its path.
            _activePreviewTabPath = null;
            RefreshPreviewTabs();
        }

        if (Modal.Visibility != Visibility.Visible)
            return;

        if (selected is null)
        {
            bool modalHadFocus = Modal.IsKeyboardFocusWithin;
            CloseModal();
            if (modalHadFocus)
                Dispatcher.BeginInvoke(SearchInput.Focus, DispatcherPriority.Input);
            return;
        }

        if (!string.Equals(_modalSourceTilePath, selected.Path, StringComparison.OrdinalIgnoreCase))
            OpenModal();
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
        RightPreviewContent.Visibility = Visibility.Collapsed;
        RightPreviewEmptyState.Visibility = Visibility.Visible;
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

        int selected = _selectedPaths.Count;
        int visible = _tiles.Count;
        int total = _allTiles.Count;
        string imageText = visible == total ? $"{total:N0} images" : $"{visible:N0} / {total:N0} images";
        string folderText = _currentFolderSet.Count == 0 ? "sample" : $"{_currentFolderSet.Count:N0} folder(s)";
        HeaderStats.Text = $"{selected:N0} selected - {imageText} - {folderText}";
    }

    private void UpdateGridMetrics(LoadMetrics metrics)
    {
        VirtualizingWrapPanel? panel = FindVisualDescendant<VirtualizingWrapPanel>(CardsList);
        int realizedCount = panel?.RealizedItemCount ?? 0;
        int windowStart = Math.Max(0, panel?.FirstRealizedIndex ?? 0);
        int windowEnd = Math.Max(windowStart, (panel?.LastRealizedIndex ?? -1) + 1);
        metrics.GridTotalItems = _tiles.Count;
        metrics.GridRealizedItems = realizedCount;
        metrics.GridDeferredItems = Math.Max(0, _tiles.Count - realizedCount);
        metrics.GridInitialRealizationLimit = MaxVirtualizedContainerSmokeCount;
        metrics.GridRealizationBatchSize = 0;
        metrics.GridMaxRealizationCount = MaxVirtualizedContainerSmokeCount;
        metrics.GridWindowStartIndex = windowStart;
        metrics.GridWindowEndIndex = windowEnd;
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
        foreach (ListBoxItem item in FindVisualDescendants<ListBoxItem>(CardsList))
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
            VirtualizingWrapPanel? panel = FindVisualDescendant<VirtualizingWrapPanel>(CardsList);
            Tile? anchorTile = _tiles.FirstOrDefault(tile => string.Equals(tile.Path, anchor.Value.Path, StringComparison.OrdinalIgnoreCase));
            int anchorIndex = anchorTile is null ? -1 : _tiles.IndexOf(anchorTile);
            if (panel is not null && anchorIndex >= 0 && panel.RestoreItemViewportTop(anchorIndex, anchor.Value.ViewportY))
            {
                Dispatcher.BeginInvoke(() =>
                {
                    double actual = panel.GetItemViewportTop(anchorIndex);
                    if (double.IsFinite(actual))
                        _lastGridZoomAnchorDrift = Math.Abs(actual - anchor.Value.ViewportY);
                }, DispatcherPriority.Render);
                return;
            }

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
        _galleryVirtualizingPanel?.InvalidateItemLayout();
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
        tile.CardThumbnailStretch = _aspectMode == AspectOriginalValue ? Stretch.Uniform : Stretch.UniformToFill;
        tile.ListThumbnailWidth = listThumbnailBase;
        tile.ListThumbnailHeight = Math.Clamp(listThumbnailBase * aspectHeightFactor, 32, 120);
        tile.ListThumbnailSize = Math.Max(tile.ListThumbnailWidth, tile.ListThumbnailHeight);
    }

    private double AspectHeightFactor(Tile tile)
    {
        return _aspectMode switch
        {
            AspectSquareValue => 1.0,
            _ => 1.5,
        };
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
            ConfigureGalleryItemsSources();
            // A sort change cannot alter filter membership. Reorder the already
            // filtered Tile references directly instead of rebuilding 100k
            // filter snapshots and evaluating every predicate again.
            ApplyFilterResult(new FilterResult(SortTiles(_tiles, _sortBy, _randomSortSeed)), selectFirst: true);
            SaveState();
        }

        return changed;
    }

    private static List<Tile> SortTiles(IEnumerable<Tile> tiles, string sortBy, string randomSortSeed)
    {
        IEnumerable<Tile> ordered = sortBy switch
        {
            SortModifiedOldestValue => tiles
                .OrderBy(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortCreatedNewestValue => tiles
                .OrderByDescending(static tile => tile.CreatedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortCreatedOldestValue => tiles
                .OrderBy(static tile => tile.CreatedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortRandomValue => tiles
                .OrderBy(tile => StableRandomSortKey(randomSortSeed, tile.Path))
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            SortNameValue => tiles
                .OrderBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
            _ => tiles
                .OrderByDescending(static tile => tile.ModifiedUtc)
                .ThenBy(static tile => tile.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tile => tile.Path, StringComparer.OrdinalIgnoreCase),
        };
        return ordered.ToList();
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

    private bool IsZoomModifierActive()
        => (_shortcutModifierProvider() & (ModifierKeys.Control | ModifierKeys.Windows)) != 0;

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
        Tile? selected = SelectedTile();
        CardsList.Visibility = Visibility.Visible;
        RowsList.Visibility = Visibility.Collapsed;
        ConfigureRowsItemsSource(forceGroupedView: false);
        if (selected is not null)
        {
            SynchronizeSelectionControls();
            CardsList.ScrollIntoView(selected);
            QueueGridSelectionVisualSync(selected);
        }
        else
            SynchronizeSelectionControls();
        SizeSlider.IsEnabled = true;
    }

    private void ModeList_Checked(object sender, RoutedEventArgs e)
    {
        if (CardsList is null || RowsList is null) return;
        Tile? selected = SelectedTile();
        CardsList.Visibility = Visibility.Collapsed;
        RowsList.Visibility = Visibility.Visible;
        ConfigureRowsItemsSource(forceGroupedView: true);
        SynchronizeSelectionControls();
        if (selected is not null)
        {
            RowsList.ScrollIntoView(selected);
            SelectRealizedContainer(RowsList, selected);
            QueueListSelectionVisualSync(selected);
        }
        Dispatcher.BeginInvoke(ScheduleListThumbnailViewport, DispatcherPriority.Render);
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
        var merged = NormalizeFolderSet(_currentFolderSet.Concat(folders));
        if (!merged.Any(Directory.Exists) || merged.SequenceEqual(_currentFolderSet, StringComparer.OrdinalIgnoreCase))
            return false;

        await LoadFolderSetAsync(merged);
        return true;
    }

    private void FolderDropTarget_DragEnter(object sender, DragEventArgs e) => UpdateFolderDropAffordance(sender, e);

    private void FolderDropTarget_DragOver(object sender, DragEventArgs e) => UpdateFolderDropAffordance(sender, e);

    private void FolderDropTarget_DragLeave(object sender, DragEventArgs e)
    {
        // WPF raises DragLeave on the parent while the cursor crosses between
        // children.  Keep the affordance until the pointer actually exits this
        // target, rather than flashing it off between gallery elements.
        if (sender is FrameworkElement target && PointerIsInside(target, e))
            return;

        SetFolderDropAffordance(sender, visible: false);
        e.Handled = true;
    }

    private async void FolderDropTarget_Drop(object sender, DragEventArgs e)
    {
        SetFolderDropAffordance(sender, visible: false);
        DroppedFolderSet dropped = ReadDroppedFolders(e.Data);
        bool landing = ReferenceEquals(sender, Landing);
        await ApplyDroppedFoldersAsync(dropped, landing);
        e.Handled = true;
    }

    private void UpdateFolderDropAffordance(object sender, DragEventArgs e)
    {
        DroppedFolderSet dropped = ReadDroppedFolders(e.Data);
        bool accepted = dropped.Folders.Count > 0;
        SetFolderDropAffordance(sender, accepted);

        // The intake surface accepts only existing folders.  Image FileDrop
        // payloads remain Copy payloads at their source, but are explicitly not a
        // valid drop here, so they cannot be mistaken for a folder intake action.
        e.Effects = accepted ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private static bool PointerIsInside(FrameworkElement target, DragEventArgs e)
    {
        if (target.ActualWidth <= 0 || target.ActualHeight <= 0)
            return false;

        Point point = e.GetPosition(target);
        return point.X >= 0 && point.X <= target.ActualWidth
            && point.Y >= 0 && point.Y <= target.ActualHeight;
    }

    private void SetFolderDropAffordance(object sender, bool visible)
    {
        if (ReferenceEquals(sender, Landing))
            LandingFolderDropOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        else if (ReferenceEquals(sender, ViewerFolderDropTarget))
            ViewerFolderDropOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private DroppedFolderSet ReadDroppedFolders(IDataObject? data)
    {
        if (data is null || !data.GetDataPresent(DataFormats.FileDrop))
            return new DroppedFolderSet([], 1, "drop existing folders from Explorer");

        try
        {
            return data.GetData(DataFormats.FileDrop) is string[] paths
                ? ReadDroppedFolders(paths)
                : new DroppedFolderSet([], 1, "folder payload was unavailable");
        }
        catch
        {
            return new DroppedFolderSet([], 1, "folder payload could not be read");
        }
    }

    private DroppedFolderSet ReadDroppedFolders(IEnumerable<string> paths)
    {
        var folders = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int rejected = 0;
        string reason = "drop existing absolute folders only";

        foreach (string raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw) || !Path.IsPathFullyQualified(raw))
            {
                rejected++;
                reason = "folder paths must be absolute";
                continue;
            }

            try
            {
                string lexical = Path.GetFullPath(raw);
                if (!Directory.Exists(lexical))
                {
                    rejected++;
                    reason = "only existing folders are accepted";
                    continue;
                }

                string canonical = _resolveFinalPath(lexical);
                if (!Directory.Exists(canonical))
                {
                    rejected++;
                    reason = "the canonical folder is unavailable";
                    continue;
                }

                if (seen.Add(canonical))
                    folders.Add(canonical);
            }
            catch
            {
                rejected++;
                reason = "a folder path could not be canonicalized";
            }
        }

        return new DroppedFolderSet(folders, rejected, reason);
    }

    private async Task<FolderDropSmokeSnapshot> ApplyDroppedFoldersAsync(DroppedFolderSet dropped, bool landing)
    {
        if (dropped.Folders.Count == 0)
        {
            string rejectedMessage = $"Folder drop rejected: {dropped.RejectionReason}.";
            ReportFolderDropStatus(rejectedMessage, landing);
            return new FolderDropSmokeSnapshot(false, 0, dropped.RejectedCount, dropped.RejectionReason, landing, LandingFolderSetSnapshot().ToList(), _currentFolderSet.ToList(), rejectedMessage);
        }

        int added;
        if (landing)
            added = AppendLandingFolders(dropped.Folders);
        else
        {
            int before = _currentFolderSet.Count;
            await AddFoldersToCurrentSetAsync(dropped.Folders);
            added = Math.Max(0, _currentFolderSet.Count - before);
        }

        string message = added > 0
            ? $"Added {added:N0} folder(s) by reference; files were not copied or moved."
            : "Dropped folders are already in the current folder set.";
        if (dropped.RejectedCount > 0)
            message += $" Skipped {dropped.RejectedCount:N0}: {dropped.RejectionReason}.";
        ReportFolderDropStatus(message, landing);
        return new FolderDropSmokeSnapshot(true, added, dropped.RejectedCount, dropped.RejectionReason, landing, LandingFolderSetSnapshot().ToList(), _currentFolderSet.ToList(), message);
    }

    private void ReportFolderDropStatus(string message, bool landing)
    {
        if (landing)
            LandingFolderStatusText.Text = message;
        else
            SetStatusToast(message);
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
        _scanCancelable = false;
        _loadPhase = "idle";
        CancelScanButton.Visibility = Visibility.Collapsed;
        CancelScanButton.IsEnabled = false;
        ScanBar.Value = 0;
        Landing.Visibility = landing ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void StartScan_Click(object sender, RoutedEventArgs e) => await OpenLandingFolderSetAsync();

    private void CancelScan_Click(object sender, RoutedEventArgs e) => CancelActiveScan();

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
            await LoadFolderSetAsync(_currentFolderSet, commitRecent: false);
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
            Trace.TraceWarning($"Explorer source canonicalization failed: {ex.GetType().Name}");
            return Fail("canonical path could not be resolved", out reason);
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
        bool sourceChanged = !string.Equals(_modalSourceTilePath, t.Path, StringComparison.OrdinalIgnoreCase);
        if (opening)
            _modalFocusBeforeOverlay = Keyboard.FocusedElement;
        if (!string.Equals(_modalTransformPath, t.Path, StringComparison.OrdinalIgnoreCase))
            ResetModalTransform(t.Path);
        if (sourceChanged)
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
        if (opening)
            SetModalMetadataSidebarVisible(false);
        Modal.Visibility = Visibility.Visible;
        if (opening)
            Dispatcher.BeginInvoke(Modal.Focus, DispatcherPriority.Input);
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

        if (opening || sourceChanged)
            BeginModalEnhancementRefresh(t.Path);
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
            bitmap = await Task.Run(() =>
            {
                if (_modalDecodeDelayForSmokeMs > 0)
                    Thread.Sleep(_modalDecodeDelayForSmokeMs);
                return LoadBitmap(displayPath, 1400);
            }, token);
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

        if (token.IsCancellationRequested)
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
                    {
                        completion.TrySetResult(false);
                        return;
                    }
                    if (SelectedTile()?.Path != selectedPath)
                    {
                        completion.TrySetResult(false);
                        return;
                    }
                    if (!string.Equals(_modalDisplayPath, displayPath, StringComparison.OrdinalIgnoreCase))
                    {
                        completion.TrySetResult(false);
                        return;
                    }

                    if (bitmap is null)
                    {
                        // OpenModal intentionally shows an immediate preview or
                        // thumbnail while the full decode runs. If the source
                        // changed underneath that decode, clear the immediate
                        // bitmap so a stale image is never mistaken for success.
                        ModalBitmap.Source = null;
                        ModalBitmap.Visibility = Visibility.Collapsed;
                        ModalArtBase.Visibility = Visibility.Visible;
                        ModalArtGlow.Visibility = Visibility.Visible;
                        ReportCurrentImageDecodeFailure();
                        completion.TrySetResult(false);
                        return;
                    }

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

    private void CloseModal_Click(object sender, RoutedEventArgs e) => CloseModal(restoreFocus: true);

    private void ModalBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only a click whose hit target is the empty black surface closes the
        // preview. Image clicks, edge zones, metadata, and toolbar controls
        // retain their own interactions.
        if (TryCloseModalFromBackdrop(e.OriginalSource))
        {
            e.Handled = true;
        }
    }

    private bool TryCloseModalFromBackdrop(object? hitTarget)
    {
        if (Modal.Visibility != Visibility.Visible
            || (!ReferenceEquals(hitTarget, Modal) && !ReferenceEquals(hitTarget, ModalImageArea)))
        {
            return false;
        }

        CloseModal(restoreFocus: true);
        return true;
    }

    private void ModalZoomOut_Click(object sender, RoutedEventArgs e)
        => AdjustModalZoom(1 / ModalZoomKeyboardStep);

    private void ModalZoomIn_Click(object sender, RoutedEventArgs e)
        => AdjustModalZoom(ModalZoomKeyboardStep);

    private void ModalFit_Click(object sender, RoutedEventArgs e)
        => ResetModalTransform(_modalTransformPath, showFeedback: true);

    private void BeginModalEnhancementRefresh(string sourcePath)
    {
        long generation = ++_modalEnhancementGeneration;
        _modalEnhancementPollTimer.Stop();
        _modalEnhancementJobId = null;
        _modalEnhancementJobStatus = null;
        _modalEnhancementProgress = 0;
        _modalEnhancementError = null;
        UpdateModalEnhancementActionControls();
        _ = RefreshModalEnhancementStateAsync(sourcePath, generation, showUnavailableError: false);
    }

    private async void ModalEnhancementPollTimer_Tick(object? sender, EventArgs e)
    {
        if (_modalEnhancementPolling || Modal.Visibility != Visibility.Visible || SelectedTile() is not Tile tile)
            return;

        _modalEnhancementPolling = true;
        try
        {
            await RefreshModalEnhancementStateAsync(tile.Path, _modalEnhancementGeneration, showUnavailableError: false);
        }
        finally
        {
            _modalEnhancementPolling = false;
        }
    }

    private static Uri ResolveBrowserEnhancementBaseUri()
    {
        string configured = Environment.GetEnvironmentVariable("PHOTOVIEWER_BROWSER_BASE_URL") ?? "http://127.0.0.1:3000/";
        if (Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri)
            && uri.IsLoopback
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        }

        return new Uri("http://127.0.0.1:3000/", UriKind.Absolute);
    }

    private async Task<EnhancementApiResponse> SendEnhancementApiAsync(
        HttpMethod method,
        string relativePath,
        object? body = null,
        CancellationToken token = default)
    {
        try
        {
            Uri endpoint = new(ResolveBrowserEnhancementBaseUri(), relativePath.TrimStart('/'));
            using var request = new HttpRequestMessage(method, endpoint);
            if (body is not null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json");
            }

            using HttpResponseMessage response = await _modalEnhancementSender(request, token);
            string text = await response.Content.ReadAsStringAsync(token);
            JsonElement? payload = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(text);
                    payload = document.RootElement.Clone();
                }
                catch (JsonException)
                {
                }
            }

            string error = "";
            if (!response.IsSuccessStatusCode)
            {
                error = payload is JsonElement root
                    && TryGetStringProperty(root, "error", out string? apiError)
                    ? apiError ?? "Enhancement request failed."
                    : $"Enhancement request failed ({(int)response.StatusCode}).";
            }
            return new EnhancementApiResponse(response.IsSuccessStatusCode, (int)response.StatusCode, payload, error);
        }
        catch (OperationCanceledException)
        {
            return new EnhancementApiResponse(false, 0, null, "Enhancement request was canceled.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            return new EnhancementApiResponse(
                false,
                0,
                null,
                $"Browser AI engine is unavailable at {ResolveBrowserEnhancementBaseUri().GetLeftPart(UriPartial.Authority)}. Start the Browser PhotoViewer, then retry.");
        }
    }

    private static ModalEnhancementJobSnapshot? ParseModalEnhancementJob(JsonElement job)
    {
        if (job.ValueKind != JsonValueKind.Object
            || !TryGetStringProperty(job, "id", out string? id)
            || !TryGetStringProperty(job, "status", out string? status))
        {
            return null;
        }

        TryGetStringProperty(job, "sourcePath", out string? sourcePath);
        if (string.IsNullOrWhiteSpace(sourcePath))
            TryGetStringProperty(job, "sourceId", out sourcePath);
        TryGetStringProperty(job, "outputPath", out string? outputPath);
        TryGetStringProperty(job, "errorMessage", out string? errorMessage);
        int progress = job.TryGetProperty("progress", out JsonElement progressElement)
            && progressElement.TryGetInt32(out int parsedProgress)
            ? Math.Clamp(parsedProgress, 0, 100)
            : 0;
        return new ModalEnhancementJobSnapshot(id!, sourcePath ?? "", status!, progress, outputPath, errorMessage);
    }

    private static ModalEnhancementJobSnapshot? SelectModalEnhancementJob(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("jobs", out JsonElement jobs)
            || jobs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<ModalEnhancementJobSnapshot> parsed = jobs.EnumerateArray()
            .Select(ParseModalEnhancementJob)
            .Where(static job => job is not null)
            .Cast<ModalEnhancementJobSnapshot>()
            .ToList();
        return parsed.FirstOrDefault(static job => job.Status is "queued" or "running")
            ?? parsed.FirstOrDefault(static job => job.Status == "succeeded" && !string.IsNullOrWhiteSpace(job.OutputPath))
            ?? parsed.FirstOrDefault();
    }

    private async Task RefreshModalEnhancementStateAsync(string sourcePath, long generation, bool showUnavailableError)
    {
        string encodedSource = Uri.EscapeDataString(sourcePath);
        EnhancementApiResponse response = await SendEnhancementApiAsync(HttpMethod.Get, $"api/enhance/jobs?sourceId={encodedSource}");
        if (generation != _modalEnhancementGeneration
            || Modal.Visibility != Visibility.Visible
            || SelectedTile() is not Tile tile
            || !string.Equals(tile.Path, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!response.Ok || response.Payload is not JsonElement payload)
        {
            if (showUnavailableError)
                _modalEnhancementError = response.Error;
            UpdateModalEnhancementActionControls();
            return;
        }

        ApplyModalEnhancementJob(tile, SelectModalEnhancementJob(payload));
    }

    private bool IsCurrentModalEnhancementContext(Tile tile, string sourcePath, long generation)
        => generation == _modalEnhancementGeneration
            && Modal.Visibility == Visibility.Visible
            && SelectedTile() is Tile selected
            && ReferenceEquals(selected, tile)
            && string.Equals(selected.Path, sourcePath, StringComparison.OrdinalIgnoreCase);

    private void ApplyModalEnhancementJob(Tile tile, ModalEnhancementJobSnapshot? job)
    {
        // A filtered endpoint should only return jobs for the requested image,
        // but keep the desktop client defensive: never attach another image's
        // job/output to the currently selected tile.
        if (job is not null
            && !string.IsNullOrWhiteSpace(job.SourcePath)
            && !string.Equals(job.SourcePath, tile.Path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _modalEnhancementJobId = job?.Id;
        _modalEnhancementJobStatus = job?.Status;
        _modalEnhancementProgress = job?.Progress ?? 0;
        _modalEnhancementError = job?.ErrorMessage;

        if (job is { Status: "succeeded", OutputPath: not null }
            && File.Exists(job.OutputPath))
        {
            string outputPath = Path.GetFullPath(job.OutputPath);
            tile.Enhanced = true;
            tile.EnhancedOutputPath = outputPath;
            _enhancedOutputs[NormalizeFavoritePath(tile.Path)] = outputPath;
            UpdateModalEnhancedControls(canShowEnhanced: true);
        }

        bool active = job?.Status is "queued" or "running";
        if (active)
            _modalEnhancementPollTimer.Start();
        else
            _modalEnhancementPollTimer.Stop();
        UpdateModalEnhancementActionControls();
    }

    private void UpdateModalEnhancementActionControls()
    {
        if (ModalEnhanceButton is null)
            return;

        bool hasRealSource = SelectedTile() is { IsRealFile: true } tile && File.Exists(tile.Path);
        bool active = _modalEnhancementJobStatus is "queued" or "running";
        bool retryable = _modalEnhancementJobStatus is "failed" or "canceled";
        bool hasDeletableOutput = _modalEnhancementJobStatus == "succeeded"
            && !string.IsNullOrWhiteSpace(_modalEnhancementJobId)
            && SelectedTile() is Tile selected
            && TryGetModalEnhancedOutput(selected, out _);

        ModalEnhanceButton.IsEnabled = hasRealSource && !_modalEnhancementRequestPending && !active;
        ModalEnhanceButtonLabel.Text = _modalEnhancementRequestPending ? "Starting" : retryable ? "Retry AI" : "AI x2";
        ModalEnhanceCancelButton.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ModalEnhanceCancelButton.IsEnabled = active && !_modalEnhancementRequestPending;
        ModalEnhancedDeleteButton.Visibility = hasDeletableOutput ? Visibility.Visible : Visibility.Collapsed;
        ModalEnhancedDeleteButton.IsEnabled = hasDeletableOutput && !_modalEnhancementRequestPending;

        string status = _modalEnhancementRequestPending ? "AI: starting…"
            : _modalEnhancementJobStatus == "queued" ? "AI: queued"
            : _modalEnhancementJobStatus == "running" ? $"AI: {_modalEnhancementProgress}%"
            : _modalEnhancementJobStatus == "succeeded" ? "AI: ready"
            : _modalEnhancementJobStatus == "canceled" ? "AI: canceled"
            : _modalEnhancementJobStatus == "failed" ? $"AI failed: {_modalEnhancementError ?? "unknown error"}"
            : _modalEnhancementError ?? "";
        ModalEnhancementStatusText.Text = status;
        ModalEnhancementStatusText.Visibility = string.IsNullOrWhiteSpace(status) ? Visibility.Collapsed : Visibility.Visible;
        ModalEnhancementStatusText.ToolTip = string.IsNullOrWhiteSpace(status) ? null : status;
    }

    private async void StartModalEnhancement_Click(object sender, RoutedEventArgs e)
    {
        if (_modalEnhancementRequestPending || SelectedTile() is not Tile { IsRealFile: true } tile || !File.Exists(tile.Path))
            return;

        long requestGeneration = _modalEnhancementGeneration;
        string sourcePath = tile.Path;
        string? requestJobId = _modalEnhancementJobId;
        _modalEnhancementRequestPending = true;
        _modalEnhancementError = null;
        UpdateModalEnhancementActionControls();
        try
        {
            bool retry = _modalEnhancementJobStatus is "failed" or "canceled"
                && !string.IsNullOrWhiteSpace(requestJobId);
            EnhancementApiResponse response = retry
                ? await SendEnhancementApiAsync(HttpMethod.Post, $"api/enhance/jobs/{Uri.EscapeDataString(requestJobId!)}/retry")
                : await SendEnhancementApiAsync(HttpMethod.Post, "api/enhance/jobs", new
                {
                    sourceId = sourcePath,
                    presetId = "anime-sharp-x2",
                    adapterId = "realesrgan-ncnn",
                    scale = 2,
                });

            if (!IsCurrentModalEnhancementContext(tile, sourcePath, requestGeneration))
                return;

            bool needsConfirmation = response.StatusCode == 409
                && response.Payload is JsonElement conflict
                && TryGetStringProperty(conflict, "code", out string? code)
                && string.Equals(code, "UPSCALE_REQUIRES_CONFIRMATION", StringComparison.Ordinal);
            if (needsConfirmation)
            {
                bool confirmed = _confirmLargeEnhancementForSmoke?.Invoke() ?? MessageBox.Show(
                        this,
                        "This AI upscale is large and may take several minutes. Start it anyway?",
                        "PhotoViewer AI enhancement",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No) == MessageBoxResult.Yes;
                if (confirmed)
                {
                    response = await SendEnhancementApiAsync(HttpMethod.Post, "api/enhance/jobs", new
                    {
                        sourceId = sourcePath,
                        presetId = "anime-sharp-x2",
                        adapterId = "realesrgan-ncnn",
                        scale = 2,
                        confirmLargeJob = true,
                    });
                    if (!IsCurrentModalEnhancementContext(tile, sourcePath, requestGeneration))
                        return;
                }
            }

            if (!response.Ok || response.Payload is not JsonElement payload
                || !payload.TryGetProperty("job", out JsonElement jobElement))
            {
                _modalEnhancementError = response.Error;
                SetStatusToast(response.Error);
                return;
            }

            ApplyModalEnhancementJob(tile, ParseModalEnhancementJob(jobElement));
            ShowModalInteractionFeedback("AI enhancement started");
        }
        finally
        {
            _modalEnhancementRequestPending = false;
            UpdateModalEnhancementActionControls();
        }
    }

    private async void CancelModalEnhancement_Click(object sender, RoutedEventArgs e)
    {
        if (_modalEnhancementRequestPending
            || string.IsNullOrWhiteSpace(_modalEnhancementJobId)
            || SelectedTile() is not Tile tile)
        {
            return;
        }

        long requestGeneration = _modalEnhancementGeneration;
        string sourcePath = tile.Path;
        string requestJobId = _modalEnhancementJobId;
        _modalEnhancementRequestPending = true;
        UpdateModalEnhancementActionControls();
        try
        {
            EnhancementApiResponse response = await SendEnhancementApiAsync(
                HttpMethod.Post,
                $"api/enhance/jobs/{Uri.EscapeDataString(requestJobId)}/cancel");
            if (!IsCurrentModalEnhancementContext(tile, sourcePath, requestGeneration))
                return;
            if (!response.Ok || response.Payload is not JsonElement payload
                || !payload.TryGetProperty("job", out JsonElement jobElement))
            {
                _modalEnhancementError = response.Error;
                SetStatusToast(response.Error);
                return;
            }
            ApplyModalEnhancementJob(tile, ParseModalEnhancementJob(jobElement));
            ShowModalInteractionFeedback("AI enhancement canceled");
        }
        finally
        {
            _modalEnhancementRequestPending = false;
            UpdateModalEnhancementActionControls();
        }
    }

    private async void DeleteModalEnhancedOutput_Click(object sender, RoutedEventArgs e)
    {
        if (_modalEnhancementRequestPending
            || string.IsNullOrWhiteSpace(_modalEnhancementJobId)
            || SelectedTile() is not Tile tile)
        {
            return;
        }

        bool confirmed = _confirmEnhancedOutputDeleteForSmoke?.Invoke() ?? MessageBox.Show(
                this,
                "Delete only this enhanced output? The original image will be kept.",
                "Delete enhanced output",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        if (!confirmed)
            return;

        long requestGeneration = _modalEnhancementGeneration;
        string sourcePath = tile.Path;
        string requestJobId = _modalEnhancementJobId;
        _modalEnhancementRequestPending = true;
        UpdateModalEnhancementActionControls();
        try
        {
            EnhancementApiResponse response = await SendEnhancementApiAsync(
                HttpMethod.Delete,
                $"api/enhance/jobs/{Uri.EscapeDataString(requestJobId)}/output");
            if (!IsCurrentModalEnhancementContext(tile, sourcePath, requestGeneration))
                return;
            if (!response.Ok)
            {
                _modalEnhancementError = response.Error;
                SetStatusToast(response.Error);
                return;
            }

            _modalShowingEnhanced = false;
            tile.Enhanced = false;
            tile.EnhancedOutputPath = null;
            _enhancedOutputs.Remove(NormalizeFavoritePath(tile.Path));
            _modalEnhancementJobId = null;
            _modalEnhancementJobStatus = null;
            _modalEnhancementProgress = 0;
            _modalEnhancementError = null;
            OpenModal();
            ShowModalInteractionFeedback("Enhanced output deleted; original kept");
        }
        finally
        {
            _modalEnhancementRequestPending = false;
            UpdateModalEnhancementActionControls();
        }
    }

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

    private void CloseModal(bool restoreFocus = false)
    {
        bool wasVisible = Modal.Visibility == Visibility.Visible;
        IInputElement? focusTarget = _modalFocusBeforeOverlay;
        _modalFocusBeforeOverlay = null;
        CancelPendingModalSingleClick();
        EndModalPointerGesture();
        _modalCts?.Cancel();
        _modalEnhancementPollTimer.Stop();
        _modalEnhancementGeneration++;
        Modal.Visibility = Visibility.Collapsed;
        _modalShowingEnhanced = false;
        _modalSourceTilePath = null;
        _modalDisplayPath = null;
        _modalEnhancementRequestPending = false;
        _modalEnhancementJobId = null;
        _modalEnhancementJobStatus = null;
        _modalEnhancementProgress = 0;
        _modalEnhancementError = null;
        UpdateModalEnhancedControls(false);
        UpdateModalEnhancementActionControls();
        SetModalChromeVisible(true, showFeedback: false);
        _modalFeedbackTimer.Stop();
        ModalInteractionFeedback.Visibility = Visibility.Collapsed;
        ResetModalTransform();
        if (wasVisible && restoreFocus)
            RestoreOverlayFocus(focusTarget);
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
        // _selectedPaths is the canonical selection. A hidden Grid/List surface
        // may deliberately retain a bounded visual SelectedItem until that
        // surface becomes visible again; never let that stale projection revive
        // a cleared selection for Favorite or Recycle actions.
        if (_selectedPaths.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(_primarySelectedPath))
        {
            var primary = _tiles.FirstOrDefault(tile => string.Equals(tile.Path, _primarySelectedPath, StringComparison.OrdinalIgnoreCase));
            if (primary is not null && _selectedPaths.Contains(primary.Path))
                return primary;
        }

        Tile? projected = CardsList.SelectedItem as Tile ?? RowsList.SelectedItem as Tile;
        if (projected is not null && _selectedPaths.Contains(projected.Path))
        {
            return _tiles.FirstOrDefault(tile =>
                string.Equals(tile.Path, projected.Path, StringComparison.OrdinalIgnoreCase));
        }

        return _tiles.FirstOrDefault(tile => _selectedPaths.Contains(tile.Path));
    }

    private void OpenSelectedExternally_Click(object sender, RoutedEventArgs e)
        => TryOpenSelectedExternally();

    private bool TryOpenSelectedExternally()
    {
        Tile? tile = SelectedTile();
        string canonical = "";
        string reason = "select a source image";
        if (tile is null || !TryValidateFileDropTile(tile, out canonical, out reason))
        {
            SetStatusToast($"Open externally unavailable: {reason}.");
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo(canonical) { UseShellExecute = true };
            if (!_externalFileLauncher(startInfo))
            {
                ReportExternalOpenFailure();
                return false;
            }

            SetStatusToast("Opened the selected image externally.");
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or System.Security.SecurityException)
        {
            Trace.TraceWarning($"External image open failed: {ex.GetType().Name}");
            ReportExternalOpenFailure();
            return false;
        }
    }

    private void ReportExternalOpenFailure()
        => SetStatusToast(
            "Open externally could not start the selected image. Check the default app and try again.",
            () => { TryOpenSelectedExternally(); });

    private void ShowSelectedInFolder_Click(object sender, RoutedEventArgs e)
        => ShowSelectedInFolder();

    private bool ShowSelectedInFolder()
    {
        string reason = "select a source image";
        if (SelectedTile() is not Tile tile || !TryValidateFileDropTile(tile, out string canonical, out reason))
        {
            SetStatusToast($"Show in folder unavailable: {reason}.");
            return false;
        }
        try
        {
            var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add($"/select,{canonical}");
            if (!_explorerLauncher(startInfo))
            {
                SetStatusToast("Show in folder could not start Explorer. Try again.");
                return false;
            }
            SetStatusToast("Opened Explorer with the selected source.");
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Show in folder failed: {ex.GetType().Name}");
            SetStatusToast("Show in folder could not start Explorer. Try again.");
            return false;
        }
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e) => RequestDeleteSelected();

    private void BulkDeleteSelected_Click(object sender, RoutedEventArgs e) => RequestBulkDeleteSelected();

    private void InitializeKeyBindingEditor()
    {
        foreach (KeyBindingDefinition definition in KeyBindingSettings.Definitions)
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 7) };
            var line = new DockPanel { LastChildFill = true };
            var capture = new Button
            {
                Style = (Style)FindResource("GhostButton"),
                MinWidth = 132,
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Tag = definition.Action,
            };
            DockPanel.SetDock(capture, Dock.Right);
            capture.Click += KeyBindingCapture_Click;
            AutomationProperties.SetName(capture, $"{definition.Label} key binding");
            AutomationProperties.SetHelpText(capture, $"{definition.HelpText} Activate, then press a new key combination.");
            var label = new TextBlock
            {
                Text = definition.Label,
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
            };
            line.Children.Add(capture);
            line.Children.Add(label);
            var conflict = new TextBlock
            {
                Foreground = (Brush)FindResource("DangerText"),
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Collapsed,
            };
            AutomationProperties.SetLiveSetting(conflict, AutomationLiveSetting.Polite);
            row.Children.Add(line);
            row.Children.Add(conflict);
            KeyBindingsPanel.Children.Add(row);
            _keyBindingButtons[definition.Action] = capture;
            _keyBindingConflictTexts[definition.Action] = conflict;
        }
        RefreshKeyBindingEditor();
    }

    private void BeginKeyBindingEdit()
    {
        _recordingKeyAction = null;
        _keyBindingCaptureError = null;
        _draftKeyBindings = new Dictionary<ViewerKeyAction, KeyChord>(_keyBindings);
        KeyBindingsStatusText.Text = "Choose an action to record a new key combination.";
        RefreshKeyBindingEditor();
    }

    private void CancelKeyBindingEdit()
    {
        _recordingKeyAction = null;
        _keyBindingCaptureError = null;
        _draftKeyBindings = new Dictionary<ViewerKeyAction, KeyChord>(_keyBindings);
        RefreshKeyBindingEditor();
    }

    private void KeyBindingCapture_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ViewerKeyAction action } button)
            return;
        _recordingKeyAction = action;
        _keyBindingCaptureError = null;
        KeyBindingsStatusText.Text = $"Press the new key combination for {KeyBindingSettings.Definition(action).Label}. Escape cancels recording.";
        RefreshKeyBindingEditor();
        button.Focus();
    }

    private bool CaptureRecordedKey(Key key, ModifierKeys modifiers)
    {
        if (_recordingKeyAction is not ViewerKeyAction action)
            return false;
        if (key == Key.Escape && modifiers == ModifierKeys.None)
        {
            _recordingKeyAction = null;
            _keyBindingCaptureError = null;
            KeyBindingsStatusText.Text = "Key recording canceled. Press Escape again to close App Settings.";
            RefreshKeyBindingEditor();
            return true;
        }
        if (!KeyChord.TryCreate(key, modifiers, out KeyChord chord, out string error))
        {
            _keyBindingCaptureError = error;
            KeyBindingsStatusText.Text = error;
            RefreshKeyBindingEditor();
            return true;
        }
        if (!KeyBindingSettings.IsAllowedForAction(action, chord, out error))
        {
            _keyBindingCaptureError = error;
            KeyBindingsStatusText.Text = error;
            RefreshKeyBindingEditor();
            return true;
        }
        _draftKeyBindings[action] = chord;
        _recordingKeyAction = null;
        _keyBindingCaptureError = null;
        KeyBindingsStatusText.Text = $"Draft: {KeyBindingSettings.Definition(action).Label} = {chord.DisplayText}. Save to apply.";
        RefreshKeyBindingEditor();
        return true;
    }

    private void RefreshKeyBindingEditor()
    {
        if (KeyBindingsPanel is null)
            return;
        IReadOnlyDictionary<ViewerKeyAction, IReadOnlyList<ViewerKeyAction>> conflicts =
            KeyBindingSettings.FindConflicts(_draftKeyBindings);
        foreach (KeyBindingDefinition definition in KeyBindingSettings.Definitions)
        {
            KeyChord chord = _draftKeyBindings.TryGetValue(definition.Action, out KeyChord draft)
                ? draft
                : definition.DefaultChord;
            if (_keyBindingButtons.TryGetValue(definition.Action, out Button? button))
            {
                button.Content = _recordingKeyAction == definition.Action ? "Press key…" : chord.DisplayText;
                button.ToolTip = definition.HelpText;
                AutomationProperties.SetHelpText(
                    button,
                    $"{definition.HelpText} Current draft is {chord.DisplayText}. Activate, then press a new key combination.");
            }
            if (!_keyBindingConflictTexts.TryGetValue(definition.Action, out TextBlock? conflictText))
                continue;
            if (_recordingKeyAction == definition.Action && !string.IsNullOrWhiteSpace(_keyBindingCaptureError))
            {
                conflictText.Text = _keyBindingCaptureError;
                conflictText.Visibility = Visibility.Visible;
                AutomationProperties.SetName(conflictText, $"Invalid key binding for {definition.Label}: {_keyBindingCaptureError}");
            }
            else if (conflicts.TryGetValue(definition.Action, out IReadOnlyList<ViewerKeyAction>? others))
            {
                string labels = string.Join(", ", others.Select(action => KeyBindingSettings.Definition(action).Label));
                conflictText.Text = $"Also assigned to {labels} in an overlapping context.";
                conflictText.Visibility = Visibility.Visible;
                AutomationProperties.SetName(conflictText, $"Key binding conflict: {definition.Label} is also assigned to {labels}");
            }
            else
            {
                conflictText.Text = "";
                conflictText.Visibility = Visibility.Collapsed;
            }
        }
        if (SaveKeyBindingsButton is not null)
            SaveKeyBindingsButton.IsEnabled = conflicts.Count == 0 && _recordingKeyAction is null;
    }

    private void ResetKeyBindings_Click(object sender, RoutedEventArgs e)
    {
        _recordingKeyAction = null;
        _keyBindingCaptureError = null;
        _draftKeyBindings = KeyBindingSettings.CreateDefaults();
        KeyBindingsStatusText.Text = "Default key bindings are in the draft. Save to apply them.";
        RefreshKeyBindingEditor();
    }

    private void SaveKeyBindings_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyDictionary<ViewerKeyAction, IReadOnlyList<ViewerKeyAction>> conflicts =
            KeyBindingSettings.FindConflicts(_draftKeyBindings);
        if (conflicts.Count > 0)
        {
            KeyBindingsStatusText.Text = "Resolve the highlighted key conflicts before saving.";
            RefreshKeyBindingEditor();
            return;
        }

        var previous = new Dictionary<ViewerKeyAction, KeyChord>(_keyBindings);
        _keyBindings = new Dictionary<ViewerKeyAction, KeyChord>(_draftKeyBindings);
        SaveState();
        bool persisted = TryReadViewerStateFile(ResolvedStatePath, out ViewerState? savedState)
            && savedState is not null
            && KeyBindingMapsEqual(
                _keyBindings,
                KeyBindingSettings.NormalizePersisted(savedState.KeyBindings, out _));
        if (!persisted)
        {
            _keyBindings = previous;
            KeyBindingsStatusText.Text = "Key bindings could not be saved. The draft is preserved; fix the local state error and retry.";
            RefreshKeyBindingEditor();
            return;
        }

        _draftKeyBindings = new Dictionary<ViewerKeyAction, KeyChord>(_keyBindings);
        _keyBindingCaptureError = null;
        KeyBindingsStatusText.Text = "Key bindings saved and applied.";
        ApplyKeyBindingTooltips();
        RefreshKeyBindingEditor();
    }

    private static bool KeyBindingMapsEqual(
        IReadOnlyDictionary<ViewerKeyAction, KeyChord> first,
        IReadOnlyDictionary<ViewerKeyAction, KeyChord> second)
        => KeyBindingSettings.Definitions.All(definition =>
            first.TryGetValue(definition.Action, out KeyChord firstChord)
            && second.TryGetValue(definition.Action, out KeyChord secondChord)
            && firstChord == secondChord);

    private void ApplyKeyBindingTooltips()
    {
        if (ModalCloseBtn is null)
            return;
        ModalCloseBtn.ToolTip = $"Close ({BindingText(ViewerKeyAction.CloseModal)})";
        ModalPreviousButton.ToolTip = $"Previous image ({BindingText(ViewerKeyAction.PreviousImage)})";
        ModalNextButton.ToolTip = $"Next image ({BindingText(ViewerKeyAction.NextImage)})";
        ModalFavoriteDecreaseButton.ToolTip = $"Favorite -1 ({BindingText(ViewerKeyAction.FavoriteDecrease)})";
        ModalFavoriteIncreaseButton.ToolTip = $"Favorite +1 ({BindingText(ViewerKeyAction.FavoriteIncrease)})";
        FavoriteDecreaseButton.ToolTip = $"Favorite -1 ({BindingText(ViewerKeyAction.FavoriteDecrease)})";
        FavoriteIncreaseButton.ToolTip = $"Favorite +1 ({BindingText(ViewerKeyAction.FavoriteIncrease)})";
        BulkFavoriteDecreaseButton.ToolTip = $"Decrease favorite level for selected images ({BindingText(ViewerKeyAction.FavoriteDecrease)})";
        BulkFavoriteIncreaseButton.ToolTip = $"Increase favorite level for selected images ({BindingText(ViewerKeyAction.FavoriteIncrease)})";
        ModalDeleteButton.ToolTip = $"Move current source to Recycle Bin ({BindingText(ViewerKeyAction.RecycleCurrentImage)})";
        RestorePreviewTabButton.ToolTip = $"Reopen last closed ({BindingText(ViewerKeyAction.ReopenLastClosedPreviewTab)})";
        ModalFlipButton.ToolTip = $"Flip horizontal ({BindingText(ViewerKeyAction.FlipHorizontal)})";
        ModalEnhancedToggleButton.ToolTip = $"Toggle Original / Enhanced ({BindingText(ViewerKeyAction.ToggleEnhancedPreview)})";
        ModalZoomOutButton.ToolTip = $"Zoom out ({BindingText(ViewerKeyAction.ModalZoomOut)})";
        ModalZoomResetButton.ToolTip = $"Reset to fit ({BindingText(ViewerKeyAction.ModalZoomReset)})";
        ModalZoomInButton.ToolTip = $"Zoom in ({BindingText(ViewerKeyAction.ModalZoomIn)})";
        ModalShortcutHintText.Text = $"   {BindingText(ViewerKeyAction.PreviousImage)} / {BindingText(ViewerKeyAction.NextImage)} navigate   ·   {BindingText(ViewerKeyAction.CloseModal)} close";
    }

    private string BindingText(ViewerKeyAction action)
        => _keyBindings.TryGetValue(action, out KeyChord chord)
            ? chord.DisplayText
            : KeyBindingSettings.Definition(action).DefaultChord.DisplayText;

    private void OpenAppSettings_Click(object sender, RoutedEventArgs e)
    {
        _settingsFocusBeforeDialog = Keyboard.FocusedElement;
        BeginKeyBindingEdit();
        ConfirmBeforeDeleteCheckBox.IsChecked = _confirmBeforeDelete;
        SetShowUnseenDots(_showUnseenDots, persist: false);
        DiagnosticsText.Text = BuildDiagnosticsText();
        DiagnosticsStatusText.Text = "Read-only diagnostics. Copy excludes paths, image metadata, prompts, and personal state.";
        AppSettingsDialog.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(ConfirmBeforeDeleteCheckBox.Focus, DispatcherPriority.Input);
    }

    private void CloseAppSettings_Click(object sender, RoutedEventArgs e)
    {
        CancelKeyBindingEdit();
        AppSettingsDialog.Visibility = Visibility.Collapsed;
        RestoreOverlayFocus(_settingsFocusBeforeDialog);
    }

    private void ConfirmBeforeDelete_Changed(object sender, RoutedEventArgs e)
    {
        _confirmBeforeDelete = ConfirmBeforeDeleteCheckBox.IsChecked == true;
        SaveState();
    }

    private string BuildDiagnosticsText()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(MainWindow).Assembly;
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString() ?? "local build";
        string sourceRevision = Environment.GetEnvironmentVariable("PVU_SOURCE_REVISION")?.Trim() ?? "";
        string sourceDirty = Environment.GetEnvironmentVariable("PVU_SOURCE_DIRTY")?.Trim() ?? "";
        string source = string.IsNullOrWhiteSpace(sourceRevision)
            ? "local build"
            : $"{sourceRevision[..Math.Min(12, sourceRevision.Length)]}{(string.Equals(sourceDirty, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(sourceDirty, "true", StringComparison.OrdinalIgnoreCase) ? " (dirty)" : "")}";
        string buildTime = "unavailable";
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
            {
                DateTime utc = File.GetLastWriteTimeUtc(exe);
                buildTime = $"{utc:yyyy-MM-dd HH:mm:ss} UTC / {utc.ToLocalTime():yyyy-MM-dd HH:mm:ss} local";
            }
        }
        catch { }
        return string.Join(Environment.NewLine, [
            $"PhotoViewer.Wpf {version}",
            $"Source: {source}",
            $"Build: {buildTime}",
            $"Process: {RuntimeInformation.ProcessArchitecture} · {RuntimeInformation.FrameworkDescription}",
            $"Catalog: {_allTiles.Count} · Visible: {_tiles.Count}",
            $"Safety: confirm delete {(_confirmBeforeDelete ? "on" : "off")} · unseen dots {(_showUnseenDots ? "on" : "off")}",
        ]);
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
        => TryCopyDiagnostics();

    private bool TryCopyDiagnostics()
    {
        string text = BuildDiagnosticsText();
        _lastDiagnosticsCopyText = text;
        try
        {
            _diagnosticsClipboardWriter(text);
            DiagnosticsStatusText.Text = "Diagnostics copied. It contains no paths, metadata, prompts, or saved image state.";
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Diagnostics clipboard copy failed: {ex.GetType().Name}");
            DiagnosticsStatusText.Text = "Diagnostics could not be copied. Try again after another app releases the clipboard.";
            return false;
        }
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

        bool refreshModal = ReconcileSuccessfulSourceRecycle([tile]);
        ApplyFilters(selectFirst: false);

        Tile? neighbor = priorFilteredOrder
            .Skip(Math.Max(0, oldIndex + 1))
            .FirstOrDefault(_tiles.Contains)
            ?? priorFilteredOrder
                .Take(Math.Max(0, oldIndex))
                .Reverse()
                .FirstOrDefault(_tiles.Contains);
        if (neighbor is not null)
        {
            SelectTile(neighbor);
            if (refreshModal)
                OpenModal();
        }
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

        bool refreshModal = ReconcileSuccessfulSourceRecycle(succeeded);
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
            if (refreshModal)
                OpenModal();
        }
        else if (remainingSelected.Count > 0)
        {
            SetSelection(remainingSelected, remainingSelected[^1]);
            if (refreshModal)
                OpenModal();
        }
        else
        {
            SelectTile(null);
            CloseModal();
        }

        SaveState();
        if (failed.Count == 0)
        {
            SetStatusToast($"Moved {succeeded.Count:N0} selected images to Recycle Bin.");
        }
        else
        {
            string firstReason = failed.Count > 0 ? $" {failed[0].Reason}" : "";
            SetStatusToast($"Moved {succeeded.Count:N0} image(s); {failed.Count:N0} failed and remain selected.{firstReason}");
        }
        return true;
    }

    /// <summary>
    /// Reconciles WPF-owned UI references after the Recycle Bin backend has
    /// confirmed success. Favorite, Seen, and enhancement data are deliberately
    /// retained: those stores are multi-owner history with separate deletion
    /// authority, not children of the source-file lifecycle.
    /// </summary>
    private bool ReconcileSuccessfulSourceRecycle(IEnumerable<Tile> deletedTiles)
    {
        var deletedPaths = new HashSet<string>(deletedTiles.Select(static tile => tile.Path), StringComparer.OrdinalIgnoreCase);
        var deletedKeys = new HashSet<string>(deletedPaths.Select(NormalizeFavoritePath), StringComparer.OrdinalIgnoreCase);
        long recycleGeneration = ++_sourceRecycleGeneration;
        foreach (string key in deletedKeys)
            _sourceRecycleGenerationByPath[key] = recycleGeneration;
        bool refreshModal = Modal.Visibility == Visibility.Visible;
        bool deletedModalSource = refreshModal
            && !string.IsNullOrWhiteSpace(_modalSourceTilePath)
            && deletedPaths.Contains(_modalSourceTilePath);

        if (deletedModalSource)
            CloseModal();
        if (_hoverPreviewTabPath is not null && deletedPaths.Contains(_hoverPreviewTabPath))
            HidePreviewTabHover();

        _allTiles.RemoveAll(tile => deletedPaths.Contains(tile.Path));
        _selectedPaths.RemoveWhere(deletedPaths.Contains);
        if (_primarySelectedPath is not null && deletedPaths.Contains(_primarySelectedPath))
            _primarySelectedPath = null;
        if (_restoredSelectedPath is not null && deletedPaths.Contains(_restoredSelectedPath))
            _restoredSelectedPath = null;

        foreach (string key in deletedKeys)
            _pinnedPreviewPaths.Remove(key);
        foreach (PreviewTabView tab in _previewTabs.Where(tab => deletedPaths.Contains(tab.Path)).ToList())
            _previewTabs.Remove(tab);
        _closedPreviewTabs.RemoveAll(tile => deletedPaths.Contains(tile.Path));
        _restoredPreviewTabPaths.RemoveAll(path => deletedPaths.Contains(path));
        bool activePreviewTabDeleted = _activePreviewTabPath is not null && deletedPaths.Contains(_activePreviewTabPath);
        if (activePreviewTabDeleted)
            _activePreviewTabPath = null;
        if (activePreviewTabDeleted && _previewTabs.LastOrDefault() is { } nextActiveTab)
            _activePreviewTabPath = nextActiveTab.Path;
        if (_restoredActivePreviewTabPath is not null && deletedPaths.Contains(_restoredActivePreviewTabPath))
            _restoredActivePreviewTabPath = null;
        if (_previewDecodedPath is not null && deletedPaths.Contains(_previewDecodedPath))
            _previewDecodedPath = null;
        if (_currentPreviewMetadataPath is not null && deletedPaths.Contains(_currentPreviewMetadataPath))
        {
            _currentPreviewMetadataPath = null;
            _currentPreviewMetadata = null;
        }

        RefreshPreviewTabs();
        return refreshModal;
    }

    private bool WasSourceRecycledAfter(string path, long generation)
        => _sourceRecycleGenerationByPath.TryGetValue(NormalizeFavoritePath(path), out long recycledAt)
            && recycledAt > generation;

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

        if (IsProtectedDeletePath(lexical, canonical, out reason))
            return false;

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

    private bool IsProtectedDeletePath(string lexical, string canonical, out string reason)
    {
        IReadOnlyList<string> protectedRoots;
        try
        {
            protectedRoots = _protectedDeleteRoots();
        }
        catch
        {
            reason = "protected project/app root could not be verified";
            return true;
        }

        foreach (string root in protectedRoots.Where(static root => !string.IsNullOrWhiteSpace(root)))
        {
            try
            {
                string lexicalRoot = Path.GetFullPath(root);
                string canonicalRoot = _resolveFinalPath(lexicalRoot);
                if (IsPathInside(lexical, lexicalRoot)
                    || IsPathInside(canonical, canonicalRoot)
                    || IsPathInside(lexical, canonicalRoot)
                    || IsPathInside(canonical, lexicalRoot))
                {
                    reason = "source is inside a protected project/app root";
                    return true;
                }
            }
            catch
            {
                // A root that cannot be resolved cannot safely authorize a
                // destructive operation. Fail closed without disclosing it.
                reason = "protected project/app root could not be verified";
                return true;
            }
        }

        reason = "";
        return false;
    }

    private static IReadOnlyList<string> ResolveProtectedDeleteRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static void Add(HashSet<string> destination, string? candidate)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                destination.Add(Path.GetFullPath(candidate));
        }

        Add(roots, AppContext.BaseDirectory);
        Add(roots, FindProjectRoot(Environment.CurrentDirectory));
        Add(roots, FindProjectRoot(AppContext.BaseDirectory));
        Add(roots, FindProjectRoot(Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? ""));
        return roots.ToList();
    }

    private static bool Fail(string value, out string reason)
    {
        reason = value;
        return false;
    }

    private static bool IsPathInside(string candidate, string root)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string prefix = Path.EndsInDirectorySeparator(normalizedRoot)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
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
        if (HasUnresolvedSharedFailures)
        {
            // The toast is a singleton, but Favorite and Seen recovery intents
            // are independent. Never let an unrelated success/warning replace
            // the only reachable route to either retained failed batch.
            retryAction = RetryFailedSharedBatches;
            if (!status.Contains("Retry", StringComparison.OrdinalIgnoreCase))
                status = $"{status} Favorite or Seen changes still need Retry.";
        }
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

    private void ReportCurrentImageDecodeFailure()
    {
        const string currentFailure = "Image could not be decoded. It may be locked, changing, or unavailable. Refresh after fixing the file.";
        bool previousIsRecoverableStatus = _deleteStatus.Contains("selected root(s) were unavailable", StringComparison.OrdinalIgnoreCase)
            || _deleteStatus.Contains("folders could not be scanned", StringComparison.OrdinalIgnoreCase)
            || _deleteStatus.Contains("junction or symbolic-link", StringComparison.OrdinalIgnoreCase)
            || _deleteStatus.Contains("image file(s) could not be decoded", StringComparison.OrdinalIgnoreCase)
            || _deleteStatus.Contains("could not be saved", StringComparison.OrdinalIgnoreCase)
            || _deleteStatus.Contains("is busy in another PhotoViewer window", StringComparison.OrdinalIgnoreCase);
        if (!previousIsRecoverableStatus)
        {
            SetStatusToast(currentFailure);
            return;
        }

        // Scan warnings and persistence refusals explain how to recover data
        // or retry a protected write. A later preview failure must not erase
        // those instructions or its retry action.
        SetStatusToast(
            _deleteStatus.Contains("could not be decoded", StringComparison.OrdinalIgnoreCase)
                ? _deleteStatus
                : $"{_deleteStatus} {currentFailure}",
            _statusRetryAction);
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
            if (Landing.Visibility == Visibility.Visible && OpenFolderSetButton.IsVisible && OpenFolderSetButton.IsEnabled && OpenFolderSetButton.Focus())
                return;
            ListBox activeList = RowsList.Visibility == Visibility.Visible ? RowsList : CardsList;
            if (activeList.IsVisible && activeList.IsEnabled && activeList.Focus())
                return;
            LogoHomeButton.Focus();
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
            _keyBindings = KeyBindingSettings.CreateDefaults();
            _draftKeyBindings = new Dictionary<ViewerKeyAction, KeyChord>(_keyBindings);
            ApplyKeyBindingTooltips();
            RefreshKeyBindingEditor();
            return;
        }

        _stateExtensionData = state.ExtensionData is null ? null : new Dictionary<string, JsonElement>(state.ExtensionData);
        _keyBindings = KeyBindingSettings.NormalizePersisted(state.KeyBindings, out _keyBindingUnknownEntries);
        _draftKeyBindings = new Dictionary<ViewerKeyAction, KeyChord>(_keyBindings);

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
        SetShowUnseenDots(_showUnseenDots, persist: false);
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
        ApplyKeyBindingTooltips();
        RefreshKeyBindingEditor();
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
                KeyBindings = KeyBindingSettings.ToPersisted(_keyBindings, _keyBindingUnknownEntries),
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
                _ = KeyBindingSettings.NormalizePersisted(latest?.KeyBindings, out Dictionary<string, JsonElement>? latestUnknownKeyBindings);
                state.KeyBindings = KeyBindingSettings.ToPersisted(
                    _keyBindings,
                    latest is null ? _keyBindingUnknownEntries : latestUnknownKeyBindings);
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
            _ = KeyBindingSettings.NormalizePersisted(state.KeyBindings, out _keyBindingUnknownEntries);
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
        return ReadSharedRecentFolders(ResolvedSharedRecentPath);
    }

    private static SharedRecentReadResult ReadSharedRecentFolders(string path)
    {
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

    private bool CommitSharedRecentFolderSet(IEnumerable<string> folders)
    {
        var folderSet = NormalizeFolderSet(folders);
        if (folderSet.Count == 0)
            return true;

        string key = FormatRecentFolderSet(folderSet);
        if (string.Equals(key, _lastSuccessfulSharedRecentFolderSetKey, StringComparison.OrdinalIgnoreCase))
            return true;

        _sharedRecentCommitAttemptCount++;
        if (!SaveSharedRecentFolderSet(folderSet))
            return false;

        _lastSuccessfulSharedRecentFolderSetKey = key;
        _sharedRecentCommitSuccessCount++;
        return true;
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
            CloseAppSettings_Click(this, new RoutedEventArgs());
            return true;
        }

        if (Modal.Visibility != Visibility.Visible)
            return false;

        CloseModal(restoreFocus: true);
        return true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = _shortcutModifierProvider();
        if (DeleteConfirmationDialog.Visibility == Visibility.Visible || AppSettingsDialog.Visibility == Visibility.Visible)
        {
            if (AppSettingsDialog.Visibility == Visibility.Visible
                && _recordingKeyAction is not null
                && CaptureRecordedKey(key, modifiers))
            {
                e.Handled = true;
                return;
            }
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

        if (!IsViewerShortcutSurfaceActive())
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        // Modal close remains reachable even when a child Button owns focus.
        // Settings and Recycle confirmation use the fixed Escape rescue above.
        if (Modal.Visibility == Visibility.Visible
            && MatchesBinding(ViewerKeyAction.CloseModal, key, modifiers))
        {
            CloseModal(restoreFocus: true);
            e.Handled = true;
            return;
        }

        // Preview-tab reorder is meaningful only while a tab button owns focus.
        // Handle just these configured chords before the generic Button guard;
        // ordinary buttons and editable inputs still suppress viewer shortcuts.
        if ((MatchesBinding(ViewerKeyAction.MovePreviewTabLeft, key, modifiers)
                && TryReorderFocusedPreviewTab(-1))
            || (MatchesBinding(ViewerKeyAction.MovePreviewTabRight, key, modifiers)
                && TryReorderFocusedPreviewTab(1)))
        {
            e.Handled = true;
            return;
        }

        if (IsGlobalShortcutInputFocused(Keyboard.FocusedElement))
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        if (TryHandleConfiguredViewerShortcut(key, modifiers))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private bool TryHandleConfiguredViewerShortcut(Key key, ModifierKeys modifiers)
    {
        if (!IsViewerShortcutSurfaceActive())
            return false;

        bool modalVisible = Modal.Visibility == Visibility.Visible;
        if (modalVisible)
        {
            if (MatchesBinding(ViewerKeyAction.CloseModal, key, modifiers))
            {
                CloseModal(restoreFocus: true);
                return true;
            }
            if (MatchesBinding(ViewerKeyAction.PreviousImage, key, modifiers))
            {
                NavigateModal(-1);
                return true;
            }
            if (MatchesBinding(ViewerKeyAction.NextImage, key, modifiers))
            {
                NavigateModal(1);
                return true;
            }
            if (MatchesBinding(ViewerKeyAction.FlipHorizontal, key, modifiers))
                return ToggleModalFlip();
            if (MatchesBinding(ViewerKeyAction.ToggleEnhancedPreview, key, modifiers))
                return ToggleModalEnhanced();
            if (MatchesBinding(ViewerKeyAction.ModalZoomIn, key, modifiers))
                return AdjustModalZoom(ModalZoomKeyboardStep);
            if (MatchesBinding(ViewerKeyAction.ModalZoomOut, key, modifiers))
                return AdjustModalZoom(1 / ModalZoomKeyboardStep);
            if (MatchesBinding(ViewerKeyAction.ModalZoomReset, key, modifiers))
                return ResetModalTransform(_modalTransformPath, showFeedback: true);
        }
        else
        {
            if (MatchesBinding(ViewerKeyAction.SelectAllResults, key, modifiers))
                return SelectAllCurrentResults();
            if (MatchesBinding(ViewerKeyAction.ClearSelection, key, modifiers))
                return ClearCurrentSelection();
            if (MatchesBinding(ViewerKeyAction.GalleryZoomIn, key, modifiers))
                return AdjustCardWidth(1);
            if (MatchesBinding(ViewerKeyAction.GalleryZoomOut, key, modifiers))
                return AdjustCardWidth(-1);
            if (MatchesBinding(ViewerKeyAction.GalleryZoomReset, key, modifiers))
                return ResetCardWidth();
        }

        if (MatchesBinding(ViewerKeyAction.FavoriteIncrease, key, modifiers))
            return AdjustSelectedFavorite(1);
        if (MatchesBinding(ViewerKeyAction.FavoriteDecrease, key, modifiers))
            return AdjustSelectedFavorite(-1);
        if (MatchesBinding(ViewerKeyAction.FavoriteLevel1, key, modifiers))
            return SetFavoriteLevelForSelection(1);
        if (MatchesBinding(ViewerKeyAction.FavoriteLevel2, key, modifiers))
            return SetFavoriteLevelForSelection(2);
        if (MatchesBinding(ViewerKeyAction.FavoriteLevel3, key, modifiers))
            return SetFavoriteLevelForSelection(3);
        if (MatchesBinding(ViewerKeyAction.FavoriteLevel4, key, modifiers))
            return SetFavoriteLevelForSelection(4);
        if (MatchesBinding(ViewerKeyAction.FavoriteLevel5, key, modifiers))
            return SetFavoriteLevelForSelection(5);
        if (MatchesBinding(ViewerKeyAction.RecycleCurrentImage, key, modifiers))
            return RequestDeleteSelected();
        if (MatchesBinding(ViewerKeyAction.ReopenLastClosedPreviewTab, key, modifiers))
            return RestoreLastClosedPreviewTab();
        if (MatchesBinding(ViewerKeyAction.MovePreviewTabLeft, key, modifiers))
            return TryReorderFocusedPreviewTab(-1);
        if (MatchesBinding(ViewerKeyAction.MovePreviewTabRight, key, modifiers))
            return TryReorderFocusedPreviewTab(1);
        return false;
    }

    private bool MatchesBinding(ViewerKeyAction action, Key key, ModifierKeys modifiers)
        => _keyBindings.TryGetValue(action, out KeyChord chord)
            && chord.Matches(key, modifiers);

    private bool SelectAllCurrentResults()
    {
        Tile? previousPrimary = SelectedTile();
        Tile? primary = null;
        int selectableCount = 0;
        _selectedPaths.Clear();
        foreach (Tile tile in _tiles)
        {
            if (!tile.IsRealFile)
                continue;
            selectableCount++;
            _selectedPaths.Add(tile.Path);
            primary ??= tile;
            if (ReferenceEquals(tile, previousPrimary))
                primary = tile;
        }

        if (selectableCount == 0 || primary is null)
            return false;

        _primarySelectedPath = primary.Path;
        _selectionVisualSyncGeneration++;
        SynchronizeSelectionControls();
        ApplyPrimarySelection(primary);
        SetStatusToast($"Selected all {selectableCount:N0} current results.");
        return true;
    }

    private bool ClearCurrentSelection()
    {
        if (_selectedPaths.Count == 0)
            return false;
        _selectedPaths.Clear();
        _primarySelectedPath = null;
        _selectionVisualSyncGeneration++;
        SynchronizeSelectionControls();
        ApplyPrimarySelection(null);
        SetStatusToast("Image selection cleared.");
        return true;
    }

    private static bool IsGlobalShortcutInputFocused(object? source)
    {
        // Mouse routed events normally report a template child (TextBoxView,
        // ScrollViewer, Path, TextBlock, etc.) as OriginalSource rather than
        // the owning input/button. Walk both visual and logical ancestry so a
        // Ctrl/Win+wheel gesture over any part of those controls keeps its
        // native behavior instead of leaking into gallery zoom.
        for (DependencyObject? current = source as DependencyObject;
             current is not null;
             current = current is Visual or System.Windows.Media.Media3D.Visual3D
                 ? VisualTreeHelper.GetParent(current)
                 : LogicalTreeHelper.GetParent(current))
        {
            if (current is TextBoxBase or ComboBox or DatePicker or ButtonBase)
                return true;
        }

        return false;
    }

    private bool IsViewerShortcutSurfaceActive()
        => Landing.Visibility != Visibility.Visible;

    private bool IsModalImageWheelSource(DependencyObject? source)
        => source is not null
            && IsDescendantOrSelf(source, ModalImageArea)
            && !IsDescendantOrSelf(source, ModalMetadataSidebar)
            && !IsDescendantOrSelf(source, ModalPreviousButton)
            && !IsDescendantOrSelf(source, ModalNextButton)
            && !IsDescendantOrSelf(source, ModalFooter);

    private static bool IsDescendantOrSelf(DependencyObject source, DependencyObject ancestor)
    {
        for (DependencyObject? current = source; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }
        return false;
    }

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if (DeleteConfirmationDialog.Visibility == Visibility.Visible
            || AppSettingsDialog.Visibility == Visibility.Visible
            || !IsViewerShortcutSurfaceActive())
        {
            base.OnPreviewMouseWheel(e);
            return;
        }

        if (Modal.Visibility == Visibility.Visible)
        {
            if (IsModalImageWheelSource(e.OriginalSource as DependencyObject))
            {
                AdjustModalZoom(e.Delta > 0 ? ModalZoomWheelStep : 1 / ModalZoomWheelStep);
                e.Handled = true;
            }
            else
            {
                base.OnPreviewMouseWheel(e);
            }
            return;
        }

        if (IsGlobalShortcutInputFocused(e.OriginalSource)
            || IsGlobalShortcutInputFocused(Keyboard.FocusedElement))
        {
            base.OnPreviewMouseWheel(e);
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
            Rect restored = NormalizeRestoreBounds(
                _restoreBounds,
                ResolveSafeCurrentMonitorWorkArea(),
                MinWidth,
                MinHeight);
            Width = restored.Width;
            Height = restored.Height;
            Left = restored.Left;
            Top = restored.Top;
            _fakeMaximized = false;
        }
        else
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
            Rect wa = ResolveSafeCurrentMonitorWorkArea();
            Left = wa.Left;
            Top = wa.Top;
            Width = wa.Width;
            Height = wa.Height;
            _fakeMaximized = true;
        }
    }

    private static Rect NormalizeRestoreBounds(Rect saved, Rect workArea, double minWidth, double minHeight)
    {
        double safeMinWidth = Math.Min(workArea.Width, Math.Max(1, minWidth));
        double safeMinHeight = Math.Min(workArea.Height, Math.Max(1, minHeight));
        double width = double.IsFinite(saved.Width) && saved.Width > 0
            ? Math.Clamp(saved.Width, safeMinWidth, workArea.Width)
            : safeMinWidth;
        double height = double.IsFinite(saved.Height) && saved.Height > 0
            ? Math.Clamp(saved.Height, safeMinHeight, workArea.Height)
            : safeMinHeight;
        double left = double.IsFinite(saved.Left)
            ? Math.Clamp(saved.Left, workArea.Left, workArea.Right - width)
            : workArea.Left + ((workArea.Width - width) / 2);
        double top = double.IsFinite(saved.Top)
            ? Math.Clamp(saved.Top, workArea.Top, workArea.Bottom - height)
            : workArea.Top + ((workArea.Height - height) / 2);
        return new Rect(left, top, width, height);
    }

    private Rect ResolveSafeCurrentMonitorWorkArea()
    {
        try
        {
            Rect area = _currentMonitorWorkArea();
            if (!area.IsEmpty
                && double.IsFinite(area.Left) && double.IsFinite(area.Top)
                && double.IsFinite(area.Width) && double.IsFinite(area.Height)
                && area.Width > 0 && area.Height > 0)
                return area;
        }
        catch
        {
            // Fall through to WPF's primary work area if monitor discovery is
            // unavailable. Maximize remains non-destructive and reversible.
        }
        return SystemParameters.WorkArea;
    }

    private Rect ResolveCurrentMonitorWorkArea()
    {
        nint windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (windowHandle == 0)
            return SystemParameters.WorkArea;
        nint monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        if (monitor == 0)
            return SystemParameters.WorkArea;

        var info = new NativeMonitorInfo { Size = (uint)Marshal.SizeOf<NativeMonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
            return SystemParameters.WorkArea;

        Matrix fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        Point topLeft = fromDevice.Transform(new Point(info.WorkArea.Left, info.WorkArea.Top));
        Point bottomRight = fromDevice.Transform(new Point(info.WorkArea.Right, info.WorkArea.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref NativeMonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMonitorInfo
    {
        public uint Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    public Rect WindowBoundsForSmoke => new(Left, Top, Width, Height);
    public bool FakeMaximizedForSmoke => _fakeMaximized;
    public void SetCurrentMonitorWorkAreaForSmoke(Rect area) => _currentMonitorWorkArea = () => area;
    public void SetThrowingMonitorWorkAreaForSmoke() => _currentMonitorWorkArea = () => throw new InvalidOperationException("injected monitor lookup failure");
    public void ResetCurrentMonitorWorkAreaForSmoke() => _currentMonitorWorkArea = ResolveCurrentMonitorWorkArea;
    public void ToggleMaximizeForSmoke() => Maximize_Click(this, new RoutedEventArgs());
    public Rect RestoreFromFakeMaximizeForSmoke(Rect savedBounds)
    {
        _restoreBounds = savedBounds;
        _fakeMaximized = true;
        Maximize_Click(this, new RoutedEventArgs());
        return WindowBoundsForSmoke;
    }
    public static Rect NormalizeRestoreBoundsForSmoke(Rect savedBounds, Rect workArea, double minWidth = 900, double minHeight = 560)
        => NormalizeRestoreBounds(savedBounds, workArea, minWidth, minHeight);

    public string? SelectedPathForSmoke => SelectedTile()?.Path;
    public string? SelectedFileNameForSmoke => SelectedTile()?.FileName;
    public static string BuildScanWarningForSmoke(int accessFailureCount, int boundarySkipCount, int unavailableRootCount, int decodeFailureCount)
        => BuildScanWarning(accessFailureCount, boundarySkipCount, unavailableRootCount, decodeFailureCount);
    public static List<string> SupportedImageExtensionsForSmoke
        => SupportedImageExtensions.OrderBy(static extension => extension, StringComparer.OrdinalIgnoreCase).ToList();
    public string SearchQueryForSmoke => SearchInput.Text;
    public bool IsEditableTextInputFocusedForSmoke => Keyboard.FocusedElement is TextBoxBase;
    public string StatePathForSmoke => ResolvedStatePath;
    public string FavoritesPathForSmoke => ResolvedFavoritesPath;
    public string SeenPathForSmoke => ResolvedSeenPath;
    public string SharedRecentPathForSmoke => ResolvedSharedRecentPath;
    public string? MetadataIndexPathForSmoke => _metadataIndexPath;
    public string MetadataIndexStatusForSmoke => _metadataIndexStatus;
    public int MetadataIndexProgressForSmoke => _metadataIndexProgress;
    public int MetadataIndexCompletedForSmoke => _metadataIndexCompleted;
    public int MetadataIndexTotalForSmoke => _metadataIndexTotal;
    public int MetadataIndexCacheHitsForSmoke => _metadataIndexCacheHits;
    public int MetadataIndexCacheMissesForSmoke => _metadataIndexCacheMisses;
    public string MetadataIndexStatusTextForSmoke => MetadataIndexStatusText.Text;
    public bool MetadataIndexProgressVisibleForSmoke => MetadataIndexProgressBar.Visibility == Visibility.Visible;
    public static string ResolveSharedProjectRootForSmoke(string start)
    {
        string fullStart = Path.GetFullPath(start);
        string projectRoot = FindProjectRoot(fullStart) ?? fullStart;
        return ResolveMainCheckoutRoot(projectRoot) ?? projectRoot;
    }
    public int CatalogCountForSmoke => _allTiles.Count;
    public List<string> AllFileNamesForSmoke => _allTiles.Select(static tile => tile.FileName).ToList();
    public string DeleteStatusForSmoke => _deleteStatus;
    public bool DeleteConfirmationVisibleForSmoke => DeleteConfirmationDialog.Visibility == Visibility.Visible;
    public bool DeleteStatusVisibleForSmoke => DeleteStatusToast.Visibility == Visibility.Visible;
    public bool DeleteStatusRetryVisibleForSmoke => DeleteStatusRetryButton.Visibility == Visibility.Visible;
    public bool AppSettingsVisibleForSmoke => AppSettingsDialog.Visibility == Visibility.Visible;
    public bool DiagnosticsSurfaceContractForSmoke
        => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(DiagnosticsText))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(CopyDiagnosticsButton))
            && AutomationProperties.GetLiveSetting(DiagnosticsStatusText) == AutomationLiveSetting.Polite
            && AppSettingsDialogSurface.MaxHeight > 0;
    public DiagnosticsSmokeSnapshot CopyDiagnosticsForSmoke(bool injectClipboardFailure)
    {
        Action<string> previous = _diagnosticsClipboardWriter;
        _diagnosticsClipboardWriter = injectClipboardFailure
            ? _ => throw new ExternalException("clipboard unavailable")
            : _ => { };
        try
        {
            bool copied = TryCopyDiagnostics();
            return new DiagnosticsSmokeSnapshot(copied, _lastDiagnosticsCopyText, DiagnosticsStatusText.Text, DiagnosticsSurfaceContractForSmoke, IsSettingsDialogFocusedForSmoke);
        }
        finally
        {
            _diagnosticsClipboardWriter = previous;
        }
    }
    public bool FocusDiagnosticsForSmoke() => CopyDiagnosticsButton.Focus();
    public bool FocusAppSettingsDoneForSmoke() => AppSettingsDoneButton.Focus();
    public ExternalOpenSmokeSnapshot ActivateExternalOpenForSmoke(string launcherBehavior)
    {
        ProcessStartInfo? captured = null;
        Func<ProcessStartInfo, bool> previous = _externalFileLauncher;
        _externalFileLauncher = info =>
        {
            captured = info;
            return launcherBehavior switch
            {
                "success" => true,
                "failure" => false,
                "win32" => throw new Win32Exception(1155, "injected missing file association"),
                "io" => throw new IOException("injected shell I/O failure"),
                "access" => throw new UnauthorizedAccessException("injected shell access failure"),
                "path" => throw new ArgumentException("injected shell path failure"),
                _ => throw new ArgumentOutOfRangeException(nameof(launcherBehavior)),
            };
        };
        try
        {
            string? selectedBefore = SelectedTile()?.Path;
            bool focused = ModalOpenExternalButton.Focus();
            ModalOpenExternalButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool launched = captured is not null
                && string.Equals(_deleteStatus, "Opened the selected image externally.", StringComparison.Ordinal);
            return new ExternalOpenSmokeSnapshot(
                launched,
                captured is not null,
                captured?.FileName ?? "",
                captured?.UseShellExecute ?? false,
                _deleteStatus,
                DeleteStatusRetryButton.Visibility == Visibility.Visible,
                focused && ModalOpenExternalButton.IsKeyboardFocused,
                string.Equals(selectedBefore, SelectedTile()?.Path, StringComparison.OrdinalIgnoreCase),
                SelectedTile()?.Path,
                Modal.Visibility == Visibility.Visible,
                string.Equals(AutomationProperties.GetName(ModalOpenExternalButton), "Open selected image externally", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(ModalOpenExternalButton.ToolTip?.ToString()));
        }
        finally
        {
            _externalFileLauncher = previous;
        }
    }

    public ExplorerRevealSmokeSnapshot ShowSelectedInFolderForSmoke()
        => ActivateExplorerRevealForSmoke("right-preview", "success");

    public ExplorerRevealSmokeSnapshot ActivateExplorerRevealForSmoke(string surface, string launcherBehavior)
    {
        ProcessStartInfo? captured = null;
        Func<ProcessStartInfo, bool> previous = _explorerLauncher;
        _explorerLauncher = info =>
        {
            captured = info;
            return launcherBehavior switch
            {
                "success" => true,
                "failure" => false,
                "throw" => throw new InvalidOperationException("injected explorer failure with private fixture path"),
                _ => throw new ArgumentOutOfRangeException(nameof(launcherBehavior)),
            };
        };
        try
        {
            Button button = string.Equals(surface, "modal", StringComparison.OrdinalIgnoreCase)
                ? ModalRevealButton
                : RightPreviewRevealButton;
            if (ReferenceEquals(button, ModalRevealButton) && Modal.Visibility != Visibility.Visible)
                OpenModal();
            bool focused = button.Focus();
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool launched = captured is not null
                && string.Equals(_deleteStatus, "Opened Explorer with the selected source.", StringComparison.Ordinal);
            return new ExplorerRevealSmokeSnapshot(
                launched,
                captured?.FileName ?? "",
                captured?.ArgumentList.ToList() ?? [],
                captured?.Arguments ?? "",
                captured?.UseShellExecute ?? false,
                ExplorerRevealSurfaceContractForSmoke,
                focused && button.IsKeyboardFocused,
                _deleteStatus,
                ReferenceEquals(button, ModalRevealButton) ? "modal" : "right-preview");
        }
        finally { _explorerLauncher = previous; }
    }

    public ExplorerRevealValidationSnapshot ValidateExplorerRevealPathForSmoke(
        string path,
        bool includeInCatalog,
        bool isRealFile = true)
    {
        var tile = new Tile { Path = path, FileName = Path.GetFileName(path), IsRealFile = isRealFile };
        if (includeInCatalog)
            _allTiles.Add(tile);
        try
        {
            bool accepted = TryValidateFileDropTile(tile, out string canonical, out string reason);
            return new ExplorerRevealValidationSnapshot(accepted, canonical, reason);
        }
        finally
        {
            _allTiles.Remove(tile);
        }
    }

    public bool ExplorerRevealSurfaceContractForSmoke
        => string.Equals(AutomationProperties.GetName(RightPreviewRevealButton), "Show selected source in folder", StringComparison.Ordinal)
            && string.Equals(AutomationProperties.GetName(ModalRevealButton), "Show selected source in folder", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(RightPreviewRevealButton.ToolTip?.ToString())
            && !string.IsNullOrWhiteSpace(ModalRevealButton.ToolTip?.ToString());
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

    public void SetProtectedDeleteRootsForSmoke(params string[] roots)
    {
        string[] snapshot = roots
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _protectedDeleteRoots = () => snapshot;
    }

    public void ResetProtectedDeleteRootsForSmoke() => _protectedDeleteRoots = ResolveProtectedDeleteRoots;

    public void SetConfirmBeforeDeleteForSmoke(bool value) => _confirmBeforeDelete = value;
    public void FlushStateForSmoke()
    {
        _searchStateSaveTimer.Stop();
        SaveState();
    }
    public int SharedRecentCommitAttemptCountForSmoke => _sharedRecentCommitAttemptCount;
    public int SharedRecentCommitSuccessCountForSmoke => _sharedRecentCommitSuccessCount;
    public async Task RefreshActiveFolderForSmokeAsync()
    {
        if (_currentFolderSet.Any(Directory.Exists))
            await LoadFolderSetAsync(_currentFolderSet, commitRecent: false);
    }
    public int ShutdownPersistenceFlushCountForSmoke => _shutdownPersistenceFlushCount;
    public bool RequestDeleteSelectedForSmoke() => RequestDeleteSelected();
    public bool RequestBulkDeleteSelectedForSmoke() => RequestBulkDeleteSelected();
    public void CancelDeleteForSmoke() => DeleteCancel_Click(this, new RoutedEventArgs());
    public void ConfirmDeleteForSmoke(bool doNotAskAgain)
    {
        DoNotAskAgainCheckBox.IsChecked = doNotAskAgain;
        DeleteConfirm_Click(this, new RoutedEventArgs());
    }
    public void OpenAppSettingsForSmoke() => OpenAppSettings_Click(this, new RoutedEventArgs());
    public bool KeyBindingSurfaceContractForSmoke
        => KeyBindingsPanel.Children.Count == KeyBindingSettings.Definitions.Count
            && string.Equals(AutomationProperties.GetName(KeyBindingsPanel), "Editable key bindings", StringComparison.Ordinal)
            && string.Equals(AutomationProperties.GetName(ResetKeyBindingsButton), "Reset key bindings to defaults", StringComparison.Ordinal)
            && string.Equals(AutomationProperties.GetName(SaveKeyBindingsButton), "Save key bindings", StringComparison.Ordinal)
            && AutomationProperties.GetLiveSetting(KeyBindingsStatusText) == AutomationLiveSetting.Polite
            && KeyBindingSettings.Definitions.All(definition =>
                _keyBindingButtons.TryGetValue(definition.Action, out Button? button)
                && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))
                && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(button)));
    public bool KeyBindingSaveEnabledForSmoke => SaveKeyBindingsButton.IsEnabled;
    public bool KeyBindingHintsMatchForSmoke
        => ModalCloseBtn.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.CloseModal), StringComparison.Ordinal) == true
            && ModalPreviousButton.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.PreviousImage), StringComparison.Ordinal) == true
            && ModalNextButton.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.NextImage), StringComparison.Ordinal) == true
            && FavoriteDecreaseButton.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.FavoriteDecrease), StringComparison.Ordinal) == true
            && FavoriteIncreaseButton.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.FavoriteIncrease), StringComparison.Ordinal) == true
            && ModalDeleteButton.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.RecycleCurrentImage), StringComparison.Ordinal) == true
            && RestorePreviewTabButton.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.ReopenLastClosedPreviewTab), StringComparison.Ordinal) == true
            && ModalFlipButton.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.FlipHorizontal), StringComparison.Ordinal) == true
            && ModalEnhancedToggleButton.ToolTip?.ToString()?.Contains(BindingText(ViewerKeyAction.ToggleEnhancedPreview), StringComparison.Ordinal) == true
            && ModalShortcutHintText.Text.Contains(BindingText(ViewerKeyAction.CloseModal), StringComparison.Ordinal);
    public bool KeyBindingRecordingForSmoke => _recordingKeyAction is not null;
    public string KeyBindingStatusForSmoke => KeyBindingsStatusText.Text;
    public int KeyBindingConflictCountForSmoke => KeyBindingSettings.FindConflicts(_draftKeyBindings).Count;
    public string? KeyBindingTextForSmoke(string storageName, bool draft)
    {
        KeyBindingDefinition? definition = KeyBindingSettings.Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.StorageName, storageName, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
            return null;
        IReadOnlyDictionary<ViewerKeyAction, KeyChord> source = draft ? _draftKeyBindings : _keyBindings;
        return source.TryGetValue(definition.Action, out KeyChord chord) ? chord.CanonicalText : null;
    }
    public bool SetKeyBindingDraftForSmoke(string storageName, Key key, ModifierKeys modifiers)
    {
        KeyBindingDefinition? definition = KeyBindingSettings.Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.StorageName, storageName, StringComparison.OrdinalIgnoreCase));
        if (definition is null
            || !KeyChord.TryCreate(key, modifiers, out KeyChord chord, out _)
            || !KeyBindingSettings.IsAllowedForAction(definition.Action, chord, out _))
            return false;
        _recordingKeyAction = null;
        _draftKeyBindings[definition.Action] = chord;
        RefreshKeyBindingEditor();
        return true;
    }
    public bool BeginKeyBindingCaptureForSmoke(string storageName)
    {
        KeyBindingDefinition? definition = KeyBindingSettings.Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.StorageName, storageName, StringComparison.OrdinalIgnoreCase));
        if (definition is null || !_keyBindingButtons.TryGetValue(definition.Action, out Button? button))
            return false;
        KeyBindingCapture_Click(button, new RoutedEventArgs());
        return _recordingKeyAction == definition.Action;
    }
    public bool SaveKeyBindingsForSmoke()
    {
        SaveKeyBindings_Click(SaveKeyBindingsButton, new RoutedEventArgs());
        return KeyBindingsStatusText.Text.Contains("saved and applied", StringComparison.OrdinalIgnoreCase);
    }
    public void ResetKeyBindingsForSmoke()
        => ResetKeyBindings_Click(ResetKeyBindingsButton, new RoutedEventArgs());
    public bool ActivateLogoForSmoke()
    {
        LogoHomeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return Landing.Visibility == Visibility.Visible;
    }
    public bool ActivateViewerForSmoke()
    {
        SetPhase(landing: false);
        return Landing.Visibility != Visibility.Visible;
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
    public string LandingFolderStatusForSmoke => LandingFolderStatusText.Text;
    public bool LandingVisibleForSmoke => Landing.Visibility == Visibility.Visible;
    public string LoadPhaseForSmoke => _loadPhase;
    public bool CancelScanVisibleForSmoke => CancelScanButton.Visibility == Visibility.Visible;
    public bool CancelScanEnabledForSmoke => CancelScanButton.IsEnabled;
    public bool OpenFolderSetFocusedForSmoke => OpenFolderSetButton.IsKeyboardFocused;
    public string ScanLabelForSmoke => ScanLabel.Text;
    public string ScanMessageForSmoke => ScanMessage.Text;
    public double ScanProgressForSmoke => ScanBar.Value;
    public bool ScanCancellationSurfaceContractForSmoke
        => string.Equals(AutomationProperties.GetName(CancelScanButton), "Cancel scan", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(CancelScanButton))
            && string.Equals(AutomationProperties.GetName(ScanMessage), "Scan status", StringComparison.Ordinal)
            && AutomationProperties.GetLiveSetting(ScanMessage) == AutomationLiveSetting.Polite;
    public bool CancelActiveScanForSmoke() => CancelActiveScan();
    public bool CancelBackgroundMetadataForSmoke()
    {
        if (!string.Equals(_loadPhase, "background-metadata", StringComparison.Ordinal)
            || _loadCts is null
            || _loadCts.IsCancellationRequested)
        {
            return false;
        }

        CancellationTokenSource canceled = _loadCts;
        _loadGeneration++;
        _loadCts = null;
        _scanCancelable = false;
        _loadPhase = "metadata-canceled";
        canceled.Cancel();
        _metadataIndexStatus = "canceled";
        RenderMetadataIndexProgress(
            "Prompt metadata canceled - the Viewer and last complete index were kept.",
            _metadataIndexProgress,
            showProgress: false);
        return true;
    }
    public int LoadCtsCreatedCountForSmoke => _loadCtsCreatedCount;
    public int LoadCtsRetiredCountForSmoke => _loadCtsRetiredCount;
    public void ConfigureScanPhaseDelaysForSmoke(int enumerationMilliseconds, int metadataMilliseconds)
    {
        _scanEnumerationDelayForSmokeMs = Math.Max(0, enumerationMilliseconds);
        _scanMetadataDelayForSmokeMs = Math.Max(0, metadataMilliseconds);
    }
    public void SetBeforeMaterializeFilesForSmoke(Action action)
        => _beforeMaterializeFilesForSmoke = action ?? throw new ArgumentNullException(nameof(action));

    public void ConfigureCatalogPreparationBatchesForSmoke(int batchSize, Action<string, int>? hook)
    {
        _catalogPreparationBatchSizeForSmoke = Math.Max(1, batchSize);
        _catalogPreparationBatchHookForSmoke = hook;
    }
    public void ConfigureImageDecodeDelaysForSmoke(int previewMilliseconds, int modalMilliseconds)
    {
        _previewDecodeDelayForSmokeMs = Math.Max(0, previewMilliseconds);
        _modalDecodeDelayForSmokeMs = Math.Max(0, modalMilliseconds);
    }
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
    public bool IsModalDialogFocusedForSmoke => Modal.IsKeyboardFocusWithin;
    public bool ModalFocusTrapConfiguredForSmoke
        => KeyboardNavigation.GetTabNavigation(Modal) == KeyboardNavigationMode.Cycle
            && KeyboardNavigation.GetControlTabNavigation(Modal) == KeyboardNavigationMode.Cycle;
    public bool ModalAccessibilityContractForSmoke
        => string.Equals(AutomationProperties.GetName(Modal), "Image preview dialog", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(Modal))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(ModalPreviousButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(ModalPreviousButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(ModalNextButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(ModalNextButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(ModalMetadataSidebar))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(ModalMetadataSidebar))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(ModalPromptTabButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(ModalPromptTabButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(ModalNegativeTabButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(ModalNegativeTabButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(ModalSettingsTabButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(ModalSettingsTabButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(CopyModalPromptButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(CopyModalPromptButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(CopyModalNegativeButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(CopyModalNegativeButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(CopyModalMetadataButton))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(CopyModalMetadataButton));
    public string? PreviewDecodedPathForSmoke => _previewDecodedPath;
    public string? PreviewMetadataPathForSmoke => _currentPreviewMetadataPath;
    public string? ModalSourcePathForSmoke => _modalSourceTilePath;
    public int PreviewBitmapPixelWidthForSmoke => (PreviewBitmap.Source as BitmapSource)?.PixelWidth ?? 0;
    public int PreviewBitmapPixelHeightForSmoke => (PreviewBitmap.Source as BitmapSource)?.PixelHeight ?? 0;
    public int ModalBitmapPixelWidthForSmoke => (ModalBitmap.Source as BitmapSource)?.PixelWidth ?? 0;
    public int ModalBitmapPixelHeightForSmoke => (ModalBitmap.Source as BitmapSource)?.PixelHeight ?? 0;
    public int ThumbnailPixelWidthForSmoke(string fileName)
        => (_allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Thumbnail as BitmapSource)?.PixelWidth ?? 0;
    public int ThumbnailPixelHeightForSmoke(string fileName)
        => (_allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Thumbnail as BitmapSource)?.PixelHeight ?? 0;
    public string? PreviewBitmapCenterColorForSmoke => BitmapCenterColorForSmoke(PreviewBitmap.Source as BitmapSource);
    public string? ModalBitmapCenterColorForSmoke => BitmapCenterColorForSmoke(ModalBitmap.Source as BitmapSource);
    public string PreviewSizeTextForSmoke => PreviewSizeText.Text;
    public WeakReference? CapturePreviewBitmapWeakReferenceForSmoke()
        => PreviewBitmap.Source is BitmapSource source ? new WeakReference(source) : null;
    public WeakReference? CaptureModalBitmapWeakReferenceForSmoke()
        => ModalBitmap.Source is BitmapSource source ? new WeakReference(source) : null;
    private static string? BitmapCenterColorForSmoke(BitmapSource? source)
    {
        if (source is null || source.PixelWidth <= 0 || source.PixelHeight <= 0)
            return null;

        BitmapSource readable = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        byte[] pixel = new byte[4];
        readable.CopyPixels(
            new Int32Rect(readable.PixelWidth / 2, readable.PixelHeight / 2, 1, 1),
            pixel,
            stride: 4,
            offset: 0);
        return $"#{pixel[3]:X2}{pixel[2]:X2}{pixel[1]:X2}{pixel[0]:X2}";
    }
    public bool FocusModalCloseForSmoke() => ModalCloseBtn.Focus();
    public bool ModalCloseFocusedForSmoke => ModalCloseBtn.IsKeyboardFocused;
    public bool PreviewPlaceholderVisibleForSmoke
        => PreviewBitmap.Visibility == Visibility.Collapsed
            && PreviewArtBase.Visibility == Visibility.Visible
            && PreviewArtGlow.Visibility == Visibility.Visible;
    public bool ModalPlaceholderVisibleForSmoke
        => ModalBitmap.Visibility == Visibility.Collapsed
            && ModalArtBase.Visibility == Visibility.Visible
            && ModalArtGlow.Visibility == Visibility.Visible;
    public void CloseModalForSmoke() => CloseModal();
    public int FilteredCountForSmoke => _tiles.Count;
    public int SelectedCountForSmoke => _selectedPaths.Count;
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
            && PreviewBitmap.Visibility == Visibility.Collapsed
            && RightPreviewContent.Visibility == Visibility.Collapsed
            && RightPreviewEmptyState.Visibility == Visibility.Visible;
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

    public bool ScrollListThumbnailProbeForSmoke(int index)
    {
        if (RowsList.Visibility != Visibility.Visible || index < 0 || index >= _tiles.Count)
            return false;
        Tile tile = _tiles[index];
        tile.Thumbnail = null;
        _thumbnailDecodeFailures.TryRemove(tile.Path, out _);
        RowsList.ScrollIntoView(tile);
        Dispatcher.BeginInvoke(ScheduleListThumbnailViewport, DispatcherPriority.Render);
        return true;
    }

    public async Task<bool> WaitForListThumbnailProbeForSmokeAsync(int index, int timeoutMilliseconds = 3_000)
    {
        if (index < 0 || index >= _tiles.Count)
            return false;
        Tile tile = _tiles[index];
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (tile.Thumbnail is not null)
                return true;
            await Task.Delay(20);
        }
        return tile.Thumbnail is not null;
    }

    public bool SetGridModeForSmoke()
    {
        ModeGrid.IsChecked = true;
        return CardsList.Visibility == Visibility.Visible;
    }
    public async Task<GridSelectionVisualSmokeSnapshot> WaitForGridSelectionVisualForSmokeAsync(string fileName)
    {
        // Let the production Render-priority resync run. This probe only reads
        // the resulting canonical and visual states; it does not scroll or
        // mutate selection and therefore cannot make a broken path pass.
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);

        Tile? tile = _tiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        string? canonicalPath = SelectedTile()?.Path;
        bool canonicalSelected = tile is not null
            && string.Equals(canonicalPath, tile.Path, StringComparison.OrdinalIgnoreCase)
            && _selectedPaths.Contains(tile.Path)
            && string.Equals(_primarySelectedPath, tile.Path, StringComparison.OrdinalIgnoreCase);
        bool gridWindowContains = tile is not null && _tiles.Contains(tile);
        bool selectedItemsContains = tile is not null && CardsList.SelectedItems.Contains(tile);
        ListBoxItem? container = tile is null
            ? null
            : CardsList.ItemContainerGenerator.ContainerFromItem(tile) as ListBoxItem;

        return new GridSelectionVisualSmokeSnapshot(
            canonicalPath,
            canonicalSelected,
            gridWindowContains,
            selectedItemsContains,
            container is not null,
            container?.IsSelected == true);
    }
    public async Task<ListSelectionVisualSmokeSnapshot> WaitForListSelectionVisualForSmokeAsync(string fileName)
    {
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);

        Tile? tile = _tiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        string? canonicalPath = SelectedTile()?.Path;
        bool canonicalSelected = tile is not null
            && string.Equals(canonicalPath, tile.Path, StringComparison.OrdinalIgnoreCase)
            && _selectedPaths.Contains(tile.Path)
            && string.Equals(_primarySelectedPath, tile.Path, StringComparison.OrdinalIgnoreCase);
        bool selectedItemsContains = tile is not null && RowsList.SelectedItems.Contains(tile);
        ListBoxItem? container = tile is null
            ? null
            : RowsList.ItemContainerGenerator.ContainerFromItem(tile) as ListBoxItem;

        return new ListSelectionVisualSmokeSnapshot(
            canonicalPath,
            canonicalSelected,
            selectedItemsContains,
            container is not null,
            container?.IsSelected == true);
    }
    public int SelectedFavoriteLevelForSmoke => SelectedTile()?.Fav ?? 0;
    public bool SelectedUnseenForSmoke => SelectedTile()?.Unseen == true;
    public int FavoriteStoreCountForSmoke => _favorites.Count(static item => item.Value > 0);
    public int SeenStoreCountForSmoke => _seenPaths.Count;
    public int EnhancedStoreCountForSmoke => _enhancedOutputs.Count;
    public int FavoriteLevelForFileForSmoke(string fileName)
        => _allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Fav ?? -1;
    public bool UnseenForFileForSmoke(string fileName)
        => _allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Unseen == true;
    public bool EnhancedForFileForSmoke(string fileName)
        => _allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Enhanced == true;
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
    public bool SelectedUnseenDotForSmoke => SelectedTile()?.ShowUnseenDot == true;
    public int UnseenCountForSmoke => _allTiles.Count(static tile => tile.Unseen);
    public int VisibleUnseenDotCountForSmoke => _allTiles.Count(static tile => tile.ShowUnseenDot);
    public bool FoldersSectionExpandedForSmoke => _foldersSectionExpanded && FoldersSectionContent.Visibility == Visibility.Visible;
    public int LastInitialUnseenCountForSmoke => _lastInitialUnseenCount;
    public int GridRealizedCountForSmoke
        => FindVisualDescendant<VirtualizingWrapPanel>(CardsList)?.RealizedItemCount ?? 0;
    public int ThumbnailBrowserCacheHitsForSmoke => _thumbnailBrowserCacheHits;
    public static IReadOnlyList<string> BrowserThumbnailCachePathsForSmoke(string path, DateTime modifiedUtc)
        => GetBrowserThumbnailCachePaths(new Tile { Path = Path.GetFullPath(path), ModifiedUtc = modifiedUtc });
    public int GridItemsSourceCountForSmoke => CardsList.Items.Count;
    public bool GridUsesFullExtentVirtualizationForSmoke
        => ReferenceEquals(CardsList.ItemsSource, _tiles)
            && FindVisualDescendant<VirtualizingWrapPanel>(CardsList) is not null;
    public double GridExtentHeightForSmoke
        => FindVisualDescendant<VirtualizingWrapPanel>(CardsList)?.ExtentHeight ?? 0;
    public double GridViewportHeightForSmoke
        => FindVisualDescendant<VirtualizingWrapPanel>(CardsList)?.ViewportHeight ?? 0;
    public int GridFirstVisibleIndexForSmoke
        => FindVisualDescendant<VirtualizingWrapPanel>(CardsList)?.FirstVisibleIndex ?? -1;
    public int GridLastVisibleIndexForSmoke
        => FindVisualDescendant<VirtualizingWrapPanel>(CardsList)?.LastVisibleIndex ?? -1;
    public int GridDeferredCountForSmoke => Math.Max(0, _tiles.Count - GridRealizedCountForSmoke);
    public int GridWindowStartIndexForSmoke
        => Math.Max(0, FindVisualDescendant<VirtualizingWrapPanel>(CardsList)?.FirstRealizedIndex ?? 0);
    public int GridWindowEndIndexForSmoke
        => Math.Max(GridWindowStartIndexForSmoke, (FindVisualDescendant<VirtualizingWrapPanel>(CardsList)?.LastRealizedIndex ?? -1) + 1);
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
    public bool InvokePreviewKeyForSmoke(Key key, ModifierKeys modifiers)
    {
        Func<ModifierKeys> previous = _shortcutModifierProvider;
        _shortcutModifierProvider = () => modifiers;
        try
        {
            return InvokePreviewKeyForSmoke(key);
        }
        finally
        {
            _shortcutModifierProvider = previous;
        }
    }
    public bool InvokePreviewMouseWheelForSmoke(int delta, ModifierKeys modifiers)
        => InvokePreviewMouseWheelForSmoke(delta, modifiers, Keyboard.FocusedElement ?? CardsList);

    private bool InvokePreviewMouseWheelForSmoke(int delta, ModifierKeys modifiers, IInputElement source)
    {
        Func<ModifierKeys> previous = _shortcutModifierProvider;
        _shortcutModifierProvider = () => modifiers;
        try
        {
            var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
            {
                RoutedEvent = Mouse.PreviewMouseWheelEvent,
                Source = source,
            };
            OnPreviewMouseWheel(args);
            return args.Handled;
        }
        finally
        {
            _shortcutModifierProvider = previous;
        }
    }

    public bool InvokeModalImageMouseWheelForSmoke(int delta)
        => InvokePreviewMouseWheelForSmoke(delta, ModifierKeys.None, ModalBitmap);

    public bool InvokeModalMetadataMouseWheelForSmoke(int delta)
        => InvokePreviewMouseWheelForSmoke(delta, ModifierKeys.None, ModalMetadataStatusText);

    public bool SearchInputWheelVisualChildAvailableForSmoke
    {
        get
        {
            SearchInput.ApplyTemplate();
            UpdateLayout();
            return FindVisualDescendant<FrameworkElement>(SearchInput) is not null;
        }
    }

    public bool InvokeSearchInputVisualChildMouseWheelForSmoke(int delta, ModifierKeys modifiers)
    {
        SearchInput.ApplyTemplate();
        UpdateLayout();
        IInputElement source = FindVisualDescendant<FrameworkElement>(SearchInput)
            ?? throw new InvalidOperationException("Search input visual child was not generated.");
        return InvokePreviewMouseWheelForSmoke(delta, modifiers, source);
    }

    public bool ViewerButtonWheelVisualChildAvailableForSmoke
    {
        get
        {
            ToggleSidebar.ApplyTemplate();
            UpdateLayout();
            return FindVisualDescendant<FrameworkElement>(ToggleSidebar) is not null;
        }
    }

    public bool InvokeViewerButtonVisualChildMouseWheelForSmoke(int delta, ModifierKeys modifiers)
    {
        ToggleSidebar.ApplyTemplate();
        UpdateLayout();
        IInputElement source = FindVisualDescendant<FrameworkElement>(ToggleSidebar)
            ?? throw new InvalidOperationException("Viewer button visual child was not generated.");
        return InvokePreviewMouseWheelForSmoke(delta, modifiers, source);
    }

    public int MaterializedSelectionVisualLimitForSmoke => MaxMaterializedSelectionVisualItems;
    public int SelectionVisualItemCountForSmoke => CardsList.SelectedItems.Count + RowsList.SelectedItems.Count;

    public void SeedLargeSelectionCatalogForSmoke(int count, string primaryPath)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        Tile template = _allTiles.FirstOrDefault(tile => string.Equals(tile.Path, primaryPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The large-selection primary fixture is not in the current catalog.");
        string root = Path.GetDirectoryName(template.Path) ?? Path.GetTempPath();
        var tiles = new List<Tile>(count) { template };
        for (int index = 1; index < count; index++)
        {
            tiles.Add(new Tile
            {
                Path = Path.Combine(root, $".selection-smoke-{index:D6}.png"),
                FileName = $"selection-smoke-{index:D6}.png",
                IsRealFile = true,
                Group = template.Group,
                FolderBucketKey = template.FolderBucketKey,
                FolderBucketLabel = template.FolderBucketLabel,
                ModifiedUtc = template.ModifiedUtc,
                CreatedUtc = template.CreatedUtc,
                ModifiedText = template.ModifiedText,
                SizeText = template.SizeText,
                Thumbnail = template.Thumbnail,
                ArtBase = template.ArtBase,
                ArtGlow = template.ArtGlow,
                Fav = 0,
                Unseen = false,
            });
        }

        bool wasSyncingSelection = _syncingSelection;
        _syncingSelection = true;
        try
        {
            _allTiles.Clear();
            _allTiles.AddRange(tiles);
            _tiles.ReplaceAll(tiles);
            _selectedPaths.Clear();
            _primarySelectedPath = null;
        }
        finally
        {
            _syncingSelection = wasSyncingSelection;
        }

        ApplyCardLayoutToAllTiles();
        _galleryVirtualizingPanel?.InvalidateItemLayout();
        _selectionVisualSyncGeneration++;
        SynchronizeSelectionControls();
        ApplyPrimarySelection(null);
        UpdateFolderStats();
    }
    public int GridMaxRealizationCountForSmoke => MaxVirtualizedContainerSmokeCount;
    public double CardWidthForSmoke => SizeSlider.Value;
    public double ListThumbnailSizeForSmoke => _allTiles.FirstOrDefault()?.ListThumbnailSize ?? 0;
    public bool ListUsesRecyclingVirtualizationForSmoke
        => VirtualizingPanel.GetIsVirtualizing(RowsList) && VirtualizingPanel.GetVirtualizationMode(RowsList) == VirtualizationMode.Recycling;
    public int ListRealizedContainerCountForSmoke
        => FindVisualDescendants<ListBoxItem>(RowsList).Count();
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

    internal static void CreateCrashedPersistenceWriterForSmoke(string targetPath, string readyPath)
    {
        targetPath = Path.GetFullPath(targetPath);
        readyPath = Path.GetFullPath(readyPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(readyPath)!);

        // Intentionally do not dispose this lease. Environment.Exit models an
        // abrupt process loss: Windows closes the handle, while the protocol
        // lock and pre-replace temp file remain for the next writer to recover.
        PersistenceLockLease? lease = TryAcquirePersistenceLock(targetPath, PersistenceLockTimeoutMilliseconds);
        if (lease is null)
        {
            File.WriteAllText(readyPath, JsonSerializer.Serialize(new { ok = false, targetPath }));
            Environment.Exit(72);
        }

        string tempPath = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".{Path.GetFileName(targetPath)}.crash-{Environment.ProcessId}-{Guid.NewGuid():N}.tmp");
        using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write("{\"crashedWriter\":true}");
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        File.WriteAllText(readyPath, JsonSerializer.Serialize(new
        {
            ok = true,
            targetPath,
            lockPath = targetPath + ".lock",
            tempPath,
            pid = Environment.ProcessId,
        }));
        Environment.Exit(71);
    }

    internal static bool HoldPersistenceLockForSmoke(string targetPath, string readyPath, int holdMilliseconds)
    {
        targetPath = Path.GetFullPath(targetPath);
        readyPath = Path.GetFullPath(readyPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(readyPath)!);
        using PersistenceLockLease? lease = TryAcquirePersistenceLock(targetPath, PersistenceLockTimeoutMilliseconds);
        if (lease is null)
            return false;

        File.WriteAllText(readyPath, JsonSerializer.Serialize(new
        {
            ok = true,
            targetPath,
            lockPath = targetPath + ".lock",
            pid = Environment.ProcessId,
        }));
        Thread.Sleep(Math.Clamp(holdMilliseconds, 100, 10_000));
        return true;
    }

    internal static bool TryRecoverPersistenceForSmoke(string kind, string targetPath, string key)
    {
        targetPath = Path.GetFullPath(targetPath);
        string normalizedKind = kind.Trim().ToLowerInvariant();
        return normalizedKind switch
        {
            "favorites" => TryMergeFavoriteForSmoke(targetPath, key, 3),
            "seen" => TryMergeSeenForSmoke(targetPath, key),
            "recent" => TryMergeSharedRecentForSmoke(targetPath, key),
            "state" => TryWithPersistenceLock(targetPath, () =>
            {
                if (!TryReadViewerStateFile(targetPath, out ViewerState? state))
                    return false;
                state ??= new ViewerState();
                state.Version = 2;
                state.SearchQuery = key;
                return TryWriteAtomicText(targetPath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            }),
            _ => false,
        };
    }

    /// <summary>
    /// Cross-runtime verifier writer.  It intentionally uses the same create-new
    /// lock, read/merge/atomic-replace path as the interactive recent writer.
    /// The single lastFolderSet is last-lock-holder-wins; recentFolderSets retains
    /// the newest distinct sets from every writer up to the shared cap.
    /// </summary>
    internal static bool TryMergeSharedRecentForSmoke(string path, string folderMarker)
    {
        string normalizedMarker = NormalizeRecentFolderPath(folderMarker) ?? "";
        if (string.IsNullOrWhiteSpace(normalizedMarker))
            return false;

        return TryWithPersistenceLock(path, () =>
        {
            var current = ReadSharedRecentFolders(path);
            if (!current.Ok)
                return false;

            var next = new SharedRecentFoldersState
            {
                LastFolderSet = [normalizedMarker],
                RecentFolderSets = NormalizeRecentFolderSets(
                    new[] { (IReadOnlyList<string>)[normalizedMarker] }.Concat(current.Recent.RecentFolderSets)),
                UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
                ExtensionData = CloneExtensionData(current.Recent.ExtensionData),
            };
            return TryWriteAtomicText(path, JsonSerializer.Serialize(next, SharedRecentJsonOptions));
        });
    }

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
        bool bounded = first is > 0 and <= MaxVirtualizedContainerSmokeCount
            && middle is > 0 and <= MaxVirtualizedContainerSmokeCount
            && last is > 0 and <= MaxVirtualizedContainerSmokeCount;
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
    public bool PreviewRightPanelWidthForSmoke(double width) => SetRightPanelWidth(width);
    public void CommitRightPanelWidthForSmoke() => SaveState();
    public void ToggleRightPanelForSmoke() => ToggleRight_Click(this, new RoutedEventArgs());
    public string? LastGridZoomAnchorPathForSmoke => _lastGridZoomAnchorPath;
    public double LastGridZoomAnchorDriftForSmoke => _lastGridZoomAnchorDrift;
    public string? GridViewportAnchorForSmoke
    {
        get
        {
            VirtualizingWrapPanel? panel = FindVisualDescendant<VirtualizingWrapPanel>(CardsList);
            if (_tiles.Count == 0 || panel is null || panel.FirstVisibleIndex < 0)
                return null;
            int end = Math.Max(panel.FirstVisibleIndex, panel.LastVisibleIndex);
            return _tiles[Math.Clamp((panel.FirstVisibleIndex + end) / 2, 0, _tiles.Count - 1)].FileName;
        }
    }
    public string? CaptureGridViewportAnchorForSmoke() => Path.GetFileName(CaptureGridZoomAnchor()?.Path);
    public bool GridContainsFileForSmoke(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName) && _tiles.Any(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase));
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
    public bool DateGroupingActiveForSmoke
        => UsesDateGrouping
            && FindVisualDescendant<VirtualizingWrapPanel>(CardsList) is { ShowGroupHeaders: true };
    public bool FlatGridActiveForSmoke
        => !UsesDateGrouping
            && FindVisualDescendant<VirtualizingWrapPanel>(CardsList) is { ShowGroupHeaders: false };
    public string DatePresetForSmoke => _datePreset;
    public string? DateFromForSmoke => FormatStateDate(_dateFromLocal);
    public string? DateToForSmoke => FormatStateDate(_dateToLocal);
    public string DateFilterSummaryForSmoke => DateFilterSummary.Text;
    public bool ShowFavoritesOnlyForSmoke => FavoriteOnlyFilter?.IsChecked == true;
    public bool ShowUnfavoriteOnlyForSmoke => UnfavoriteOnlyFilter?.IsChecked == true;
    public bool ShowUnseenDotsForSmoke => _showUnseenDots;
    public bool SidebarUnseenDotsCheckedForSmoke => ShowUnseenDots.IsChecked == true;
    public bool AppSettingsUnseenDotsCheckedForSmoke => AppSettingsUnseenDotsCheckBox.IsChecked == true;
    public bool UnseenDotsSurfaceContractForSmoke
        => string.Equals(AutomationProperties.GetName(ShowUnseenDots), "Show unseen dots in gallery", StringComparison.Ordinal)
            && string.Equals(AutomationProperties.GetName(AppSettingsUnseenDotsCheckBox), "Show unseen dots in gallery", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(ShowUnseenDots))
            && !string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(AppSettingsUnseenDotsCheckBox));
    public bool FocusSidebarUnseenDotsForSmoke() => ShowUnseenDots.Focus();
    public bool FocusAppSettingsUnseenDotsForSmoke() => AppSettingsUnseenDotsCheckBox.Focus();
    public bool IsSidebarUnseenDotsFocusedForSmoke => ShowUnseenDots.IsKeyboardFocused;
    public bool IsAppSettingsUnseenDotsFocusedForSmoke => AppSettingsUnseenDotsCheckBox.IsKeyboardFocused;
    public bool UnseenOnlyForSmoke => UnseenOnlyFilter?.IsChecked == true;
    public List<int> FavoriteFilterLevelsForSmoke => _favoriteFilterLevels.OrderBy(static level => level).ToList();
    public bool GridModeVisibleForSmoke => CardsList.Visibility == Visibility.Visible;
    public bool ListModeVisibleForSmoke => RowsList.Visibility == Visibility.Visible;

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
    {
        if (Modal.Visibility != Visibility.Visible || delta == 0)
            return false;

        int before = SelectedFavoriteLevelForSmoke;
        Button button = delta > 0 ? ModalFavoriteIncreaseButton : ModalFavoriteDecreaseButton;
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        return SelectedFavoriteLevelForSmoke == Math.Clamp(before + Math.Sign(delta), 0, 5);
    }
    public int ModalFavoriteLevelForSmoke
        => int.TryParse(ModalFavoriteLevelText.Text, out int level) ? level : -1;
    public bool MarkSelectedSeenForSmoke() => SelectedTile() is { IsRealFile: true } tile && MarkTileSeen(tile);
    public bool SetFileFavoriteLevelForSmoke(string fileName, int level)
        => _allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase)) is { IsRealFile: true } tile
            && SetFavoriteLevel(tile, level);
    public bool MarkFileSeenForSmoke(string fileName)
        => _allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase)) is { IsRealFile: true } tile
            && MarkTileSeen(tile);
    public bool UnseenDotForFileForSmoke(string fileName)
        => _allTiles.FirstOrDefault(tile => string.Equals(tile.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.ShowUnseenDot == true;
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
        => SetShowUnseenDots(enabled, persist: true);
    public void SetSidebarUnseenDotsForSmoke(bool enabled) => ShowUnseenDots.IsChecked = enabled;
    public void SetAppSettingsUnseenDotsForSmoke(bool enabled) => AppSettingsUnseenDotsCheckBox.IsChecked = enabled;
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
        => InvokeModalImageMouseWheelForSmoke(delta);

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
            && string.Equals(System.Windows.Automation.AutomationProperties.GetName(ModalNextButton), "Next image edge zone", StringComparison.Ordinal)
            && ModalImageArea.ActualWidth > 0
            && Math.Abs((ModalPreviousZoneColumn.ActualWidth / ModalImageArea.ActualWidth) - 0.28) < 0.01
            && Math.Abs((ModalNextZoneColumn.ActualWidth / ModalImageArea.ActualWidth) - 0.28) < 0.01;
    public bool CloseModalFromBackdropForSmoke() => TryCloseModalFromBackdrop(ModalImageArea);
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
    public string? ModalEnhancementJobIdForSmoke => _modalEnhancementJobId;
    public string? ModalEnhancementStatusForSmoke => _modalEnhancementJobStatus;
    public int ModalEnhancementProgressForSmoke => _modalEnhancementProgress;
    public string ModalEnhancementMessageForSmoke => ModalEnhancementStatusText.Text;
    public bool ModalEnhancementCancelVisibleForSmoke => ModalEnhanceCancelButton.Visibility == Visibility.Visible;
    public bool ModalEnhancedDeleteVisibleForSmoke => ModalEnhancedDeleteButton.Visibility == Visibility.Visible;

    public void ConfigureModalEnhancementForSmoke(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sender,
        bool confirmLargeJob = true,
        bool confirmOutputDelete = true)
    {
        _modalEnhancementSender = sender;
        _confirmLargeEnhancementForSmoke = () => confirmLargeJob;
        _confirmEnhancedOutputDeleteForSmoke = () => confirmOutputDelete;
    }

    public async Task<bool> RefreshModalEnhancementForSmokeAsync()
    {
        if (Modal.Visibility != Visibility.Visible || SelectedTile() is not Tile tile)
            return false;
        await RefreshModalEnhancementStateAsync(tile.Path, _modalEnhancementGeneration, showUnavailableError: true);
        return true;
    }

    public async Task<bool> StartModalEnhancementForSmokeAsync()
    {
        StartModalEnhancement_Click(this, new RoutedEventArgs());
        await WaitForModalEnhancementRequestForSmokeAsync();
        return _modalEnhancementJobStatus is "queued" or "running";
    }

    public void BeginModalEnhancementForSmoke()
        => StartModalEnhancement_Click(this, new RoutedEventArgs());

    public Task WaitForModalEnhancementRequestCompletionForSmokeAsync()
        => WaitForModalEnhancementRequestForSmokeAsync();

    public async Task<bool> CancelModalEnhancementForSmokeAsync()
    {
        CancelModalEnhancement_Click(this, new RoutedEventArgs());
        await WaitForModalEnhancementRequestForSmokeAsync();
        return _modalEnhancementJobStatus == "canceled";
    }

    public async Task<bool> DeleteModalEnhancedOutputForSmokeAsync()
    {
        DeleteModalEnhancedOutput_Click(this, new RoutedEventArgs());
        await WaitForModalEnhancementRequestForSmokeAsync();
        return SelectedTile() is Tile tile && !tile.Enhanced && !_modalShowingEnhanced;
    }

    private async Task WaitForModalEnhancementRequestForSmokeAsync()
    {
        for (int attempt = 0; attempt < 300 && _modalEnhancementRequestPending; attempt++)
            await Task.Delay(10);
    }

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
            tile?.CardThumbnailStretch.ToString() ?? Stretch.Uniform.ToString(),
            _tiles.Count);
    }

    public int AppendPastedFoldersForSmoke(string folderText)
        => AppendLandingFolders(SplitFolderSet(folderText));

    public Task<FolderDropSmokeSnapshot> DropFoldersForSmokeAsync(IEnumerable<string> folders, bool landing)
        => ApplyDroppedFoldersAsync(ReadDroppedFolders(folders), landing);

    public void SetFolderDropAffordanceForSmoke(bool landing, bool visible)
        => SetFolderDropAffordance(landing ? Landing : ViewerFolderDropTarget, visible);

    public bool FolderDropSurfaceContractForSmoke
        => !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetHelpText(Landing))
            && !string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetHelpText(ViewerFolderDropTarget));

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
    public bool FavoriteWriterAdoptedForSmoke => _favoriteWriterAdopted;
    public bool SeenWriterAdoptedForSmoke => _seenWriterAdopted;
    public bool FavoriteWriterPendingForSmoke => _favoriteWriter?.HasPendingOrInFlight == true;
    public bool SeenWriterPendingForSmoke => _seenWriter?.HasPendingOrInFlight == true;
    public int FavoriteWriterBatchCountForSmoke => _favoriteWriter?.BatchWriteCount ?? 0;
    public int SeenWriterBatchCountForSmoke => _seenWriter?.BatchWriteCount ?? 0;
    public int PendingFavoriteMutationCountForSmoke => _pendingFavoriteMutations.Count;
    public int PendingSeenMutationCountForSmoke => _pendingSeenMutations.Count;
    public bool FavoritesWriteBlockedForSmoke => _favoritesWriteBlocked;
    public bool SeenWriteBlockedForSmoke => _seenWriteBlocked;
    public bool FailedFavoriteRetryPendingForSmoke => _failedFavoriteBatch is { Count: > 0 };
    public bool FailedSeenRetryPendingForSmoke => _failedSeenBatch is { Count: > 0 };
    public bool SharedReloadBarrierActiveForSmoke => _sharedReloadBarrierDepth > 0;

    public void ForceSharedStoreWritersForSmoke() => _forceSharedWritersForSmoke = true;

    public void ConfigureFavoriteWriterGateForSmoke(ManualResetEventSlim? entered, ManualResetEventSlim? gate)
    {
        _favoriteWriterEnteredForSmoke = entered;
        _favoriteWriterGateForSmoke = gate;
    }

    public void ConfigureSeenWriterGateForSmoke(ManualResetEventSlim? entered, ManualResetEventSlim? gate)
    {
        _seenWriterEnteredForSmoke = entered;
        _seenWriterGateForSmoke = gate;
    }

    public void ConfigureReloadDrainStartedForSmoke(
        ManualResetEventSlim? favoriteStarted,
        ManualResetEventSlim? seenStarted)
    {
        _favoriteReloadDrainStartedForSmoke = favoriteStarted;
        _seenReloadDrainStartedForSmoke = seenStarted;
    }

    public void FailNextFavoriteWriterForSmoke() => Interlocked.Exchange(ref _failNextFavoriteWriterForSmoke, 1);
    public void FailNextSeenWriterForSmoke() => Interlocked.Exchange(ref _failNextSeenWriterForSmoke, 1);
    public void RetryFailedFavoriteForSmoke() => RetryFailedFavoriteBatch();
    public void RetryFailedSeenForSmoke() => RetryFailedSeenBatch();

    internal async Task<SharedWriteStatus[]> DrainSharedStoreWritersForSmokeAsync()
    {
        Task<SharedWriteStatus> favorite = _favoriteWriter is { } favoriteWriter
            ? favoriteWriter.DrainAsync(CancellationToken.None)
            : Task.FromResult(SharedWriteStatus.Succeeded);
        Task<SharedWriteStatus> seen = _seenWriter is { } seenWriter
            ? seenWriter.DrainAsync(CancellationToken.None)
            : Task.FromResult(SharedWriteStatus.Succeeded);
        return await Task.WhenAll(favorite, seen);
    }

    public Task CloseAndWaitForSmokeAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Closed += (_, _) => completion.TrySetResult();
        Close();
        return completion.Task;
    }

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
        VirtualizingWrapPanel? panel = FindVisualDescendant<VirtualizingWrapPanel>(CardsList);
        if (panel is null)
            return false;
        int before = panel.FirstVisibleIndex;
        panel.PageDown();
        UpdateLayout();
        return panel.FirstVisibleIndex > before;
    }

    public bool RealizePreviousGridBatchForSmoke()
    {
        VirtualizingWrapPanel? panel = FindVisualDescendant<VirtualizingWrapPanel>(CardsList);
        if (panel is null)
            return false;
        int before = panel.FirstVisibleIndex;
        panel.PageUp();
        UpdateLayout();
        return panel.FirstVisibleIndex >= 0 && panel.FirstVisibleIndex < before;
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
    public long LastCardsSelectionSyncMsForSmoke => _lastCardsSelectionSyncMs;
    public long LastRowsSelectionSyncMsForSmoke => _lastRowsSelectionSyncMs;
    public long LastEnsureGridSelectionMsForSmoke => _lastEnsureGridSelectionMs;
    public long LastCardsScrollSelectionMsForSmoke => _lastCardsScrollSelectionMs;
    public long LastRowsScrollSelectionMsForSmoke => _lastRowsScrollSelectionMs;
    public long LastPreviewSelectionMsForSmoke => _lastPreviewSelectionMs;
    public long LastSeenSelectionMsForSmoke => _lastSeenSelectionMs;

    public void ClearSelectionForSmoke() => SetSelection([], null);

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

    public async Task<PreviewDecodeSmokeSnapshot> WaitForCurrentPreviewDecodeForSmokeAsync(string expectedFileName)
    {
        Tile? tile = SelectedTile();
        if (tile is null
            || !tile.IsRealFile
            || !string.Equals(tile.FileName, expectedFileName, StringComparison.OrdinalIgnoreCase)
            || _previewDecodeCompletion is null)
        {
            return PreviewDecodeSmokeSnapshot.NotSelected(expectedFileName);
        }

        TaskCompletionSource<PreviewDecodeResult> completion = _previewDecodeCompletion;
        Task timeout = Task.Delay(TimeSpan.FromSeconds(5));
        if (await Task.WhenAny(completion.Task, timeout) != completion.Task)
            return new PreviewDecodeSmokeSnapshot(true, tile.Path, 0, _lastPreviewImmediateMs, 0, false, false, false, "preview decode timed out");

        PreviewDecodeResult decoded = await completion.Task;
        await Task.Delay(125);
        bool stable = decoded.Applied
            && string.Equals(SelectedTile()?.Path, tile.Path, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_previewDecodedPath, tile.Path, StringComparison.OrdinalIgnoreCase)
            && PreviewBitmap.Source is not null;
        return new PreviewDecodeSmokeSnapshot(
            true,
            tile.Path,
            0,
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

    public PromptTagSearchSmokeSnapshot SearchModalPromptTagWithKeyForSmoke(string tag, Key key)
    {
        Button? chip = ModalPromptChips.Children
            .OfType<Button>()
            .FirstOrDefault(candidate => string.Equals(candidate.Tag as string, tag, StringComparison.OrdinalIgnoreCase));
        PresentationSource? source = PresentationSource.FromVisual(this);
        bool applied = false;
        if (chip is not null && source is not null)
        {
            chip.Focus();
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            chip.RaiseEvent(args);
            applied = args.Handled;
        }

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
    public Dictionary<string, JsonElement>? KeyBindings { get; set; }
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
    string CardThumbnailStretch,
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
    public long CatalogPrepareMs { get; set; }
    public long CatalogPublishOtherMs { get; set; }
    public long FolderBucketViewMs { get; set; }
    public long InitialFilterMs { get; set; }
    public long CatalogStatsMs { get; set; }
    public long CatalogReadyMs { get; set; }
    public long MetadataMs { get; set; }
    public int MetadataWorkers { get; set; }
    public int MetadataCompleted { get; set; }
    public int MetadataCacheHits { get; set; }
    public int MetadataCacheMisses { get; set; }
    public long MetadataIndexReadMs { get; set; }
    public long MetadataIndexWriteMs { get; set; }
    public string MetadataIndexLoadState { get; set; } = "";
    public bool MetadataIndexSaveSucceeded { get; set; }
    public bool MetadataIndexWritten { get; set; }
    public string? MetadataIndexSaveError { get; set; }
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

public sealed record ScanBoundarySmokeSnapshot(
    IReadOnlyList<string> Images,
    IReadOnlyList<string> AccessFailures,
    IReadOnlyList<string> BoundarySkips,
    long ElapsedMs);

public sealed record GridSelectionVisualSmokeSnapshot(
    string? CanonicalPath,
    bool CanonicalSelected,
    bool GridWindowContains,
    bool SelectedItemsContains,
    bool ContainerRealized,
    bool ContainerSelected);

public sealed record ListSelectionVisualSmokeSnapshot(
    string? CanonicalPath,
    bool CanonicalSelected,
    bool SelectedItemsContains,
    bool ContainerRealized,
    bool ContainerSelected);

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

public sealed record DiagnosticsSmokeSnapshot(
    bool Copied,
    string CopyText,
    string Status,
    bool SurfaceContract,
    bool SettingsFocused);

public sealed record ExternalOpenSmokeSnapshot(
    bool Launched,
    bool LauncherInvoked,
    string FileName,
    bool UseShellExecute,
    string Status,
    bool RetryVisible,
    bool Focused,
    bool SelectionStable,
    string? SelectedPath,
    bool ModalVisible,
    bool AutomationReady);

public sealed record ExplorerRevealSmokeSnapshot(
    bool Launched,
    string FileName,
    List<string> Arguments,
    string ArgumentsText,
    bool UseShellExecute,
    bool AutomationReady,
    bool Focused,
    string Status,
    string Surface);

public sealed record ExplorerRevealValidationSnapshot(bool Accepted, string CanonicalPath, string Reason);

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

public sealed record FolderDropSmokeSnapshot(
    bool Accepted,
    int AddedCount,
    int RejectedCount,
    string RejectionReason,
    bool Landing,
    List<string> LandingFolders,
    List<string> CurrentFolders,
    string Status);

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
    int DecodeFailures,
    int CacheHits = 0,
    int CacheMisses = 0,
    long IndexReadMs = 0,
    long IndexWriteMs = 0,
    string IndexLoadState = "Missing",
    bool IndexSaveSucceeded = false,
    bool IndexWritten = false,
    string? IndexSaveError = null)
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

internal readonly record struct BitmapDecodePlan(int PixelWidth, int PixelHeight);

internal readonly record struct GridZoomAnchor(string Path, double ViewportY, double CenterDistance);

internal readonly record struct DecodedThumbnail(Tile Tile, BitmapSource? Thumbnail);

internal readonly record struct DecodedImageMetadata(
    Tile Tile,
    ImageDimensions Dimensions,
    string Prompt);

internal sealed record MetadataIndexSnapshotPlan(
    MetadataIndexEntry[] Entries,
    bool DurableEntrySetExact);

internal sealed class ResettableObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        CheckReentrancy();
        Items.Clear();
        if (Items is List<T> list)
            list.EnsureCapacity(items.Count);
        for (int index = 0; index < items.Count; index++)
            Items.Add(items[index]);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

// ─────────── Tile view model ───────────
public sealed class Tile : INotifyPropertyChanged
{
    public Brush? ArtBase { get; set; }
    public Brush? ArtGlow { get; set; }
    public string FileName { get; set; } = "";
    public string Group { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsRealFile { get; set; }
    public string FolderBucketKey { get; set; } = "";
    public string FolderBucketLabel { get; set; } = "";
    public bool Enhanced { get; set; }
    public string? EnhancedOutputPath { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public long SourceLength { get; set; }
    public long SourceLastWriteUtcTicks { get; set; }
    public long SourceCreationUtcTicks { get; set; }
    public string SizeText { get; set; } = "";
    public string ModifiedText { get; set; } = "";

    private string _prompt = "";
    private int _imagePixelWidth;
    private int _imagePixelHeight;

    public string Prompt
    {
        get => _prompt;
        set
        {
            value ??= "";
            if (string.Equals(_prompt, value, StringComparison.Ordinal)) return;
            _prompt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Prompt)));
        }
    }

    public int ImagePixelWidth
    {
        get => _imagePixelWidth;
        set
        {
            if (_imagePixelWidth == value) return;
            _imagePixelWidth = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImagePixelWidth)));
        }
    }

    public int ImagePixelHeight
    {
        get => _imagePixelHeight;
        set
        {
            if (_imagePixelHeight == value) return;
            _imagePixelHeight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImagePixelHeight)));
        }
    }

    private Stretch _cardThumbnailStretch = Stretch.Uniform;
    public Stretch CardThumbnailStretch
    {
        get => _cardThumbnailStretch;
        set
        {
            if (_cardThumbnailStretch == value) return;
            _cardThumbnailStretch = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CardThumbnailStretch)));
        }
    }

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
