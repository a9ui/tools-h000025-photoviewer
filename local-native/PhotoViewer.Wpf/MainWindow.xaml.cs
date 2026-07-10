using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
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
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff",
    };

    private const int MaxLoadedImages = 1200;
    private const int MinParallelThumbnailCount = 32;
    private const int MaxThumbnailDecodeWorkers = 12;
    private const int MaxMetadataReadWorkers = 4;
    private const int SearchStateSaveDebounceMilliseconds = 300;
    private const int InitialGridRealizationCount = 96;
    private const int GridRealizationBatchSize = 96;
    private const int MaxGridRealizationCount = 384;
    private const int MaxRecentFolderSets = 8;
    private const double DefaultCardWidth = 190;
    private const double CardWidthStep = 15;
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
    private const string DatePresetNoneValue = "none";
    private const string DatePresetTodayValue = "today";
    private const string DatePreset7DaysValue = "7d";
    private const string DatePreset30DaysValue = "30d";
    private const string DatePresetThisYearValue = "this-year";
    private const string DatePresetManualValue = "manual";
    private const int MinFavoriteFilterLevel = 1;
    private const int MaxFavoriteFilterLevel = 5;
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
    private readonly Dictionary<string, int> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _hiddenFolderBuckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _enhancedOutputs = new(StringComparer.OrdinalIgnoreCase);
    private int _gridStartIndex;
    private int _lastInitialUnseenCount;
    private int _enhancementJobsRead;
    private int _enhancedCandidateCount;
    private bool _enhancementReadOk = true;
    private string? _enhancementReadError;
    private Rect _restoreBounds;
    private bool _fakeMaximized;
    private bool _initializing = true;
    private bool _suppressStateSave;
    private bool _favoritesWriteBlocked;
    private bool _seenWriteBlocked;
    private bool _syncingSelection;
    private bool _syncingFavoriteFilterControls;
    private bool _syncingDateControls;
    private bool _settingSearchQuery;
    private string? _currentFolder;
    private List<string> _currentFolderSet = [];
    private List<string> _lastFolderSet = [];
    private string? _restoredSelectedPath;
    private string? _activePreviewTabPath;
    private string? _hoverPreviewTabPath;
    private string? _modalTransformPath;
    private Point? _modalPanStartPoint;
    private Vector _modalPanStartOffset;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _modalCts;
    private CancellationTokenSource? _previewCts;
    private TaskCompletionSource<PreviewDecodeResult>? _previewDecodeCompletion;
    private int _previewUpdateCount;
    private long _previewMs;
    private int _previewDeferredDecodeCount;
    private long _previewDeferredDecodeMs;
    private long _lastPreviewImmediateMs;
    private string? _previewDecodedPath;
    private readonly DispatcherTimer _searchStateSaveTimer;
    private string _displayStyle = DisplayStyleStandard;
    private string _aspectMode = AspectOriginalValue;
    private string _sortBy = SortModifiedNewestValue;
    private string _randomSortSeed = "default";
    private string _datePreset = DatePresetNoneValue;
    private DateTime? _dateFromLocal;
    private DateTime? _dateToLocal;
    private int _favoriteFilterLevel = MinFavoriteFilterLevel;
    private double _modalZoom = 1;
    private bool _modalFlipped;
    private double _modalPanX;
    private double _modalPanY;
    public LoadMetrics? LastLoadMetrics { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        _searchStateSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(SearchStateSaveDebounceMilliseconds),
        };
        _searchStateSaveTimer.Tick += SearchStateSaveTimer_Tick;
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
        CardsList.MouseDoubleClick += (_, _) => OpenModal();
        RowsList.MouseDoubleClick += (_, _) => OpenModal();
        RefreshLandingFolderSetUi();
        RefreshPreviewTabs();
        SetPhase(landing: true);
        _initializing = false;
    }

    private static System.ComponentModel.ICollectionView BuildGroupedView(ObservableCollection<Tile> source)
    {
        var cvs = new CollectionViewSource { Source = source };
        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Tile.Group)));
        return cvs.View;
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
        var scanWatch = Stopwatch.StartNew();
        try
        {
            files = await Task.Run(
                () => existingFolderSet
                    .SelectMany(EnumerateImageFiles)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(MaxLoadedImages)
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
        }

        var materializeWatch = Stopwatch.StartNew();
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
                _allTiles.Add(MakeFileTile(file, width, metadata.Dimensions));
            _lastInitialUnseenCount = _allTiles.Count(static tile => tile.Unseen);
            PruneHiddenFolderBucketsToCurrentSet();
            RefreshFolderBucketViews();

            FolderPathText.Text = resolvedFolderSummary;
            ApplyFilters(selectFirst: false);
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
                };
            })
            .OrderByDescending(static bucket => bucket.Count)
            .ThenBy(static bucket => bucket.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _folderBucketViews.Clear();
        foreach (var bucket in buckets)
            _folderBucketViews.Add(bucket);

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

    private void RefreshRecentFolderSetViews()
    {
        if (RecentFolderSetList is null)
            return;

        var read = ReadSharedRecentFolders();
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
        var snapshot = _allTiles.Where(static tile => tile.IsRealFile).ToList();
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

    private static IEnumerable<string> EnumerateImageFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var folder = pending.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder);
            }
            catch
            {
                files = [];
            }

            foreach (var file in files)
            {
                if (SupportedImageExtensions.Contains(Path.GetExtension(file)))
                    yield return file;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(folder);
            }
            catch
            {
                children = [];
            }

            foreach (var child in children)
                pending.Push(child);
        }
    }

    private Tile MakeFileTile(FileInfo file, double width, IReadOnlyDictionary<string, ImageDimensions> dimensions)
    {
        var modified = file.LastWriteTime;
        int paletteIndex = file.FullName.GetHashCode(StringComparison.OrdinalIgnoreCase) & int.MaxValue;
        bool enhanced = TryGetEnhancedOutputForPath(file.FullName, out string? enhancedOutputPath);
        dimensions.TryGetValue(file.FullName, out var imageSize);
        var folderBucket = ResolveFolderBucket(file.FullName);
        var tile = new Tile
        {
            ArtBase = MakeBaseBrush(paletteIndex),
            ArtGlow = MakeGlowBrush(paletteIndex),
            FileName = file.Name,
            Fav = FavoriteLevelForPath(file.FullName),
            Unseen = !SeenStateContains(file.FullName),
            Group = FormatGroup(modified),
            CardWidth = width,
            ModifiedUtc = file.LastWriteTimeUtc,
            CreatedUtc = file.CreationTimeUtc,
            Prompt = file.FullName,
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

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                _favoritesWriteBlocked = true;
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (TryReadFavoriteLevel(property.Value, out int level))
                    _favorites[NormalizeFavoritePath(property.Name)] = level;
            }
        }
        catch
        {
            _favorites.Clear();
            _favoritesWriteBlocked = true;
        }
    }

    private static bool TryReadFavoriteLevel(JsonElement value, out int level)
    {
        level = 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric))
            level = numeric;
        else if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out int parsed))
            level = parsed;

        level = Math.Clamp(level, 0, 5);
        return level > 0;
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

    private int FavoriteLevelForPath(string path)
        => _favorites.TryGetValue(NormalizeFavoritePath(path), out int level) ? Math.Clamp(level, 0, 5) : 0;

    private static string ResolvedEnhancementJobsPath => ProjectCachePath(Path.Combine("enhance", "jobs.json"));

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

    private bool SaveFavorites()
    {
        if (_favoritesWriteBlocked)
            return false;

        try
        {
            string path = ResolvedFavoritesPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var ordered = _favorites
                .Where(static item => item.Value > 0)
                .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    static item => item.Key,
                    static item => Math.Clamp(item.Value, 1, 5),
                    StringComparer.OrdinalIgnoreCase);
            var json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            return true;
        }
        catch
        {
            return false;
        }
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
        _seenWriteBlocked = false;

        if (!string.IsNullOrWhiteSpace(SeenPathOverride))
        {
            string overridePath = ResolvedSeenPath;
            _seenWriteBlocked = !TryLoadSeenFile(overridePath, _seenPaths);
            return;
        }

        bool sharedOk = TryLoadSeenFile(ResolvedSeenPath, _seenPaths);
        bool legacyOk = TryLoadSeenFile(LegacySeenPath, _seenPaths);
        _seenWriteBlocked = !sharedOk || !legacyOk;
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
                    continue;

                bool seen = property.Value.ValueKind == JsonValueKind.True
                    || (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out int numeric) && numeric != 0)
                    || (property.Value.ValueKind == JsonValueKind.String && bool.TryParse(property.Value.GetString(), out bool parsed) && parsed);
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

    private bool SeenStateContains(string path)
        => _seenPaths.Contains(NormalizeFavoritePath(path));

    private bool SaveSeenState()
    {
        if (_seenWriteBlocked)
            return false;

        try
        {
            string path = ResolvedSeenPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var ordered = _seenPaths
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static item => item, static _ => true, StringComparer.OrdinalIgnoreCase);
            var json = JsonSerializer.Serialize(ordered, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool MarkTileSeen(Tile tile)
    {
        if (!tile.IsRealFile)
            return false;

        string key = NormalizeFavoritePath(tile.Path);
        bool wasUnseen = tile.Unseen;
        bool hadSeen = _seenPaths.Contains(key);

        if (hadSeen && !wasUnseen)
            return true;

        _seenPaths.Add(key);
        tile.Unseen = false;

        if (!SaveSeenState())
        {
            if (!hadSeen)
                _seenPaths.Remove(key);
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
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodePixelWidth > 0)
                image.DecodePixelWidth = decodePixelWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
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
        int workers = Math.Max(1, Math.Min(Math.Min(MaxMetadataReadWorkers, Environment.ProcessorCount), files.Count));
        int completed = 0;
        Parallel.ForEach(
            files,
            new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = workers },
            file =>
            {
                token.ThrowIfCancellationRequested();
                TryReadBitmapSize(file.FullName, out int width, out int height);
                dimensions[file.FullName] = new ImageDimensions(width, height);
                Interlocked.Increment(ref completed);
            });
        watch.Stop();
        return new ImageMetadataLoadMetrics(dimensions, workers, completed, watch.ElapsedMilliseconds);
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
        if (_syncingSelection || sender is not ListBox lb || lb.SelectedItem is not Tile t) return;
        try
        {
            _syncingSelection = true;
            if (lb == CardsList && RowsList.SelectedItem != t)
                RowsList.SelectedItem = t;
            if (lb == RowsList && CardsList.SelectedItem != t)
                CardsList.SelectedItem = t;
        }
        finally
        {
            _syncingSelection = false;
        }

        UpdatePreview(t);
        if (t.IsRealFile)
        {
            MarkTileSeen(t);
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
        PreviewDateText.Text = t.ModifiedText;
        PreviewPromptText.Text = hasRealFile ? t.Path : (string.IsNullOrWhiteSpace(t.Prompt) ? t.Path : t.Prompt);
        FavoriteLevelText.Text = t.Fav.ToString();
        UpdateHeaderStats();
        ModalTitle.Text = $"{t.FileName} - {PreviewSizeText.Text}";
        watch.Stop();
        _previewUpdateCount++;
        _previewMs += watch.ElapsedMilliseconds;
        _lastPreviewImmediateMs = watch.ElapsedMilliseconds;

        if (hasRealFile)
            _ = LoadPreviewBitmapAsync(t.Path, cts.Token, completion);
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

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing || _settingSearchQuery) return;
        ApplyFilters();
        ScheduleSearchStateSave();
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
        PruneHiddenFolderBucketsToCurrentSet();
        RefreshFolderBucketViews();
        ApplyFilters();
        SaveState();
    }

    private static int NormalizeFavoriteFilterLevel(int level)
        => Math.Clamp(level, MinFavoriteFilterLevel, MaxFavoriteFilterLevel);

    private bool SetFavoriteFilterLevel(int level)
    {
        int normalized = NormalizeFavoriteFilterLevel(level);
        bool changed = _favoriteFilterLevel != normalized;
        _favoriteFilterLevel = normalized;
        SyncFavoriteFilterControls();

        if (changed && !_initializing)
        {
            ApplyFilters();
            SaveState();
        }

        return changed;
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
            FavoriteLevel1Filter.IsChecked = _favoriteFilterLevel == 1;
            FavoriteLevel2Filter.IsChecked = _favoriteFilterLevel == 2;
            FavoriteLevel3Filter.IsChecked = _favoriteFilterLevel == 3;
            FavoriteLevel4Filter.IsChecked = _favoriteFilterLevel == 4;
            FavoriteLevel5Filter.IsChecked = _favoriteFilterLevel == 5;
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
            ? $"Favorites Lv {_favoriteFilterLevel}+"
            : unfavoriteOnly
                ? "Unrated only"
                : "All ratings";
    }

    private void FavoriteLevel1_Checked(object sender, RoutedEventArgs e) => FavoriteLevel_Checked(1);
    private void FavoriteLevel2_Checked(object sender, RoutedEventArgs e) => FavoriteLevel_Checked(2);
    private void FavoriteLevel3_Checked(object sender, RoutedEventArgs e) => FavoriteLevel_Checked(3);
    private void FavoriteLevel4_Checked(object sender, RoutedEventArgs e) => FavoriteLevel_Checked(4);
    private void FavoriteLevel5_Checked(object sender, RoutedEventArgs e) => FavoriteLevel_Checked(5);

    private void FavoriteLevel_Checked(int level)
    {
        if (_initializing || _syncingFavoriteFilterControls) return;
        SetFavoriteFilterLevel(level);
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

    private void ToggleSelectedFavorite_Click(object sender, RoutedEventArgs e)
    {
        ToggleSelectedFavorite();
    }

    private bool ToggleSelectedFavorite()
    {
        if (SelectedTile() is not { IsRealFile: true } tile)
            return false;

        return SetFavoriteLevel(tile, tile.Fav > 0 ? 0 : 5);
    }

    private bool AdjustSelectedFavorite(int delta)
    {
        if (SelectedTile() is not { IsRealFile: true } tile)
            return false;

        int next = Math.Clamp(tile.Fav + delta, 0, 5);
        return SetFavoriteLevel(tile, next);
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

        if (!SaveFavorites())
        {
            if (hadStoredLevel)
                _favorites[key] = previousStoredLevel;
            else
                _favorites.Remove(key);
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
            ShowPreviewTabHover(tab, target);
    }

    private void PreviewTab_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab })
            HidePreviewTabHover(tab.Path);
    }

    private void ClosePreviewTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PreviewTabView tab })
            ClosePreviewTab(tab.Path);
    }

    private void RestorePreviewTab_Click(object sender, RoutedEventArgs e) => RestoreLastClosedPreviewTab();

    private void CloseAllPreviewTabs_Click(object sender, RoutedEventArgs e) => CloseAllPreviewTabs();

    private bool OpenPreviewTab(Tile tile, bool makeActive)
    {
        if (!tile.IsRealFile)
            return false;

        if (_previewTabs.All(tab => !string.Equals(tab.Path, tile.Path, StringComparison.OrdinalIgnoreCase)))
            _previewTabs.Add(new PreviewTabView(tile.Path, tile.FileName));

        _closedPreviewTabs.RemoveAll(closed => string.Equals(closed.Path, tile.Path, StringComparison.OrdinalIgnoreCase));

        if (makeActive)
            return ActivatePreviewTab(tile.Path);

        RefreshPreviewTabs();
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
                RefreshPreviewTabs();
        }
        else
        {
            RefreshPreviewTabs();
        }

        return true;
    }

    private bool RestoreLastClosedPreviewTab()
    {
        while (_closedPreviewTabs.Count > 0)
        {
            var tile = _closedPreviewTabs[0];
            _closedPreviewTabs.RemoveAt(0);
            if (_tiles.Contains(tile))
                return OpenPreviewTab(tile, makeActive: true);
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
    }

    private void RefreshPreviewTabs()
    {
        foreach (var tab in _previewTabs)
            tab.IsActive = string.Equals(tab.Path, _activePreviewTabPath, StringComparison.OrdinalIgnoreCase);

        if (PreviewTabsEmptyText is not null)
            PreviewTabsEmptyText.Visibility = _previewTabs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (CloseAllPreviewTabsButton is not null)
            CloseAllPreviewTabsButton.IsEnabled = _previewTabs.Count > 0;
        if (RestorePreviewTabButton is not null)
            RestorePreviewTabButton.IsEnabled = _closedPreviewTabs.Count > 0;
    }

    private bool ShowPreviewTabHover(PreviewTabView tab, FrameworkElement? placementTarget)
    {
        var tile = _allTiles.FirstOrDefault(candidate => string.Equals(candidate.Path, tab.Path, StringComparison.OrdinalIgnoreCase));
        if (tile is null || PreviewTabHoverPopup is null)
            return false;

        _hoverPreviewTabPath = tile.Path;
        PreviewTabHoverName.Text = tile.FileName;
        PreviewTabHoverPath.Text = tile.Path;
        PreviewTabHoverBitmap.Source = tile.Thumbnail ?? LoadBitmap(tile.Path, 360);
        if (placementTarget is not null)
            PreviewTabHoverPopup.PlacementTarget = placementTarget;
        PreviewTabHoverPopup.IsOpen = true;
        return true;
    }

    private bool HidePreviewTabHover(string? path = null)
    {
        if (!string.IsNullOrWhiteSpace(path)
            && !string.Equals(_hoverPreviewTabPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool wasVisible = PreviewTabHoverPopup?.IsOpen == true || _hoverPreviewTabPath is not null;
        _hoverPreviewTabPath = null;
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

    private void QuickSearch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;

        var token = button.Tag?.ToString() ?? button.Content?.ToString() ?? "";
        SearchInput.Text = string.Equals(token, "clear", StringComparison.OrdinalIgnoreCase) ? "" : token;
        SearchInput.Focus();
        SearchInput.CaretIndex = SearchInput.Text.Length;
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

    public void SuppressStatePersistence()
    {
        _suppressStateSave = true;
    }

    private void ApplyFilters(bool selectFirst = true)
    {
        if (CardsList is null || RowsList is null) return;

        var previous = SelectedTile();
        string query = SearchInput?.Text?.Trim() ?? "";
        bool favoritesOnly = FavoriteOnlyFilter?.IsChecked == true;
        bool unfavoriteOnly = UnfavoriteOnlyFilter?.IsChecked == true;
        bool enhancedOnly = EnhancedOnlyFilter?.IsChecked == true;
        bool unseenOnly = UnseenOnlyFilter?.IsChecked == true;

        var filtered = SortTiles(_allTiles
            .Where(tile => MatchesSearch(tile, query))
            .Where(tile => MatchesFavoriteFilter(tile, favoritesOnly, unfavoriteOnly))
            .Where(tile => !enhancedOnly || tile.Enhanced)
            .Where(tile => !unseenOnly || tile.Unseen)
            .Where(MatchesFolderBucketFilter)
            .Where(MatchesDateFilter))
            .ToList();

        _tiles.Clear();
        foreach (var tile in filtered)
            _tiles.Add(tile);

        Tile? preferred = previous is not null && filtered.Contains(previous)
            ? previous
            : (selectFirst && _tiles.Count > 0 ? _tiles[0] : null);
        RebuildGridTiles(preferred);
        UpdateFolderStats();

        if (preferred is not null)
            SelectTile(preferred);
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

        if (LastLoadMetrics is not null)
            UpdateGridMetrics(LastLoadMetrics);
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

        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (ContainsText(tile.FileName, token)
                || ContainsText(tile.Path, token)
                || ContainsText(tile.Prompt, token)
                || ContainsText(tile.Group, token)
                || ContainsText(tile.SizeText, token)
                || ContainsText(tile.ModifiedText, token))
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
            return tile.Fav >= _favoriteFilterLevel;
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

        DateTime modifiedDate = tile.ModifiedUtc.ToLocalTime().Date;
        if (_dateFromLocal.HasValue && modifiedDate < _dateFromLocal.Value.Date)
            return false;

        if (_dateToLocal.HasValue && modifiedDate > _dateToLocal.Value.Date)
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
        if (tile is not null)
            EnsureGridTileRealized(tile);

        CardsList.SelectedItem = tile;
        RowsList.SelectedItem = tile;
        if (tile is not null)
        {
            CardsList.ScrollIntoView(tile);
            RowsList.ScrollIntoView(tile);
        }
        if (tile is null)
            ClearPreview();
    }

    private void ClearPreview()
    {
        _previewCts?.Cancel();
        _previewDecodeCompletion?.TrySetResult(PreviewDecodeResult.Canceled);
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
        ModalTitle.Text = "No selection";
        UpdateHeaderStats();
    }

    private void UpdateFolderStats()
    {
        if (FolderCountText is null) return;

        int total = _allTiles.Count;
        int visible = _tiles.Count;
        string loaded = total == MaxLoadedImages ? $"{total:N0}+ images loaded" : $"{total:N0} images loaded";
        FolderCountText.Text = visible == total ? loaded : $"{visible:N0} shown / {loaded}";
        UpdateHeaderStats();
    }

    private void UpdateHeaderStats()
    {
        if (HeaderStats is null) return;

        int selected = SelectedTile() is null ? 0 : 1;
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
        ApplyCardLayoutToAllTiles();
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
        SizeSlider.Value = Math.Clamp(value, SizeSlider.Minimum, SizeSlider.Maximum);
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
            DatePresetTodayValue => DatePresetTodayValue,
            DatePreset7DaysValue or "last7" or "last-7" => DatePreset7DaysValue,
            DatePreset30DaysValue or "last30" or "last-30" => DatePreset30DaysValue,
            DatePresetThisYearValue or "year" => DatePresetThisYearValue,
            DatePresetManualValue or "range" => DatePresetManualValue,
            "clear" or "" or null => DatePresetNoneValue,
            _ => DatePresetNoneValue,
        };
    }

    private static (DateTime? From, DateTime? To) DateRangeForPreset(string preset)
    {
        DateTime today = DateTime.Today;
        return NormalizeDatePreset(preset) switch
        {
            DatePresetTodayValue => (today, today),
            DatePreset7DaysValue => (today.AddDays(-6), today),
            DatePreset30DaysValue => (today.AddDays(-29), today),
            DatePresetThisYearValue => (new DateTime(today.Year, 1, 1), today),
            _ => (null, null),
        };
    }

    private static string? FormatStateDate(DateTime? date)
        => date?.ToString("yyyy-MM-dd");

    private static DateTime? ParseStateDate(string? value)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            return parsed.Date;

        return null;
    }

    private static string DatePresetLabel(string preset)
    {
        return NormalizeDatePreset(preset) switch
        {
            DatePresetTodayValue => "Today",
            DatePreset7DaysValue => "7d",
            DatePreset30DaysValue => "30d",
            DatePresetThisYearValue => "This year",
            DatePresetManualValue => "Manual",
            _ => "Clear",
        };
    }

    private bool SetDatePreset(string preset)
    {
        string previousPreset = _datePreset;
        string normalized = NormalizeDatePreset(preset);
        DateTime? previousFrom = _dateFromLocal;
        DateTime? previousTo = _dateToLocal;
        _datePreset = normalized;
        (_dateFromLocal, _dateToLocal) = DateRangeForPreset(normalized);
        bool changed = !string.Equals(previousPreset, normalized, StringComparison.Ordinal)
            || !SameDate(previousFrom, _dateFromLocal)
            || !SameDate(previousTo, _dateToLocal);
        SyncDateControls();
        UpdateDateFilterSummary();

        if (changed && !_initializing)
        {
            ApplyFilters();
            SaveState();
        }

        return changed;
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
        _datePreset = NormalizeDatePreset(state.DatePreset);
        if (string.Equals(_datePreset, DatePresetManualValue, StringComparison.Ordinal))
        {
            _dateFromLocal = ParseStateDate(state.DateFrom);
            _dateToLocal = ParseStateDate(state.DateTo);
            if (!_dateFromLocal.HasValue && !_dateToLocal.HasValue)
                _datePreset = DatePresetNoneValue;
        }
        else if (string.Equals(_datePreset, DatePresetNoneValue, StringComparison.Ordinal))
        {
            _dateFromLocal = null;
            _dateToLocal = null;
        }
        else
        {
            _dateFromLocal = ParseStateDate(state.DateFrom);
            _dateToLocal = ParseStateDate(state.DateTo);
            if (!_dateFromLocal.HasValue && !_dateToLocal.HasValue)
                (_dateFromLocal, _dateToLocal) = DateRangeForPreset(_datePreset);
        }

        SyncDateControls();
        UpdateDateFilterSummary();
    }

    private void SyncDateControls()
    {
        if (DatePresetTodayButton is null
            || DatePreset7DaysButton is null
            || DatePreset30DaysButton is null
            || DatePresetThisYearButton is null
            || DatePresetClearButton is null
            || DateFromInput is null
            || DateToInput is null)
        {
            return;
        }

        _syncingDateControls = true;
        try
        {
            DatePresetTodayButton.IsChecked = _datePreset == DatePresetTodayValue;
            DatePreset7DaysButton.IsChecked = _datePreset == DatePreset7DaysValue;
            DatePreset30DaysButton.IsChecked = _datePreset == DatePreset30DaysValue;
            DatePresetThisYearButton.IsChecked = _datePreset == DatePresetThisYearValue;
            DatePresetClearButton.IsChecked = _datePreset == DatePresetNoneValue;
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

        DateFilterSummary.Text = $"{DatePresetLabel(_datePreset)}: {FormatStateDate(_dateFromLocal) ?? "..."} to {FormatStateDate(_dateToLocal) ?? "..."}";
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
        RightPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        RightCol.Width = show ? new GridLength(340) : new GridLength(0);
        ToggleRight.Style = (Style)FindResource(show ? "IconButtonActive" : "IconButton");
    }

    // ─────────── Display mode (Grid / List) ───────────
    private void ModeGrid_Checked(object sender, RoutedEventArgs e)
    {
        if (CardsList is null || RowsList is null) return;
        CardsList.Visibility = Visibility.Visible;
        RowsList.Visibility = Visibility.Collapsed;
    }

    private void ModeList_Checked(object sender, RoutedEventArgs e)
    {
        if (CardsList is null || RowsList is null) return;
        if (RowsList.SelectedItem is null && CardsList.SelectedIndex >= 0)
            RowsList.SelectedIndex = CardsList.SelectedIndex;
        CardsList.Visibility = Visibility.Collapsed;
        RowsList.Visibility = Visibility.Visible;
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

    private void DatePresetToday_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing && !_syncingDateControls)
            SetDatePreset(DatePresetTodayValue);
    }

    private void DatePreset7Days_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing && !_syncingDateControls)
            SetDatePreset(DatePreset7DaysValue);
    }

    private void DatePreset30Days_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing && !_syncingDateControls)
            SetDatePreset(DatePreset30DaysValue);
    }

    private void DatePresetThisYear_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing && !_syncingDateControls)
            SetDatePreset(DatePresetThisYearValue);
    }

    private void DatePresetClear_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing && !_syncingDateControls)
            SetDatePreset(DatePresetNoneValue);
    }

    private void ManualDateRange_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _syncingDateControls)
            return;

        SetManualDateRange(DateFromInput.SelectedDate, DateToInput.SelectedDate);
    }

    private void Logo_Click(object sender, MouseButtonEventArgs e) => SetPhase(landing: true);

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
            case "settings": SetPhase(landing: false); SettingsOverlay.Visibility = Visibility.Visible; break;
            case "album": SetPhase(landing: false); AlbumOverlay.Visibility = Visibility.Visible; break;
            case "enhance": SetPhase(landing: false); EnhanceOverlay.Visibility = Visibility.Visible; break;
            case "confirm": SetPhase(landing: false); ConfirmOverlay.Visibility = Visibility.Visible; break;
            default: SetPhase(landing: false); break;
        }
    }

    // ─────────── Modal ───────────
    private void PreviewImage_Click(object sender, MouseButtonEventArgs e) => OpenModal();

    private void OpenModal()
    {
        if (SelectedTile() is not Tile t) return;
        if (!string.Equals(_modalTransformPath, t.Path, StringComparison.OrdinalIgnoreCase))
            ResetModalTransform(t.Path);
        var watch = Stopwatch.StartNew();
        _modalCts?.Cancel();
        var cts = new CancellationTokenSource();
        _modalCts = cts;

        var immediate = PreviewBitmap.Source as BitmapSource ?? t.Thumbnail;
        ModalBitmap.Source = immediate;
        ModalBitmap.Visibility = immediate is null ? Visibility.Collapsed : Visibility.Visible;
        ModalArtBase.Visibility = immediate is null ? Visibility.Visible : Visibility.Collapsed;
        ModalArtGlow.Visibility = immediate is null ? Visibility.Visible : Visibility.Collapsed;
        ModalArtBase.Fill = t.ArtBase;
        ModalArtGlow.Fill = t.ArtGlow;
        Modal.Visibility = Visibility.Visible;
        watch.Stop();

        if (LastLoadMetrics is not null)
        {
            LastLoadMetrics.ModalOpenMs = watch.ElapsedMilliseconds;
            LastLoadMetrics.ModalImmediateSource = immediate is not null;
            LastLoadMetrics.ModalDeferredDecode = t.IsRealFile;
        }

        if (t.IsRealFile)
            _ = LoadModalBitmapAsync(t.Path, cts.Token);
    }

    private async Task LoadModalBitmapAsync(string path, CancellationToken token)
    {
        BitmapSource? bitmap;
        try
        {
            bitmap = await Task.Run(() => LoadBitmap(path, 1400), token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || bitmap is null)
            return;

        try
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    if (token.IsCancellationRequested || Modal.Visibility != Visibility.Visible)
                        return;
                    if (SelectedTile()?.Path != path)
                        return;

                    ModalBitmap.Source = bitmap;
                    ModalBitmap.Visibility = Visibility.Visible;
                    ModalArtBase.Visibility = Visibility.Collapsed;
                    ModalArtGlow.Visibility = Visibility.Collapsed;
                },
                DispatcherPriority.Background,
                token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CloseModal_Click(object sender, RoutedEventArgs e)
    {
        _modalCts?.Cancel();
        Modal.Visibility = Visibility.Collapsed;
        ResetModalTransform();
    }

    private void ToggleModalFlip_Click(object sender, RoutedEventArgs e) => ToggleModalFlip();

    private void ResetModalTransform_Click(object sender, RoutedEventArgs e) => ResetModalTransform(_modalTransformPath);

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
        return true;
    }

    private bool ResetModalTransform(string? path = null)
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
            ModalZoomLabel.Text = $"{Math.Round(_modalZoom * 100):0}%";
    }

    private bool TryHandleModalTransformKey(KeyEventArgs e)
    {
        if (Modal.Visibility != Visibility.Visible)
            return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key switch
        {
            Key.H => ToggleModalFlip(),
            Key.Add or Key.OemPlus => AdjustModalZoom(ModalZoomKeyboardStep),
            Key.Subtract or Key.OemMinus => AdjustModalZoom(1 / ModalZoomKeyboardStep),
            Key.D0 or Key.NumPad0 => ResetModalTransform(_modalTransformPath),
            _ => false,
        };
    }

    private void ModalImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Modal.Visibility != Visibility.Visible || _modalZoom <= 1)
            return;

        _modalPanStartPoint = e.GetPosition(ModalImage);
        _modalPanStartOffset = new Vector(_modalPanX, _modalPanY);
        ModalImage.CaptureMouse();
        e.Handled = true;
    }

    private void ModalImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_modalPanStartPoint.HasValue || !ModalImage.IsMouseCaptured)
            return;

        Point current = e.GetPosition(ModalImage);
        Vector delta = current - _modalPanStartPoint.Value;
        SetModalPan(_modalPanStartOffset.X + delta.X, _modalPanStartOffset.Y + delta.Y);
        e.Handled = true;
    }

    private void ModalImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndModalPan();
        e.Handled = true;
    }

    private void ModalImage_LostMouseCapture(object sender, MouseEventArgs e) => EndModalPan();

    private void EndModalPan()
    {
        _modalPanStartPoint = null;
        if (ModalImage.IsMouseCaptured)
            ModalImage.ReleaseMouseCapture();
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
        if (currentIndex < 0)
            currentIndex = delta > 0 ? -1 : _tiles.Count;

        int nextIndex = Math.Clamp(currentIndex + delta, 0, _tiles.Count - 1);
        if (nextIndex == currentIndex)
            return false;

        SelectTile(_tiles[nextIndex]);
        SaveState();

        if (Modal.Visibility == Visibility.Visible)
            OpenModal();

        return true;
    }

    /// <summary>Used only by the --shot --modal smoke path to capture the modal state.</summary>
    public void ShowModalForShot()
    {
        if (CardsList.SelectedItem is null && CardsList.Items.Count > 0)
            CardsList.SelectedIndex = 0;
        OpenModal();
    }

    private Tile? SelectedTile() => CardsList.SelectedItem as Tile ?? RowsList.SelectedItem as Tile;

    private void OpenSelectedExternally_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTile() is not { IsRealFile: true } tile || !File.Exists(tile.Path))
            return;

        Process.Start(new ProcessStartInfo(tile.Path) { UseShellExecute = true });
    }

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
            return;
        }

        if (state.CardWidth >= SizeSlider.Minimum && state.CardWidth <= SizeSlider.Maximum)
            SizeSlider.Value = state.CardWidth;

        _displayStyle = NormalizeDisplayStyle(state.DisplayStyle);
        SyncDisplayStyleButtons();
        _aspectMode = NormalizeAspectMode(state.AspectMode);
        SyncAspectButtons();
        _sortBy = NormalizeSortBy(state.SortBy);
        _randomSortSeed = string.IsNullOrWhiteSpace(state.RandomSortSeed) ? "default" : state.RandomSortSeed;
        SyncSortButtons();
        RestoreDateFilter(state);
        _favoriteFilterLevel = NormalizeFavoriteFilterLevel(state.FavoriteFilterLevel <= 0
            ? MinFavoriteFilterLevel
            : state.FavoriteFilterLevel);
        SetFavoriteFilterState(state.ShowFavoritesOnly, !state.ShowFavoritesOnly && state.ShowUnfavoriteOnly, apply: false, persist: false);
        _hiddenFolderBuckets.Clear();
        foreach (string folder in NormalizeFolderSet(state.HiddenFolderBuckets ?? []))
            _hiddenFolderBuckets.Add(folder);

        if (!string.IsNullOrWhiteSpace(state.SearchQuery))
            SearchInput.Text = state.SearchQuery;

        _restoredSelectedPath = state.SelectedPath;
    }

    private static ViewerState? ReadState()
    {
        try
        {
            if (!File.Exists(ResolvedStatePath)) return null;
            return JsonSerializer.Deserialize<ViewerState>(File.ReadAllText(ResolvedStatePath));
        }
        catch
        {
            return null;
        }
    }

    private void SaveState()
    {
        if (_initializing || _suppressStateSave) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResolvedStatePath)!);
            var selectedPath = SelectedTile() is { IsRealFile: true } selected ? selected.Path : null;
            _restoredSelectedPath = selectedPath;
            var state = new ViewerState
            {
                LastFolder = _currentFolder,
                LastFolderSet = _currentFolderSet.Count > 0 ? _currentFolderSet : null,
                SearchQuery = SearchInput.Text,
                CardWidth = SizeSlider.Value,
                DisplayStyle = _displayStyle,
                AspectMode = _aspectMode,
                SortBy = _sortBy,
                RandomSortSeed = _randomSortSeed,
                DatePreset = _datePreset,
                DateFrom = FormatStateDate(_dateFromLocal),
                DateTo = FormatStateDate(_dateToLocal),
                ShowFavoritesOnly = FavoriteOnlyFilter?.IsChecked == true,
                ShowUnfavoriteOnly = UnfavoriteOnlyFilter?.IsChecked == true,
                FavoriteFilterLevel = _favoriteFilterLevel,
                HiddenFolderBuckets = _hiddenFolderBuckets.Count > 0 ? _hiddenFolderBuckets.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToList() : null,
                SelectedPath = selectedPath,
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ResolvedStatePath, json);
            if (_currentFolderSet.Count > 0)
                SaveSharedRecentFolderSet(_currentFolderSet);
        }
        catch
        {
            // State persistence should never block passive browsing.
        }
    }

    private static string ResolvedSharedRecentPath => ProjectCachePath("recent-folders.json");

    private static SharedRecentReadResult ReadSharedRecentFolders()
    {
        string path = ResolvedSharedRecentPath;
        if (!File.Exists(path))
            return new SharedRecentReadResult(true, NormalizeSharedRecentFolders(null), null);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
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
            if (element.TryGetProperty("lastFolderSet", out var lastFolderSetElement))
                lastFolderSet = NormalizeFolderSet(lastFolderSetElement);
            if (element.TryGetProperty("recentFolderSets", out var recentFolderSetsElement))
                recentFolderSets = NormalizeRecentFolderSets(recentFolderSetsElement);
            if (element.TryGetProperty("updatedAtUtc", out var updatedAtElement) &&
                updatedAtElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(updatedAtElement.GetString()))
                updatedAtUtc = updatedAtElement.GetString()!;
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

    private static bool SaveSharedRecentFolder(string folder)
        => SaveSharedRecentFolderSet([folder]);

    private static bool SaveSharedRecentFolderSet(IEnumerable<string> folders)
    {
        var folderSet = NormalizeFolderSet(folders);
        if (folderSet.Count == 0)
            return true;

        var current = ReadSharedRecentFolders();
        if (!current.Ok)
            return false;

        var recentFolderSets = NormalizeRecentFolderSets(
            new[] { folderSet }.Concat(current.Recent.RecentFolderSets));
        var next = new SharedRecentFoldersState
        {
            LastFolderSet = folderSet,
            RecentFolderSets = recentFolderSets,
            UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
        };

        try
        {
            string path = ResolvedSharedRecentPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(next, SharedRecentJsonOptions);
            File.WriteAllText(path, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ─────────── Overlays (settings / album / enhance / confirm) ───────────
    private void OpenSettings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Visible;
    private void CloseSettings_Click(object sender, RoutedEventArgs e) => SettingsOverlay.Visibility = Visibility.Collapsed;
    private void OpenAlbum_Click(object sender, RoutedEventArgs e) => AlbumOverlay.Visibility = Visibility.Visible;
    private void CloseAlbum_Click(object sender, RoutedEventArgs e) => AlbumOverlay.Visibility = Visibility.Collapsed;
    private void OpenEnhance_Click(object sender, RoutedEventArgs e) => EnhanceOverlay.Visibility = Visibility.Visible;
    private void CloseEnhance_Click(object sender, RoutedEventArgs e) => EnhanceOverlay.Visibility = Visibility.Collapsed;
    private void OpenConfirm_Click(object sender, RoutedEventArgs e) => ConfirmOverlay.Visibility = Visibility.Visible;
    private void CloseConfirm_Click(object sender, RoutedEventArgs e) => ConfirmOverlay.Visibility = Visibility.Collapsed;

    private bool CloseTopmostOverlay()
    {
        foreach (var o in new[] { ConfirmOverlay, AlbumOverlay, SettingsOverlay, EnhanceOverlay, Modal })
        {
            if (o.Visibility == Visibility.Visible)
            {
                o.Visibility = Visibility.Collapsed;
                return true;
            }
        }
        return false;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.T
            && (Keyboard.Modifiers & ModifierKeys.Shift) != 0
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Windows)) != 0
            && RestoreLastClosedPreviewTab())
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
            ToggleSelectedFavorite();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.X)
        {
            AdjustSelectedFavorite(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && CloseTopmostOverlay())
            e.Handled = true;
        base.OnPreviewKeyDown(e);
    }

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
    public string SearchQueryForSmoke => SearchInput.Text;
    public bool IsEditableTextInputFocusedForSmoke => Keyboard.FocusedElement is TextBoxBase;
    public string StatePathForSmoke => ResolvedStatePath;
    public string FavoritesPathForSmoke => ResolvedFavoritesPath;
    public string SeenPathForSmoke => ResolvedSeenPath;
    public string SharedRecentPathForSmoke => ResolvedSharedRecentPath;
    public string EnhancementJobsPathForSmoke => ResolvedEnhancementJobsPath;
    public string? CurrentFolderForSmoke => _currentFolder;
    public List<string> CurrentFolderSetForSmoke => _currentFolderSet.ToList();
    public List<string> LandingFolderSetForSmoke => _landingFolderSet.ToList();
    public int RecentFolderSetCountForSmoke => _recentFolderSetViews.Count;
    public string LastFolderSetDisplayForSmoke => LastFolderSetText.Text;
    public int FolderBucketCountForSmoke => _folderBucketViews.Count;
    public int HiddenFolderBucketCountForSmoke => _folderBucketViews.Count(static bucket => bucket.Hidden);
    public List<string> FolderBucketKeysForSmoke => _folderBucketViews.Select(static bucket => bucket.Key).ToList();
    public List<string> HiddenFolderBucketKeysForSmoke => _folderBucketViews.Where(static bucket => bucket.Hidden).Select(static bucket => bucket.Key).ToList();
    public bool ModalVisibleForSmoke => Modal.Visibility == Visibility.Visible;
    public int FilteredCountForSmoke => _tiles.Count;
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
    public bool PreviewTabHoverVisibleForSmoke => PreviewTabHoverPopup?.IsOpen == true;
    public string? HoverPreviewTabNameForSmoke => string.IsNullOrWhiteSpace(_hoverPreviewTabPath)
        ? null
        : _allTiles.FirstOrDefault(tile => string.Equals(tile.Path, _hoverPreviewTabPath, StringComparison.OrdinalIgnoreCase))?.FileName;
    public string? HoverPreviewTabPathForSmoke => _hoverPreviewTabPath;
    public bool SelectedEnhancedForSmoke => SelectedTile()?.Enhanced == true;
    public string? SelectedEnhancedOutputPathForSmoke => SelectedTile()?.EnhancedOutputPath;
    public int UnseenCountForSmoke => _allTiles.Count(static tile => tile.Unseen);
    public int LastInitialUnseenCountForSmoke => _lastInitialUnseenCount;
    public int GridRealizedCountForSmoke => _gridTiles.Count;
    public int GridDeferredCountForSmoke => Math.Max(0, _tiles.Count - _gridTiles.Count);
    public int GridWindowStartIndexForSmoke => _gridStartIndex;
    public int GridWindowEndIndexForSmoke => _gridStartIndex + _gridTiles.Count;
    public bool FocusSearchInputForSmoke() => SearchInput.Focus();
    public bool FocusCardsListForSmoke() => CardsList.Focus();

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
    public string DisplayStyleForSmoke => _displayStyle;
    public string AspectModeForSmoke => _aspectMode;
    public string SortByForSmoke => _sortBy;
    public string DatePresetForSmoke => _datePreset;
    public string? DateFromForSmoke => FormatStateDate(_dateFromLocal);
    public string? DateToForSmoke => FormatStateDate(_dateToLocal);
    public bool ShowFavoritesOnlyForSmoke => FavoriteOnlyFilter?.IsChecked == true;
    public bool ShowUnfavoriteOnlyForSmoke => UnfavoriteOnlyFilter?.IsChecked == true;
    public int FavoriteFilterLevelForSmoke => _favoriteFilterLevel;

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
    public bool ShowPreviewTabHoverForSmoke(string fileName)
    {
        var tab = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return tab is not null && ShowPreviewTabHover(tab, PreviewTabList);
    }

    public bool HidePreviewTabHoverForSmoke(string? fileName = null)
    {
        string? path = null;
        if (!string.IsNullOrWhiteSpace(fileName))
            path = _previewTabs.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Path;
        return HidePreviewTabHover(path);
    }

    public bool ToggleSelectedFavoriteForSmoke() => ToggleSelectedFavorite();
    public bool AdjustSelectedFavoriteForSmoke(int delta) => AdjustSelectedFavorite(delta);
    public bool MarkSelectedSeenForSmoke() => SelectedTile() is { IsRealFile: true } tile && MarkTileSeen(tile);
    public bool ZoomInForSmoke() => AdjustCardWidth(1);
    public bool ZoomOutForSmoke() => AdjustCardWidth(-1);
    public bool ZoomResetForSmoke() => ResetCardWidth();
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
    public bool SetDatePresetForSmoke(string preset) => SetDatePreset(preset);
    public bool SetManualDateRangeForSmoke(string? from, string? to) => SetManualDateRange(ParseStateDate(from), ParseStateDate(to));
    public bool SetFavoriteFilterLevelForSmoke(int level) => SetFavoriteFilterLevel(level);
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

    public bool ResetModalTransformForSmoke() => ResetModalTransform(_modalTransformPath);

    public bool SetModalPanForSmoke(double x, double y) => SetModalPan(x, y);

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

    public void SetLandingFolderSetForSmoke(IEnumerable<string> folders)
        => SetLandingFolderSet(folders);

    public bool SetSelectedFavoriteLevelForSmoke(int level)
    {
        return SelectedTile() is { IsRealFile: true } tile && SetFavoriteLevel(tile, level);
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

    public string? PathForFileNameForSmoke(string fileName)
        => _allTiles.FirstOrDefault(candidate => string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase))?.Path;

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
    public string? LastFolder { get; set; }
    public List<string>? LastFolderSet { get; set; }
    public string? SearchQuery { get; set; }
    public string? SelectedPath { get; set; }
    public double CardWidth { get; set; } = 190;
    public string? DisplayStyle { get; set; }
    public string? AspectMode { get; set; }
    public string? SortBy { get; set; }
    public string? RandomSortSeed { get; set; }
    public string? DatePreset { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public bool ShowFavoritesOnly { get; set; }
    public bool ShowUnfavoriteOnly { get; set; }
    public int FavoriteFilterLevel { get; set; } = 1;
    public List<string>? HiddenFolderBuckets { get; set; }
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

public sealed class FolderBucketView
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Path { get; init; } = "";
    public int Count { get; init; }
    public bool Hidden { get; init; }
    public string CountText => Count.ToString("N0");
    public string VisibilityText => Hidden ? "Hidden" : "Shown";
    public double Opacity => Hidden ? 0.48 : 1.0;
}

public sealed class PreviewTabView : INotifyPropertyChanged
{
    private bool _isActive;

    public PreviewTabView(string path, string fileName)
    {
        Path = path;
        FileName = fileName;
    }

    public string Path { get; }
    public string FileName { get; }
    public string ActiveMarker => IsActive ? "*" : "";
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

    public event PropertyChangedEventHandler? PropertyChanged;
}

public readonly record struct FolderBucketIdentity(string Key, string Label);

public sealed class SharedRecentFoldersState
{
    public int Version { get; set; } = 1;
    public List<string> LastFolderSet { get; set; } = [];
    public List<List<string>> RecentFolderSets { get; set; } = [];
    public string UpdatedAtUtc { get; set; } = "";
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

public readonly record struct ImageDimensions(int Width, int Height);

public readonly record struct ImageMetadataLoadMetrics(IReadOnlyDictionary<string, ImageDimensions> Dimensions, int Workers, int Completed, long ElapsedMs)
{
    public static ImageMetadataLoadMetrics Empty => new(new Dictionary<string, ImageDimensions>(StringComparer.OrdinalIgnoreCase), 0, 0, 0);
}

public readonly record struct ThumbnailLoadMetrics(int Total, int Workers, int Completed, long ElapsedMs);

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
