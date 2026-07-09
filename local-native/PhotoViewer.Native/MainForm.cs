using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace PhotoViewer.Native;

internal sealed class MainForm : Form
{
    private static readonly char[] PromptTagLeadingTrimChars = [' ', '\t', '\r', '\n', '(', '[', '{'];
    private static readonly char[] PromptTagTrailingTrimChars = [' ', '\t', '\r', '\n', ')', ']', '}'];
    private static readonly KeyBindingAction[] MainKeyBindingActions =
    [
        new("next", "Next image", "main"),
        new("previous", "Previous image", "main"),
        new("favoriteUp", "Favorite up", "main"),
        new("favoriteDown", "Favorite down", "main"),
        new("delete", "Delete", "main"),
        new("openFile", "Open file", "main"),
        new("openFolder", "Open folder", "main"),
        new("openDetail", "Open detail", "main"),
        new("toggleView", "Toggle grid/list", "main"),
        new("togglePreview", "Toggle preview", "main"),
        new("toggleDetails", "Toggle details", "main"),
        new("reshuffleSort", "Reshuffle sort", "main"),
    ];
    private static readonly KeyBindingAction[] DetailKeyBindingActions =
    [
        new("detailNext", "Detail next image", "detail"),
        new("detailPrevious", "Detail previous image", "detail"),
        new("detailZoomIn", "Detail zoom in", "detail"),
        new("detailZoomOut", "Detail zoom out", "detail"),
        new("detailReset", "Detail reset view", "detail"),
        new("detailFlip", "Detail flip", "detail"),
        new("detailOpenExternal", "Detail open external", "detail"),
        new("detailFavoriteUp", "Detail favorite up", "detail"),
        new("detailFavoriteDown", "Detail favorite down", "detail"),
    ];
    private static readonly KeyBindingAction[] EditableKeyBindingActions =
        MainKeyBindingActions.Concat(DetailKeyBindingActions).ToArray();
    private const int MaxGridImageListDimension = 256;
    private const int GalleryThumbnailMin = 64;
    private const int GalleryThumbnailMax = 192;
    private const int GalleryThumbnailStep = 16;
    private const int GalleryThumbnailDefault = 96;
    private const int GalleryThumbnailReset = GalleryThumbnailMax;
    private const int BulkOpenExternalTargetLimit = 10;

    private readonly TextBox _folderText = new();
    private readonly Button _browseButton = new();
    private readonly Button _scanButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _importButton = new();
    private readonly TextBox _searchText = new();
    private readonly Button _clearSearchButton = new();
    private readonly FlowLayoutPanel _searchChipPanel = new();
    private readonly ComboBox _searchSuggestion = new();
    private readonly Button _addSearchSuggestionButton = new();
    private readonly CheckBox _favoritesOnly = new();
    private readonly CheckBox _enhancedOnly = new();
    private readonly ComboBox _favoriteFilter = new();
    private readonly ComboBox _dateFilter = new();
    private readonly DateTimePicker _dateFromPicker = new();
    private readonly DateTimePicker _dateToPicker = new();
    private readonly ComboBox _viewMode = new();
    private readonly ComboBox _displayStyle = new();
    private readonly ComboBox _aspectMode = new();
    private readonly ComboBox _sortMode = new();
    private readonly ComboBox _folderSortMode = new();
    private readonly Button _reshuffleButton = new();
    private readonly NumericUpDown _thumbnailSize = new();
    private readonly CheckBox _previewVisible = new();
    private readonly CheckBox _detailsVisible = new();
    private readonly CheckedListBox _folderBuckets = new();
    private readonly Button _showAllFoldersButton = new();
    private readonly Button _hideAllFoldersButton = new();
    private readonly Button _invertFoldersButton = new();
    private readonly Button _showSelectedFoldersButton = new();
    private readonly Button _hideSelectedFoldersButton = new();
    private readonly Button _clearFolderSelectionButton = new();
    private readonly Label _folderBucketLabel = new();
    private readonly Button _previousButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _detailButton = new();
    private readonly Button _copyMetadataButton = new();
    private readonly NumericUpDown _favoriteLevel = new();
    private readonly Button _openFileButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _settingsButton = new();
    private readonly Button _addFolderButton = new();
    private readonly Button _removeFolderButton = new();
    private readonly Button _recentSetButton = new();
    private readonly Button _refreshButton = new();
    private readonly Label _stateLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ListView _list = new();
    private readonly PictureBox _preview = new();
    private readonly Label _selectionLabel = new();
    private readonly Label _previewLabel = new();
    private readonly TabControl _previewTabs = new();
    private readonly Button _pinPreviewTabButton = new();
    private readonly Button _restorePreviewTabButton = new();
    private readonly Button _closePreviewTabButton = new();
    private readonly ImageList _gridImages = new();
    private SplitContainer? _mainSplit;

    private readonly string _projectRoot;
    private readonly NativeImageStore _store;
    private readonly NativePreviewRingBuffer _previewRing = new(capacity: 5);
    private readonly NativeCacheScheduler _cacheScheduler = new();
    private readonly NativeFolderWatcher _folderWatcher = new();
    private Dictionary<string, int> _favorites;
    private readonly Dictionary<string, string> _dateSectionHeadersByPath = new(StringComparer.OrdinalIgnoreCase);
    private List<NativeImageRecord> _allImages = [];
    private List<NativeImageRecord> _visibleImages = [];
    private List<SearchTagSuggestion> _searchTagSuggestions = [];
    private Dictionary<string, Keys> _keyBindings = new(StringComparer.OrdinalIgnoreCase);
    private string _currentFolder = "";
    private List<string> _currentRoots = [];
    private CancellationTokenSource? _scanCancellation;
    private long _previewVersion;
    private string _lastMetadataCopyText = "";
    private bool _updatingFavoriteControl;
    private bool _updatingFolderBuckets;
    private bool _updatingFavoriteFilter;
    private bool _updatingDateFilter;
    private bool _updatingDateRange;
    private bool _updatingThumbnailSize;
    private bool _updatingDisplayStyle;
    private bool _updatingAspectMode;
    private bool _updatingSearchSuggestions;
    private bool _committingSearchText;
    private bool _updatingPreviewTabs;
    private bool _updatingFolderBucketKeyboardRange;
    private int _folderBucketRangeAnchorIndex = -1;
    private readonly List<PreviewTabState> _closedPreviewTabs = [];
    private string _hoverPreviewPath = "";
    private string _lastSavedSelectedPath = "";
    private int _lastSavedVisibleIndex = -1;
    private int _randomSortSeed = Environment.TickCount;
    private const int ClosedPreviewTabLimit = 20;

    public MainForm(string? initialFolder)
    {
        Text = "PhotoViewer Local Native";
        Width = 1280;
        Height = 920;
        MinimumSize = new Size(900, 560);
        KeyPreview = true;

        _projectRoot = NativeStateBridge.ResolveProjectRoot();
        _store = new NativeImageStore(_projectRoot);
        _store.Initialize();
        var report = _store.ImportProjectState();
        _favorites = _store.LoadFavorites();
        RefreshKeyBindings();

        BuildLayout();
        if (!string.IsNullOrWhiteSpace(initialFolder))
        {
            _folderText.Text = initialFolder;
        }
        else
        {
            _folderText.Text = _store.LoadRecentFolder() ?? "";
        }

        _searchText.Text = _store.GetSetting("search_text", "");
        _favoritesOnly.Checked = _store.GetSetting("favorites_only", "0") == "1";
        _enhancedOnly.Checked = _store.GetSetting("enhanced_only_filter", "0") == "1";
        RefreshFavoriteFilterOptions(_store.GetSetting("favorite_filter", "all"));
        var storedDateFilter = _store.GetSetting("date_filter", "all");
        RefreshDateFilterOptions(storedDateFilter);
        ApplyStoredDateRange(
            _store.GetSetting("date_from", ""),
            _store.GetSetting("date_to", ""),
            storedDateFilter);
        ApplyViewMode(_store.GetSetting("view_mode", "details"));
        ApplySortMode(_store.GetSetting("sort_mode", "Modified"));
        ApplyFolderSortMode(_store.GetSetting("folder_sort_mode", "NameAsc"));
        var storedDisplayStyle = _store.GetSetting("display_style", "standard");
        ApplyThumbnailSize(ParseSettingInt("thumbnail_size", GalleryThumbnailDefault));
        ApplyDisplayStyle(storedDisplayStyle, persist: false);
        var storedAspectMode = _store.GetSetting("aspect_mode", "");
        ApplyAspectMode(
            string.IsNullOrWhiteSpace(storedAspectMode) ? DefaultAspectModeForDisplayStyle(storedDisplayStyle) : storedAspectMode,
            persist: false);
        _previewVisible.Checked = _store.GetSetting("preview_visible", "1") == "1";
        _detailsVisible.Checked = _store.GetSetting("preview_details_visible", "1") == "1";
        ApplyPreviewVisibility();
        ApplyDetailsVisibility();
        ApplyPreviewSplitterDistance(ParseSettingInt("preview_splitter_distance", 760));
        ApplyStateSummary(report);
        _folderWatcher.ChangesDetected += OnFolderChangesDetected;
        Shown += (_, _) => ApplyPreviewSplitterDistance(ParseSettingInt("preview_splitter_distance", 760));
    }

    public static Task<int> RunUiSmokeAsync(string folder, string searchQuery)
    {
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"native-ui-smoke error=folder-not-found folder=\"{Quote(folder)}\"");
            return Task.FromResult(2);
        }

        var resolvedFolder = Path.GetFullPath(folder);
        var exitCode = 2;
        using var form = new MainForm(resolvedFolder)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(24, 24),
            ShowInTaskbar = false,
        };

        form.Shown += async (_, _) =>
        {
            try
            {
                var report = await form.RunUiSmokeScenarioAsync(resolvedFolder, searchQuery);
                Console.WriteLine(FormatUiSmokeReport(report));
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"native-ui-smoke error={Quote(ex.Message)}");
                exitCode = 2;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return Task.FromResult(exitCode);
    }

    public static Task<int> RunUiScreenshotAsync(string folder, string outputPath, string searchQuery)
    {
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"native-ui-screenshot error=folder-not-found folder=\"{Quote(folder)}\"");
            return Task.FromResult(2);
        }

        var resolvedFolder = Path.GetFullPath(folder);
        var resolvedOutputPath = Path.GetFullPath(outputPath);
        var exitCode = 2;
        using var form = new MainForm(resolvedFolder)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(24, 24),
            ShowInTaskbar = false,
        };

        form.Shown += async (_, _) =>
        {
            try
            {
                var report = await form.RunUiScreenshotScenarioAsync(resolvedFolder, resolvedOutputPath, searchQuery);
                Console.WriteLine(FormatUiScreenshotReport(report));
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"native-ui-screenshot error={Quote(ex.Message)}");
                exitCode = 2;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return Task.FromResult(exitCode);
    }

    public static Task<int> RunFolderSetSmokeAsync(IReadOnlyList<string> folders, string searchQuery)
    {
        var roots = NativeFolderSet.NormalizeDistinct(folders);
        if (roots.Count < 2)
        {
            Console.Error.WriteLine("native-folder-set-smoke error=needs-at-least-two-roots");
            return Task.FromResult(2);
        }

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                Console.Error.WriteLine($"native-folder-set-smoke error=folder-not-found folder=\"{Quote(root)}\"");
                return Task.FromResult(2);
            }
        }

        var exitCode = 2;
        using var form = new MainForm(NativeFolderSet.FormatForDisplay(roots))
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(24, 24),
            ShowInTaskbar = false,
        };

        form.Shown += async (_, _) =>
        {
            try
            {
                var report = await form.RunFolderSetSmokeScenarioAsync(roots, searchQuery);
                Console.WriteLine(FormatFolderSetSmokeReport(report));
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"native-folder-set-smoke error={Quote(ex.Message)}");
                exitCode = 2;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return Task.FromResult(exitCode);
    }

    public static Task<int> RunLargeScrollSmokeAsync(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Console.Error.WriteLine($"native-large-scroll-smoke error=folder-not-found folder=\"{Quote(folder)}\"");
            return Task.FromResult(2);
        }

        var resolvedFolder = Path.GetFullPath(folder);
        var exitCode = 2;
        using var form = new MainForm(resolvedFolder)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(24, 24),
            ShowInTaskbar = false,
        };

        form.Shown += async (_, _) =>
        {
            try
            {
                var report = await form.RunLargeScrollSmokeScenarioAsync(resolvedFolder);
                Console.WriteLine(FormatLargeScrollSmokeReport(report));
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"native-large-scroll-smoke error={Quote(ex.Message)}");
                exitCode = 2;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return Task.FromResult(exitCode);
    }

    public static Task<int> RunDateFilterSmokeAsync(string? folder)
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var resolvedFolder = string.IsNullOrWhiteSpace(folder)
            ? PrepareDateFilterSmokeFolder(projectRoot)
            : Path.GetFullPath(folder);
        if (!Directory.Exists(resolvedFolder))
        {
            Console.Error.WriteLine($"native-date-filter-smoke error=folder-not-found folder=\"{Quote(resolvedFolder)}\"");
            return Task.FromResult(2);
        }

        var exitCode = 2;
        using var form = new MainForm(resolvedFolder)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(24, 24),
            ShowInTaskbar = false,
        };

        form.Shown += async (_, _) =>
        {
            try
            {
                var report = await form.RunDateFilterSmokeScenarioAsync(resolvedFolder);
                Console.WriteLine(FormatDateFilterSmokeReport(report));
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"native-date-filter-smoke error={Quote(ex.Message)}");
                exitCode = 2;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return Task.FromResult(exitCode);
    }

    public static Task<int> RunDateSectionSmokeAsync(string? folder)
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var resolvedFolder = string.IsNullOrWhiteSpace(folder)
            ? PrepareDateFilterSmokeFolder(projectRoot)
            : Path.GetFullPath(folder);
        if (!Directory.Exists(resolvedFolder))
        {
            Console.Error.WriteLine($"native-date-section-smoke error=folder-not-found folder=\"{Quote(resolvedFolder)}\"");
            return Task.FromResult(2);
        }

        var exitCode = 2;
        using var form = new MainForm(resolvedFolder)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(24, 24),
            ShowInTaskbar = false,
        };

        form.Shown += async (_, _) =>
        {
            try
            {
                var report = await form.RunDateSectionSmokeScenarioAsync(resolvedFolder);
                Console.WriteLine(FormatDateSectionSmokeReport(report));
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"native-date-section-smoke error={Quote(ex.Message)}");
                exitCode = 2;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return Task.FromResult(exitCode);
    }

    public static Task<int> RunEnhancedFilterSmokeAsync(string? folder)
    {
        var projectRoot = NativeStateBridge.ResolveProjectRoot();
        var resolvedFolder = string.IsNullOrWhiteSpace(folder)
            ? Path.Combine(projectRoot, ".cache", "native-fixture")
            : Path.GetFullPath(folder);
        if (!Directory.Exists(resolvedFolder))
        {
            Console.Error.WriteLine($"native-enhanced-filter-smoke error=folder-not-found folder=\"{Quote(resolvedFolder)}\"");
            return Task.FromResult(2);
        }

        var exitCode = 2;
        using var form = new MainForm(resolvedFolder)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(24, 24),
            ShowInTaskbar = false,
        };

        form.Shown += async (_, _) =>
        {
            try
            {
                var report = await form.RunEnhancedFilterSmokeScenarioAsync(resolvedFolder);
                Console.WriteLine(FormatEnhancedFilterSmokeReport(report));
                exitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"native-enhanced-filter-smoke error={Quote(ex.Message)}");
                exitCode = 2;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return Task.FromResult(exitCode);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _scanCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The automated UI smoke can close the form after scan cleanup.
            }

            _scanCancellation?.Dispose();
            _scanCancellation = null;
            _folderWatcher.Dispose();
            _cacheScheduler.Dispose();
            _previewRing.Dispose();
            _preview.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8, 6, 8, 4),
        };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var pathToolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Margin = Padding.Empty,
        };
        pathToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        pathToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        pathToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        pathToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        pathToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

        var filterToolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = Padding.Empty,
        };
        filterToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        filterToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        filterToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
        filterToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        _folderText.Dock = DockStyle.Fill;
        _folderText.PlaceholderText = "Image folder path(s); separate with ;";

        _browseButton.Text = "Browse";
        _browseButton.Dock = DockStyle.Fill;
        _browseButton.Click += (_, _) => BrowseFolder();

        _scanButton.Text = "Scan";
        _scanButton.Dock = DockStyle.Fill;
        _scanButton.Click += async (_, _) => await ScanCurrentFolderAsync();

        _cancelButton.Text = "Cancel";
        _cancelButton.Dock = DockStyle.Fill;
        _cancelButton.Enabled = false;
        _cancelButton.Click += (_, _) => _scanCancellation?.Cancel();

        _importButton.Text = "Import";
        _importButton.Dock = DockStyle.Fill;
        _importButton.Click += (_, _) => ImportState();

        _searchText.Dock = DockStyle.Fill;
        _searchText.PlaceholderText = "Search name/folder";
        _searchText.TextChanged += (_, _) => HandleSearchTextChanged();
        _searchText.KeyDown += (_, args) => HandleSearchTextKeyDown(args);

        _clearSearchButton.Text = "Clear";
        _clearSearchButton.Dock = DockStyle.Fill;
        _clearSearchButton.Click += (_, _) => ClearSearchFilter();

        _favoritesOnly.Text = "Favorites";
        _favoritesOnly.Dock = DockStyle.Fill;
        _favoritesOnly.CheckedChanged += (_, _) =>
        {
            ApplyFilter();
            SaveViewState();
        };

        _enhancedOnly.Text = "Enhanced";
        _enhancedOnly.Dock = DockStyle.Fill;
        _enhancedOnly.CheckedChanged += (_, _) =>
        {
            ApplyFilter();
            SaveViewState();
        };

        _favoriteFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _favoriteFilter.Dock = DockStyle.Fill;
        _favoriteFilter.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingFavoriteFilter)
            {
                return;
            }

            ApplyFilter();
            SaveViewState();
        };

        _stateLabel.Dock = DockStyle.Fill;
        _stateLabel.TextAlign = ContentAlignment.MiddleRight;
        _stateLabel.AutoEllipsis = true;
        _browseButton.MinimumSize = TextFitSize(_browseButton, 78, 26);
        _scanButton.MinimumSize = TextFitSize(_scanButton, 64, 26);
        _cancelButton.MinimumSize = TextFitSize(_cancelButton, 70, 26);
        _importButton.MinimumSize = TextFitSize(_importButton, 68, 26);
        _clearSearchButton.MinimumSize = TextFitSize(_clearSearchButton, 58, 26);
        _favoritesOnly.MinimumSize = TextFitSize(_favoritesOnly, 84, 26);
        _enhancedOnly.MinimumSize = TextFitSize(_enhancedOnly, 96, 26);
        pathToolbar.ColumnStyles[1].Width = _browseButton.MinimumSize.Width;
        pathToolbar.ColumnStyles[2].Width = _scanButton.MinimumSize.Width;
        pathToolbar.ColumnStyles[3].Width = _cancelButton.MinimumSize.Width;
        pathToolbar.ColumnStyles[4].Width = _importButton.MinimumSize.Width;
        filterToolbar.ColumnStyles[1].Width = _clearSearchButton.MinimumSize.Width;
        filterToolbar.ColumnStyles[2].Width = _favoritesOnly.MinimumSize.Width;
        filterToolbar.ColumnStyles[4].Width = _enhancedOnly.MinimumSize.Width;

        pathToolbar.Controls.Add(_folderText, 0, 0);
        pathToolbar.Controls.Add(_browseButton, 1, 0);
        pathToolbar.Controls.Add(_scanButton, 2, 0);
        pathToolbar.Controls.Add(_cancelButton, 3, 0);
        pathToolbar.Controls.Add(_importButton, 4, 0);
        pathToolbar.Controls.Add(_stateLabel, 5, 0);
        filterToolbar.Controls.Add(_searchText, 0, 0);
        filterToolbar.Controls.Add(_clearSearchButton, 1, 0);
        filterToolbar.Controls.Add(_favoritesOnly, 2, 0);
        filterToolbar.Controls.Add(_favoriteFilter, 3, 0);
        filterToolbar.Controls.Add(_enhancedOnly, 4, 0);
        toolbar.Controls.Add(pathToolbar, 0, 0);
        toolbar.Controls.Add(filterToolbar, 0, 1);

        var searchControls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(8, 2, 8, 2),
        };
        searchControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        searchControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        searchControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        searchControls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var searchTagLabel = new Label
        {
            Text = "Tags",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _searchChipPanel.Dock = DockStyle.Fill;
        _searchChipPanel.FlowDirection = FlowDirection.LeftToRight;
        _searchChipPanel.WrapContents = false;
        _searchChipPanel.AutoScroll = true;
        _searchChipPanel.BorderStyle = BorderStyle.FixedSingle;

        _searchSuggestion.Dock = DockStyle.Fill;
        _searchSuggestion.DropDownStyle = ComboBoxStyle.DropDownList;
        _searchSuggestion.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingSearchSuggestions)
            {
                _addSearchSuggestionButton.Enabled = _searchSuggestion.SelectedItem is SearchTagSuggestion;
            }
        };

        _addSearchSuggestionButton.Text = "Add Tag";
        _addSearchSuggestionButton.Dock = DockStyle.Fill;
        _addSearchSuggestionButton.MinimumSize = TextFitSize(_addSearchSuggestionButton, 82, 26);
        _addSearchSuggestionButton.Enabled = false;
        _addSearchSuggestionButton.Click += (_, _) => AddSelectedSearchSuggestion();
        searchControls.ColumnStyles[3].Width = _addSearchSuggestionButton.MinimumSize.Width;

        searchControls.Controls.Add(searchTagLabel, 0, 0);
        searchControls.Controls.Add(_searchChipPanel, 1, 0);
        searchControls.Controls.Add(_searchSuggestion, 2, 0);
        searchControls.Controls.Add(_addSearchSuggestionButton, 3, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(8, 4, 8, 2),
        };

        _viewMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _viewMode.Width = 124;
        _viewMode.Items.AddRange(["List", "Grid"]);
        _viewMode.SelectedIndexChanged += (_, _) =>
        {
            ApplyViewMode(_viewMode.SelectedItem?.ToString() == "Grid" ? "grid" : "details");
            SaveViewState();
        };

        _addFolderButton.Text = "Add Folder";
        ApplyTextFitSize(_addFolderButton, 92);
        _addFolderButton.Click += (_, _) => AddFolderToSet();

        _removeFolderButton.Text = "Remove Folder";
        ApplyTextFitSize(_removeFolderButton, 112);
        _removeFolderButton.Click += (_, _) => RemoveSelectedFolderFromSet();

        _recentSetButton.Text = "Recent Set";
        ApplyTextFitSize(_recentSetButton, 92);
        _recentSetButton.Click += async (_, _) => await OpenRecentFolderSetAsync();

        _refreshButton.Text = "Refresh";
        ApplyTextFitSize(_refreshButton, 76);
        _refreshButton.Click += async (_, _) => await RefreshCurrentFolderSetAsync();

        _previousButton.Text = "Previous";
        ApplyTextFitSize(_previousButton, 82);
        _previousButton.Click += (_, _) => SelectOffset(-1);

        _nextButton.Text = "Next";
        ApplyTextFitSize(_nextButton, 82);
        _nextButton.Click += (_, _) => SelectOffset(1);

        _detailButton.Text = "Detail";
        ApplyTextFitSize(_detailButton, 72);
        _detailButton.Click += (_, _) => ShowDetailModal();

        _copyMetadataButton.Text = "Copy";
        ApplyTextFitSize(_copyMetadataButton, 62);
        _copyMetadataButton.Click += (_, _) => CopySelectedMetadata();

        var favoriteLabel = new Label
        {
            Text = "Favorite",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12, 6, 0, 0),
        };

        _favoriteLevel.Minimum = 0;
        _favoriteLevel.Maximum = 5;
        _favoriteLevel.Width = 48;
        _favoriteLevel.ValueChanged += (_, _) =>
        {
            if (!_updatingFavoriteControl)
            {
                SetSelectedFavoriteLevel((int)_favoriteLevel.Value);
            }
        };

        _openFileButton.Text = "Open File(s)";
        ApplyTextFitSize(_openFileButton, 104);
        _openFileButton.Click += (_, _) => OpenSelectedFile();

        _openFolderButton.Text = "Open Folder(s)";
        ApplyTextFitSize(_openFolderButton, 118);
        _openFolderButton.Click += (_, _) => OpenSelectedFolder();

        _deleteButton.Text = "Recycle";
        ApplyTextFitSize(_deleteButton, 76);
        _deleteButton.Click += (_, _) => DeleteSelectedImage();

        _settingsButton.Text = "Settings";
        ApplyTextFitSize(_settingsButton, 82);
        _settingsButton.Click += (_, _) => ShowNativeSettings();

        actions.Controls.Add(_viewMode);
        actions.Controls.Add(_addFolderButton);
        actions.Controls.Add(_removeFolderButton);
        actions.Controls.Add(_recentSetButton);
        actions.Controls.Add(_refreshButton);
        actions.Controls.Add(_previousButton);
        actions.Controls.Add(_nextButton);
        actions.Controls.Add(_detailButton);
        actions.Controls.Add(_copyMetadataButton);
        actions.Controls.Add(favoriteLabel);
        actions.Controls.Add(_favoriteLevel);
        actions.Controls.Add(_openFileButton);
        actions.Controls.Add(_openFolderButton);
        actions.Controls.Add(_deleteButton);
        actions.Controls.Add(_settingsButton);

        var displayControls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(8, 4, 8, 2),
        };

        var sortLabel = new Label
        {
            Text = "Sort",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(0, 6, 0, 0),
        };

        _sortMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _sortMode.Width = 154;
        _sortMode.Items.AddRange(["Modified", "Created", "Name", "Folder", "Size", "Favorite", "Random"]);
        _sortMode.SelectedIndexChanged += (_, _) =>
        {
            SaveSortState();
            ApplyFilter();
        };

        _reshuffleButton.Text = "Reshuffle";
        ApplyTextFitSize(_reshuffleButton, 82);
        _reshuffleButton.Click += (_, _) => ReshuffleSort();

        var dateLabel = new Label
        {
            Text = "Date",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12, 6, 0, 0),
        };

        _dateFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _dateFilter.Width = 138;
        _dateFilter.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingDateFilter)
            {
                return;
            }

            ApplyDateRangeForSelectedFilter();
            ApplyFilter();
            SaveViewState();
        };

        var dateFromLabel = new Label
        {
            Text = "From",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(8, 6, 0, 0),
        };

        ConfigureDateRangePicker(_dateFromPicker);

        var dateToLabel = new Label
        {
            Text = "To",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(4, 6, 0, 0),
        };

        ConfigureDateRangePicker(_dateToPicker);

        var displayStyleLabel = new Label
        {
            Text = "Display",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12, 6, 0, 0),
        };

        _displayStyle.DropDownStyle = ComboBoxStyle.DropDownList;
        _displayStyle.Width = 142;
        _displayStyle.Items.AddRange(["Standard", "Compact", "Poster"]);
        _displayStyle.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingDisplayStyle)
            {
                return;
            }

            ApplyDisplayStyle(_displayStyle.SelectedItem?.ToString() ?? "Standard");
        };

        var aspectLabel = new Label
        {
            Text = "Aspect",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12, 6, 0, 0),
        };

        _aspectMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _aspectMode.Width = 108;
        _aspectMode.Items.AddRange(["Original", "1:1", "2:3"]);
        _aspectMode.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingAspectMode)
            {
                return;
            }

            ApplyAspectMode(_aspectMode.SelectedItem?.ToString() ?? "Original");
        };

        var thumbLabel = new Label
        {
            Text = "Thumb",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12, 6, 0, 0),
        };

        _thumbnailSize.Minimum = GalleryThumbnailMin;
        _thumbnailSize.Maximum = GalleryThumbnailMax;
        _thumbnailSize.Increment = GalleryThumbnailStep;
        _thumbnailSize.Width = 74;
        _thumbnailSize.ValueChanged += (_, _) =>
        {
            if (_updatingThumbnailSize)
            {
                return;
            }

            ApplyThumbnailSize((int)_thumbnailSize.Value);
            SaveThumbnailSize((int)_thumbnailSize.Value);
        };

        _previewVisible.Text = "Preview";
        ApplyTextFitSize(_previewVisible, 78);
        _previewVisible.Checked = true;
        _previewVisible.CheckedChanged += (_, _) =>
        {
            ApplyPreviewVisibility();
            _store.SaveSetting("preview_visible", _previewVisible.Checked ? "1" : "0");
        };

        _detailsVisible.Text = "Details";
        ApplyTextFitSize(_detailsVisible, 72);
        _detailsVisible.Checked = true;
        _detailsVisible.CheckedChanged += (_, _) =>
        {
            ApplyDetailsVisibility();
            _store.SaveSetting("preview_details_visible", _detailsVisible.Checked ? "1" : "0");
        };

        displayControls.Controls.Add(sortLabel);
        displayControls.Controls.Add(_sortMode);
        displayControls.Controls.Add(_reshuffleButton);
        displayControls.Controls.Add(dateLabel);
        displayControls.Controls.Add(_dateFilter);
        displayControls.Controls.Add(dateFromLabel);
        displayControls.Controls.Add(_dateFromPicker);
        displayControls.Controls.Add(dateToLabel);
        displayControls.Controls.Add(_dateToPicker);
        displayControls.Controls.Add(displayStyleLabel);
        displayControls.Controls.Add(_displayStyle);
        displayControls.Controls.Add(aspectLabel);
        displayControls.Controls.Add(_aspectMode);
        displayControls.Controls.Add(thumbLabel);
        displayControls.Controls.Add(_thumbnailSize);
        displayControls.Controls.Add(_previewVisible);
        displayControls.Controls.Add(_detailsVisible);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760,
        };
        split.SplitterMoved += (_, _) => SavePreviewSplitterDistance();
        _mainSplit = split;

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.VirtualMode = true;
        _list.MultiSelect = true;
        _list.ShowGroups = false;
        _gridImages.ColorDepth = ColorDepth.Depth32Bit;
        _list.SmallImageList = _gridImages;
        _list.LargeImageList = _gridImages;
        _list.Columns.Add("Name", 300);
        _list.Columns.Add("Fav", 48);
        _list.Columns.Add("Dims", 88);
        _list.Columns.Add("Folder", 260);
        _list.Columns.Add("Modified", 150);
        _list.Columns.Add("Size", 86);
        _list.RetrieveVirtualItem += (_, args) =>
        {
            if (args.ItemIndex < 0 || args.ItemIndex >= _visibleImages.Count)
            {
                args.Item = new ListViewItem("");
                return;
            }

            args.Item = CreateListItem(_visibleImages[args.ItemIndex]);
        };
        _list.SelectedIndexChanged += async (_, _) => await LoadSelectedPreviewAsync();
        _list.MouseMove += async (_, args) =>
        {
            if (args.Button != MouseButtons.None)
            {
                return;
            }

            var item = _list.GetItemAt(args.X, args.Y);
            if (item is null)
            {
                await EndHoverQuickPreviewAsync();
                return;
            }

            await ShowHoverQuickPreviewAsync(item.Index);
        };
        _list.MouseLeave += async (_, _) => await EndHoverQuickPreviewAsync();
        _list.MouseDown += (_, args) =>
        {
            if (_list.GetItemAt(args.X, args.Y) is null)
            {
                ClearImageSelection();
            }
        };
        _list.MouseWheel += (_, args) =>
        {
            if ((Control.ModifierKeys & (Keys.Control | Keys.Alt)) == Keys.None)
            {
                return;
            }

            if (HandleGalleryWheelZoom(args.Delta) && args is HandledMouseEventArgs handledArgs)
            {
                handledArgs.Handled = true;
            }
        };
        _list.DoubleClick += (_, _) => OpenSelectedFile();

        var browserPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        browserPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        browserPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var folderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 4, 8, 2),
        };
        folderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        folderPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        folderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        _folderBucketLabel.Dock = DockStyle.Fill;
        _folderBucketLabel.Text = "Folders";
        _folderBucketLabel.TextAlign = ContentAlignment.MiddleLeft;
        _folderBucketLabel.AutoEllipsis = true;

        _folderBuckets.Dock = DockStyle.Fill;
        _folderBuckets.CheckOnClick = true;
        _folderBuckets.SelectedIndexChanged += (_, _) =>
        {
            if (!_updatingFolderBucketKeyboardRange &&
                (ModifierKeys & Keys.Shift) != Keys.Shift &&
                _folderBuckets.SelectedIndex >= 0)
            {
                _folderBucketRangeAnchorIndex = _folderBuckets.SelectedIndex;
            }

            UpdateFolderBucketButtons();
        };
        _folderBuckets.KeyDown += (_, args) => HandleFolderBucketKeyDown(args);
        _folderBuckets.MouseUp += (_, args) => HandleFolderBucketMouseUp(args);
        _folderBuckets.ItemCheck += (_, _) =>
        {
            if (_updatingFolderBuckets || IsDisposed)
            {
                return;
            }

            BeginInvoke(() =>
            {
                SaveFolderBucketState();
                ApplyFilter();
            });
        };

        _folderSortMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _folderSortMode.Width = 132;
        _folderSortMode.Items.AddRange(["NameAsc", "NameDesc", "CountDesc", "CountAsc"]);
        _folderSortMode.SelectedIndexChanged += (_, _) =>
        {
            SaveFolderSortState();
            BuildFolderBuckets();
            ApplyFilter();
        };

        var folderActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };

        _showAllFoldersButton.Text = "All";
        ApplyTextFitSize(_showAllFoldersButton, 54);
        _showAllFoldersButton.Click += (_, _) => SetAllFolderBuckets(visible: true);

        _hideAllFoldersButton.Text = "None";
        ApplyTextFitSize(_hideAllFoldersButton, 58);
        _hideAllFoldersButton.Click += (_, _) => SetAllFolderBuckets(visible: false);

        _invertFoldersButton.Text = "Invert";
        ApplyTextFitSize(_invertFoldersButton, 64);
        _invertFoldersButton.Click += (_, _) => InvertFolderBuckets();

        _showSelectedFoldersButton.Text = "Show";
        ApplyTextFitSize(_showSelectedFoldersButton, 76);
        _showSelectedFoldersButton.Click += (_, _) => SetSelectedFolderBuckets(visible: true);

        _hideSelectedFoldersButton.Text = "Hide";
        ApplyTextFitSize(_hideSelectedFoldersButton, 74);
        _hideSelectedFoldersButton.Click += (_, _) => SetSelectedFolderBuckets(visible: false);

        _clearFolderSelectionButton.Text = "Clear";
        ApplyTextFitSize(_clearFolderSelectionButton, 76);
        _clearFolderSelectionButton.Click += (_, _) => ClearFolderBucketSelection();

        folderActions.Controls.Add(_folderSortMode);
        folderActions.Controls.Add(_showAllFoldersButton);
        folderActions.Controls.Add(_hideAllFoldersButton);
        folderActions.Controls.Add(_invertFoldersButton);
        folderActions.Controls.Add(_showSelectedFoldersButton);
        folderActions.Controls.Add(_hideSelectedFoldersButton);
        folderActions.Controls.Add(_clearFolderSelectionButton);
        folderPanel.RowStyles[2].Height = Math.Max(folderPanel.RowStyles[2].Height, FlowPanelRowHeight(folderActions, 30));
        browserPanel.RowStyles[0].Height = Math.Max(browserPanel.RowStyles[0].Height, 118 + folderPanel.RowStyles[2].Height);

        folderPanel.Controls.Add(_folderBucketLabel, 0, 0);
        folderPanel.Controls.Add(_folderBuckets, 0, 1);
        folderPanel.Controls.Add(folderActions, 0, 2);
        browserPanel.Controls.Add(folderPanel, 0, 0);
        browserPanel.Controls.Add(_list, 0, 1);
        split.Panel1.Controls.Add(browserPanel);

        var previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.FromArgb(20, 20, 20);
        _preview.SizeMode = PictureBoxSizeMode.Zoom;

        _selectionLabel.Dock = DockStyle.Fill;
        _selectionLabel.Padding = new Padding(8, 2, 8, 2);
        _selectionLabel.AutoEllipsis = true;
        _selectionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _selectionLabel.Text = "Selected 0 / 0";

        _previewLabel.Dock = DockStyle.Fill;
        _previewLabel.Padding = new Padding(8, 4, 8, 4);
        _previewLabel.AutoEllipsis = true;
        _previewLabel.Text = "Select an image.";

        var previewTabActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(4, 2, 4, 2),
        };
        previewTabActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewTabActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        previewTabActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        previewTabActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));

        _previewTabs.Dock = DockStyle.Fill;
        _previewTabs.ShowToolTips = true;
        _previewTabs.SelectedIndexChanged += async (_, _) => await ActivateSelectedPreviewTabAsync();

        _pinPreviewTabButton.Text = "Pin";
        _pinPreviewTabButton.Dock = DockStyle.Fill;
        ApplyTextFitSize(_pinPreviewTabButton, 54, 26);
        _pinPreviewTabButton.Click += (_, _) => ToggleActivePreviewTabPin();

        _restorePreviewTabButton.Text = "Restore";
        _restorePreviewTabButton.Dock = DockStyle.Fill;
        ApplyTextFitSize(_restorePreviewTabButton, 70, 26);
        _restorePreviewTabButton.Click += async (_, _) => await RestoreLastClosedPreviewTabAsync();

        _closePreviewTabButton.Text = "Close";
        _closePreviewTabButton.Dock = DockStyle.Fill;
        ApplyTextFitSize(_closePreviewTabButton, 64, 26);
        _closePreviewTabButton.Click += (_, _) => CloseActivePreviewTab();

        previewTabActions.Controls.Add(_previewTabs, 0, 0);
        previewTabActions.Controls.Add(_pinPreviewTabButton, 1, 0);
        previewTabActions.Controls.Add(_restorePreviewTabButton, 2, 0);
        previewTabActions.Controls.Add(_closePreviewTabButton, 3, 0);

        previewPanel.Controls.Add(_preview, 0, 0);
        previewPanel.Controls.Add(previewTabActions, 0, 1);
        previewPanel.Controls.Add(_selectionLabel, 0, 2);
        previewPanel.Controls.Add(_previewLabel, 0, 3);
        split.Panel2.Controls.Add(previewPanel);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Padding = new Padding(8, 0, 8, 0);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Text = "Ready.";
        root.RowStyles[0].Height = Math.Max(root.RowStyles[0].Height, StackedControlRowHeight(_browseButton, toolbar.Padding, 2, 42));
        root.RowStyles[1].Height = Math.Max(root.RowStyles[1].Height, ControlRowHeight(_addSearchSuggestionButton, searchControls.Padding, 34));
        root.RowStyles[2].Height = Math.Max(root.RowStyles[2].Height, FlowPanelRowHeight(actions, 38, 2));
        root.RowStyles[3].Height = Math.Max(root.RowStyles[3].Height, FlowPanelRowHeight(displayControls, 38, 2));
        root.RowStyles[5].Height = Math.Max(root.RowStyles[5].Height, TextFitHeight(_statusLabel, 20) + _statusLabel.Padding.Vertical);

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(searchControls, 0, 1);
        root.Controls.Add(actions, 0, 2);
        root.Controls.Add(displayControls, 0, 3);
        root.Controls.Add(split, 0, 4);
        root.Controls.Add(_statusLabel, 0, 5);
        Controls.Add(root);
        ApplyThumbnailSize(GalleryThumbnailDefault);
        RefreshSearchTagSuggestions();
        RefreshSearchChipControls();
        UpdateSelectionActions();
    }

    private static void ApplyTextFitSize(Control control, int minimumWidth, int minimumHeight = 28)
    {
        var size = TextFitSize(control, minimumWidth, minimumHeight);
        control.MinimumSize = size;
        control.Width = size.Width;
        control.Height = size.Height;
    }

    private static Size TextFitSize(Control control, int minimumWidth, int minimumHeight)
    {
        return new Size(
            Math.Max(minimumWidth, TextFitWidth(control, minimumWidth)),
            Math.Max(minimumHeight, TextFitHeight(control, minimumHeight)));
    }

    private static int TextFitWidth(Control control, int minimum)
    {
        if (string.IsNullOrWhiteSpace(control.Text))
        {
            return minimum;
        }

        var measured = TextRenderer.MeasureText(control.Text, control.Font);
        var extraWidth = control is CheckBox ? 52 : 12;
        return measured.Width + extraWidth;
    }

    private static int TextFitHeight(Control control, int minimum)
    {
        if (string.IsNullOrWhiteSpace(control.Text))
        {
            return minimum;
        }

        var measured = TextRenderer.MeasureText(control.Text, control.Font);
        return measured.Height + 8;
    }

    private static float ControlRowHeight(Control control, Padding padding, int minimum)
    {
        return Math.Max(minimum, TextFitHeight(control, minimum) + padding.Vertical);
    }

    private static float StackedControlRowHeight(Control control, Padding padding, int rows, int minimum)
    {
        return Math.Max(minimum, (TextFitHeight(control, minimum) * Math.Max(1, rows)) + padding.Vertical);
    }

    private static float FlowPanelRowHeight(FlowLayoutPanel panel, int minimum, int rows = 1)
    {
        if (panel.Controls.Count == 0)
        {
            return minimum;
        }

        var controlHeight = panel.Controls
            .Cast<Control>()
            .Where(static control => control.Visible)
            .Select(static control => control.Height + control.Margin.Vertical)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(minimum, (controlHeight * Math.Max(1, rows)) + panel.Padding.Vertical);
    }

    private async Task<NativeUiSmokeReport> RunUiSmokeScenarioAsync(string folder, string searchQuery)
    {
        var beforeEnhancementState = EnhancementStateFingerprint();

        _folderText.Text = folder;
        _store.SaveSetting("hidden_folder_buckets", "");
        _searchText.Text = "";
        _favoritesOnly.Checked = false;
        _enhancedOnly.Checked = false;
        SelectFavoriteFilter("all");
        ApplyManualDateRange(null, null);
        ImportState();
        var albums = _store.CountAlbums();
        var albumImages = _store.CountAlbumImages();
        var browserStateKeys = _store.CountBrowserStateKeys();
        var settingsImported = _store.GetSetting("browser_settings_found", "0") == "1";

        await ScanCurrentFolderAsync();
        Require(_allImages.Count > 0, "scan produced no images");
        Require(_visibleImages.Count > 0, "scan produced no visible images");
        var scannedImages = _allImages.Count;
        var initialVisible = _visibleImages.Count;
        var folderBuckets = _folderBuckets.Items.Count;
        Require(folderBuckets > 0, "folder buckets were not built");
        Require(folderBuckets >= 2, "folder buckets did not include nested fixture folders");
        SetAllFolderBuckets(visible: false);
        var folderHideAll = _visibleImages.Count == 0;
        Require(folderHideAll, "folder hide-all did not filter visible images");
        SetAllFolderBuckets(visible: true);
        Require(_visibleImages.Count == initialVisible, "folder show-all did not restore visible images");
        ApplyFolderSortMode("CountDesc");
        var folderSortMode = string.Equals(_store.GetSetting("folder_sort_mode", ""), "CountDesc", StringComparison.OrdinalIgnoreCase) &&
            _folderBuckets.Items.Count >= 2 &&
            _folderBuckets.Items.Cast<FolderBucket>().First().Count >= _folderBuckets.Items.Cast<FolderBucket>().Last().Count;
        Require(folderSortMode, "folder sort mode did not persist and sort buckets by count");
        _folderBuckets.ClearSelected();
        _folderBuckets.SelectedIndex = 0;
        Application.DoEvents();
        SetSelectedFolderBuckets(visible: false);
        var folderHideSelected = _visibleImages.Count > 0 && _visibleImages.Count < initialVisible;
        Require(folderHideSelected, $"folder hide-selected did not filter a selected bucket selected={_folderBuckets.SelectedIndex} checked={_folderBuckets.CheckedItems.Count}/{_folderBuckets.Items.Count} visible={_visibleImages.Count}/{initialVisible}");
        SetSelectedFolderBuckets(visible: true);
        var folderShowSelected = _visibleImages.Count == initialVisible;
        Require(folderShowSelected, "folder show-selected did not restore a selected bucket");
        SetAllFolderBuckets(visible: true);
        var rangeEndIndex = Math.Min(1, _folderBuckets.Items.Count - 1);
        _folderBucketRangeAnchorIndex = 0;
        var folderRangeHide = ApplyFolderBucketRange(0, rangeEndIndex, visible: false) &&
            _folderBuckets.Items.Count >= 2 &&
            !_folderBuckets.GetItemChecked(0) &&
            !_folderBuckets.GetItemChecked(rangeEndIndex) &&
            _visibleImages.Count == 0;
        Require(folderRangeHide, "folder range hide did not filter contiguous buckets");
        _folderBucketRangeAnchorIndex = rangeEndIndex;
        var folderRangeShow = ApplyFolderBucketRange(rangeEndIndex, 0, visible: true) &&
            _folderBuckets.GetItemChecked(0) &&
            _folderBuckets.GetItemChecked(rangeEndIndex) &&
            _visibleImages.Count == initialVisible;
        Require(folderRangeShow, "folder range show did not restore contiguous buckets");
        SetAllFolderBuckets(visible: true);
        _folderBuckets.ClearSelected();
        _folderBuckets.SelectedIndex = 0;
        _folderBucketRangeAnchorIndex = 0;
        var hiddenBeforeKeyboardRange = _store.GetSetting("hidden_folder_buckets", "");
        var keyboardRangeArgs = new KeyEventArgs(Keys.Shift | Keys.Down);
        HandleFolderBucketKeyDown(keyboardRangeArgs);
        var keyboardRangeSelected = keyboardRangeArgs.Handled &&
            keyboardRangeArgs.SuppressKeyPress &&
            _folderBucketRangeAnchorIndex == 0 &&
            _folderBuckets.SelectedIndex == rangeEndIndex &&
            _folderBuckets.GetItemChecked(0) &&
            _folderBuckets.GetItemChecked(rangeEndIndex) &&
            string.Equals(_store.GetSetting("hidden_folder_buckets", ""), hiddenBeforeKeyboardRange, StringComparison.Ordinal);
        Require(keyboardRangeSelected, "folder keyboard range selection changed visibility or failed to move focus");
        var keyboardRangeApplyArgs = new KeyEventArgs(Keys.Space);
        HandleFolderBucketKeyDown(keyboardRangeApplyArgs);
        var folderKeyboardRangeSelection = keyboardRangeApplyArgs.Handled &&
            keyboardRangeApplyArgs.SuppressKeyPress &&
            !_folderBuckets.GetItemChecked(0) &&
            !_folderBuckets.GetItemChecked(rangeEndIndex) &&
            _visibleImages.Count == 0;
        Require(folderKeyboardRangeSelection, "folder keyboard range apply did not filter contiguous buckets");
        SetAllFolderBuckets(visible: true);
        ClearFolderBucketSelection();
        var folderClearSelection = _folderBuckets.SelectedIndices.Count == 0;
        Require(folderClearSelection, "folder clear-selection did not clear selected buckets");

        ApplySortMode("Name");
        ApplyFilter();
        var sortName = _visibleImages.SequenceEqual(
            _visibleImages.OrderBy(static item => item.Filename, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase));
        Require(sortName, "name sort failed");

        ApplySortMode("Random");
        var beforeReshuffle = _visibleImages.Select(static item => item.AbsolutePath).ToArray();
        ReshuffleSort();
        var afterReshuffle = _visibleImages.Select(static item => item.AbsolutePath).ToArray();
        var randomReshuffle = beforeReshuffle.Length == afterReshuffle.Length && _sortMode.SelectedItem?.ToString() == "Random";
        Require(randomReshuffle, "random reshuffle failed");
        ApplySortMode("Modified");
        ApplyFilter();

        var oldThumbnailSize = _gridImages.ImageSize.Width;
        _thumbnailSize.Value = Math.Min(_thumbnailSize.Maximum, oldThumbnailSize + 16);
        var thumbnailSize = _gridImages.ImageSize.Width == (int)_thumbnailSize.Value;
        Require(thumbnailSize, "thumbnail size control failed");
        _thumbnailSize.Value = oldThumbnailSize;

        var oldViewMode = _list.View == View.LargeIcon ? "grid" : "details";
        ApplyViewMode("grid");
        ApplyThumbnailSize(GalleryThumbnailDefault);
        SaveThumbnailSize(GalleryThumbnailDefault);
        var wheelHandled = HandleGalleryWheelZoom(120);
        var wheelSize = GalleryThumbnailDefault + GalleryThumbnailStep;
        var galleryWheelZoom = wheelHandled &&
            (int)_thumbnailSize.Value == wheelSize &&
            _gridImages.ImageSize.Width == wheelSize &&
            string.Equals(_store.GetSetting("thumbnail_size", ""), wheelSize.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Require(galleryWheelZoom, "gallery wheel zoom failed");
        var keyboardMinusHandled = HandleGalleryKeyboardZoom(Keys.Control | Keys.OemMinus);
        var keyboardPlusHandled = HandleGalleryKeyboardZoom(Keys.Control | Keys.Oemplus);
        var keyboardResetHandled = HandleGalleryKeyboardZoom(Keys.Control | Keys.D0);
        var galleryKeyboardZoom = keyboardMinusHandled &&
            keyboardPlusHandled &&
            keyboardResetHandled &&
            (int)_thumbnailSize.Value == GalleryThumbnailReset &&
            _gridImages.ImageSize.Width == GalleryThumbnailReset &&
            string.Equals(_store.GetSetting("thumbnail_size", ""), GalleryThumbnailReset.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Require(galleryKeyboardZoom, "gallery keyboard zoom failed");
        ApplyViewMode(oldViewMode);
        ApplyThumbnailSize(oldThumbnailSize);
        SaveThumbnailSize(oldThumbnailSize);

        var oldDisplayStyle = CurrentDisplayStyle();
        var oldAspectMode = CurrentAspectMode();
        ApplyDisplayStyle("compact");
        var compactGridImageSize = _gridImages.ImageSize;
        var compactDisplayMode = string.Equals(CurrentDisplayStyle(), "compact", StringComparison.OrdinalIgnoreCase) &&
            _list.View == View.LargeIcon &&
            compactGridImageSize.Width == 64 &&
            compactGridImageSize.Height == 64 &&
            string.Equals(CurrentAspectMode(), "square", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_store.GetSetting("display_style", ""), "compact", StringComparison.OrdinalIgnoreCase);
        Require(compactDisplayMode, "compact display mode failed");
        ApplyDisplayStyle("poster");
        var posterGridImageSize = _gridImages.ImageSize;
        var posterDisplayMode = string.Equals(CurrentDisplayStyle(), "poster", StringComparison.OrdinalIgnoreCase) &&
            _list.View == View.LargeIcon &&
            posterGridImageSize.Width == 192 &&
            posterGridImageSize.Height > posterGridImageSize.Width &&
            string.Equals(CurrentAspectMode(), "portrait", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_store.GetSetting("display_style", ""), "poster", StringComparison.OrdinalIgnoreCase);
        Require(posterDisplayMode, "poster display mode failed");
        var displayModes = compactDisplayMode && posterDisplayMode;
        var displayModeVisuals = displayModes &&
            compactGridImageSize.Width == compactGridImageSize.Height &&
            posterGridImageSize.Height > posterGridImageSize.Width;
        Require(displayModeVisuals, "display mode visual sizing failed");

        ApplyAspectMode("square");
        var squareAspectMode = string.Equals(CurrentAspectMode(), "square", StringComparison.OrdinalIgnoreCase) &&
            _gridImages.ImageSize.Height == _gridImages.ImageSize.Width &&
            string.Equals(_store.GetSetting("aspect_mode", ""), "square", StringComparison.OrdinalIgnoreCase);
        Require(squareAspectMode, "square aspect mode failed");
        ApplyAspectMode("portrait");
        var portraitAspectMode = string.Equals(CurrentAspectMode(), "portrait", StringComparison.OrdinalIgnoreCase) &&
            _gridImages.ImageSize.Height > _gridImages.ImageSize.Width &&
            string.Equals(_store.GetSetting("aspect_mode", ""), "portrait", StringComparison.OrdinalIgnoreCase);
        Require(portraitAspectMode, "portrait aspect mode failed");
        ApplyAspectMode("original");
        var originalAspectMode = string.Equals(CurrentAspectMode(), "original", StringComparison.OrdinalIgnoreCase) &&
            _gridImages.ImageSize.Height > _gridImages.ImageSize.Width &&
            string.Equals(_store.GetSetting("aspect_mode", ""), "original", StringComparison.OrdinalIgnoreCase);
        Require(originalAspectMode, "original aspect mode failed");
        var aspectModes = squareAspectMode && portraitAspectMode && originalAspectMode;

        ApplyDisplayStyle(oldDisplayStyle);
        ApplyAspectMode(oldAspectMode);
        ApplyThumbnailSize(oldThumbnailSize);
        _store.SaveSetting("thumbnail_size", oldThumbnailSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _store.SaveSetting("aspect_mode", oldAspectMode);

        _previewVisible.Checked = false;
        var previewToggle = _mainSplit?.Panel2Collapsed == true;
        Require(previewToggle, "preview visibility toggle failed");
        _previewVisible.Checked = true;

        _detailsVisible.Checked = false;
        var detailsToggle = !_previewLabel.Visible;
        Require(detailsToggle, "preview details toggle failed");
        _detailsVisible.Checked = true;

        var previewSplitter = VerifyPreviewSplitterPersistence();
        Require(previewSplitter, "preview splitter persistence failed");

        var previewTarget = _visibleImages[0];
        SelectImage(previewTarget.AbsolutePath);
        var navigationButtons = _nextButton.Enabled;

        await LoadSelectedPreviewAsync();
        var previewLoaded = await WaitForPreviewAsync(previewTarget.Filename);
        Require(previewLoaded, "preview did not load fixture image");
        var previewTabsFirst = _previewTabs.TabPages.Count == 1 &&
            string.Equals(ActivePreviewTabState()?.Path, previewTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        var previewTabPin = ToggleActivePreviewTabPin() &&
            ActivePreviewTabState()?.IsPinned == true &&
            string.Equals(_pinPreviewTabButton.Text, "Unpin", StringComparison.OrdinalIgnoreCase);
        Require(previewTabPin, "preview tab pin failed");
        var secondPreviewTarget = _visibleImages.FirstOrDefault(image =>
            !string.Equals(image.AbsolutePath, previewTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase));
        Require(secondPreviewTarget is not null, "preview tab smoke needs a second fixture image");
        SelectImage(secondPreviewTarget!.AbsolutePath);
        await LoadSelectedPreviewAsync();
        var previewTabs = previewTabsFirst &&
            _previewTabs.TabPages.Count >= 2 &&
            string.Equals(ActivePreviewTabState()?.Path, secondPreviewTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Require(previewTabs, "preview tab creation failed");
        var previewTabCloseAction = CloseActivePreviewTab();
        await LoadSelectedPreviewAsync();
        var activeAfterClose = ActivePreviewTabState();
        var previewTabClose = previewTabCloseAction &&
            _previewTabs.TabPages.Count == 1 &&
            activeAfterClose?.IsPinned == true &&
            string.Equals(activeAfterClose.Path, previewTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Require(previewTabClose, "preview tab close failed");
        var previewTabRestoreAction = await RestoreLastClosedPreviewTabAsync();
        await LoadSelectedPreviewAsync();
        var activeAfterRestore = ActivePreviewTabState();
        var previewTabRestore = previewTabRestoreAction &&
            _previewTabs.TabPages.Count >= 2 &&
            activeAfterRestore?.IsPinned == false &&
            string.Equals(activeAfterRestore.Path, secondPreviewTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Require(previewTabRestore, "preview tab restore failed");
        var hoverPreviewTargetIndex = _visibleImages.FindIndex(image =>
            !string.Equals(image.AbsolutePath, previewTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(image.AbsolutePath, secondPreviewTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase));
        Require(hoverPreviewTargetIndex >= 0, "hover quick preview smoke needs a third fixture image");
        var hoverPreviewTarget = _visibleImages[hoverPreviewTargetIndex];
        var selectedBeforeHover = GetSelectedImage();
        var activeBeforeHover = ActivePreviewTabState();
        Require(selectedBeforeHover is not null && activeBeforeHover is not null, "hover quick preview needs an active selected preview");
        var tabCountBeforeHover = _previewTabs.TabPages.Count;
        var hoverPreviewAction = await ShowHoverQuickPreviewAsync(hoverPreviewTargetIndex);
        var hoverPreviewLoaded = await WaitForPreviewAsync(hoverPreviewTarget.Filename);
        var hoverQuickPreviewTransient = hoverPreviewAction &&
            hoverPreviewLoaded &&
            _previewLabel.Text.Contains("Hover preview:", StringComparison.OrdinalIgnoreCase) &&
            _previewTabs.TabPages.Count == tabCountBeforeHover &&
            string.Equals(ActivePreviewTabState()?.Path, activeBeforeHover?.Path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetSelectedImage()?.AbsolutePath, selectedBeforeHover?.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        await EndHoverQuickPreviewAsync();
        var hoverQuickPreview = hoverQuickPreviewTransient &&
            await WaitForPreviewAsync(selectedBeforeHover!.Filename) &&
            !_previewLabel.Text.Contains("Hover preview:", StringComparison.OrdinalIgnoreCase) &&
            _previewTabs.TabPages.Count == tabCountBeforeHover &&
            string.Equals(ActivePreviewTabState()?.Path, activeBeforeHover?.Path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetSelectedImage()?.AbsolutePath, selectedBeforeHover?.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Require(hoverQuickPreview, "hover quick preview changed tabs or selection");
        var metadataTarget = _visibleImages.FirstOrDefault(static image =>
            image.Prompt.Contains("native metadata prompt", StringComparison.OrdinalIgnoreCase) &&
            image.NegativePrompt.Contains("native negative prompt", StringComparison.OrdinalIgnoreCase));
        Require(metadataTarget is not null, "metadata fixture was not scanned");

        var selectedCount = _selectionLabel.Text.Contains("Selected 1", StringComparison.OrdinalIgnoreCase);
        Require(selectedCount, "selected count label failed");

        var galleryStateTargetIndex = Math.Min(2, _visibleImages.Count - 1);
        var galleryStateTarget = _visibleImages[galleryStateTargetIndex];
        SelectImage(galleryStateTarget.AbsolutePath);
        ClearImageSelection();
        ApplyFilter();
        var galleryStateRestore = string.Equals(GetSelectedImage()?.AbsolutePath, galleryStateTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_store.GetSetting("last_selected_image", ""), galleryStateTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_store.GetSetting("last_visible_index", ""), galleryStateTargetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        Require(galleryStateRestore, "gallery state selection restore failed");
        SelectImage(_visibleImages[0].AbsolutePath);
        await LoadSelectedPreviewAsync();

        _list.SelectedIndices.Clear();
        _list.SelectedIndices.Add(0);
        _list.SelectedIndices.Add(1);
        UpdateSelectionActions();
        var multiSelection = _list.SelectedIndices.Count == 2 && _selectionLabel.Text.Contains("Selected 2", StringComparison.OrdinalIgnoreCase);
        Require(multiSelection, "multi-selection count failed");
        var bulkFavoriteTargets = GetSelectedImages().Select(static image => image.AbsolutePath).ToArray();
        SetSelectedFavoriteLevel(5);
        var bulkFavoriteSet = bulkFavoriteTargets.Length == 2 && bulkFavoriteTargets.All(path => FavoriteLevelForPath(path) == 5);
        Require(bulkFavoriteSet, "bulk favorite set failed");
        SetSelectedFavoriteLevel(0);
        var bulkFavoriteClear = bulkFavoriteTargets.Length == 2 && bulkFavoriteTargets.All(path => FavoriteLevelForPath(path) == 0);
        Require(bulkFavoriteClear, "bulk favorite clear failed");

        var bulkOpenSelection = _visibleImages
            .GroupBy(static image => Path.GetDirectoryName(image.AbsolutePath) ?? "", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Take(2)
            .ToList();
        if (bulkOpenSelection.Count < 2)
        {
            bulkOpenSelection = _visibleImages.Take(2).ToList();
        }

        SelectImages(bulkOpenSelection.Select(static image => image.AbsolutePath));
        var bulkOpenSelectedImages = GetSelectedImages();
        var bulkOpenFileTargets = GetSelectedFileOpenTargets(bulkOpenSelectedImages);
        var bulkOpenFolderTargets = GetSelectedFolderOpenTargets(bulkOpenSelectedImages);
        var bulkOpenFiles = bulkOpenSelectedImages.Count >= 2 &&
            bulkOpenFileTargets.Count == bulkOpenSelectedImages.Count &&
            TryOpenSelectedFiles(static _ => { }, updateStatus: false);
        Require(bulkOpenFiles, "bulk open files plan failed");
        var bulkOpenFolders = bulkOpenSelectedImages.Count >= 2 &&
            bulkOpenFolderTargets.Count >= 1 &&
            bulkOpenFolderTargets.Count <= bulkOpenSelectedImages.Count &&
            TryOpenSelectedFolders(static _ => { }, updateStatus: false);
        Require(bulkOpenFolders, "bulk open folders plan failed");

        SetFavoriteLevelForPath(bulkFavoriteTargets[0], 5);
        ClearImageSelection();
        var backgroundClear = _list.SelectedIndices.Count == 0 && _selectionLabel.Text.Contains("Selected 0", StringComparison.OrdinalIgnoreCase);
        Require(backgroundClear, "background clear selection failed");
        SelectImage(metadataTarget!.AbsolutePath);
        await LoadSelectedPreviewAsync();
        var metadataPreview = _previewLabel.Text.Contains("Prompt:", StringComparison.OrdinalIgnoreCase) &&
            _previewLabel.Text.Contains("Negative prompt:", StringComparison.OrdinalIgnoreCase);
        Require(metadataPreview, "preview metadata display failed");
        var metadataCopy = CopySelectedMetadata(useSystemClipboard: false) &&
            _lastMetadataCopyText.Contains("PNG info", StringComparison.OrdinalIgnoreCase) &&
            _lastMetadataCopyText.Contains("Prompt:", StringComparison.OrdinalIgnoreCase) &&
            _lastMetadataCopyText.Contains("native metadata prompt", StringComparison.OrdinalIgnoreCase) &&
            _lastMetadataCopyText.Contains("Negative prompt:", StringComparison.OrdinalIgnoreCase) &&
            _lastMetadataCopyText.Contains("native negative prompt", StringComparison.OrdinalIgnoreCase) &&
            _lastMetadataCopyText.Contains("Settings:", StringComparison.OrdinalIgnoreCase) &&
            _lastMetadataCopyText.Contains("Steps: 12", StringComparison.OrdinalIgnoreCase);
        Require(metadataCopy, "metadata copy action failed");

        var promptTag = SplitPromptTags(metadataTarget!.Prompt).FirstOrDefault();
        Require(promptTag is not null, "metadata fixture prompt tag missing");
        var normalizedPromptTag = NormalizePromptTag(promptTag!);
        var promptTagAction = AddPromptTagToSearch(promptTag!) &&
            string.Equals(_searchText.Text, normalizedPromptTag, StringComparison.OrdinalIgnoreCase) &&
            _visibleImages.Any(image => string.Equals(image.AbsolutePath, metadataTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase));
        Require(promptTagAction, "prompt tag search action failed");
        var promptActionChip = _searchChipPanel.Controls
            .OfType<Button>()
            .Any(button => string.Equals(button.Tag as string, normalizedPromptTag, StringComparison.OrdinalIgnoreCase));
        _searchText.Text = $"{normalizedPromptTag}, fixture";
        var queryChipTags = _searchChipPanel.Controls
            .OfType<Button>()
            .Select(static button => button.Tag as string)
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        var searchChipsMirrored = queryChipTags.Count == 2 &&
            queryChipTags.Any(tag => string.Equals(tag, normalizedPromptTag, StringComparison.OrdinalIgnoreCase)) &&
            queryChipTags.Any(static tag => string.Equals(tag, "fixture", StringComparison.OrdinalIgnoreCase));
        RemoveSearchTag(normalizedPromptTag);
        var chipTagsAfterRemove = _searchChipPanel.Controls
            .OfType<Button>()
            .Select(static button => button.Tag as string)
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        var searchChips = promptActionChip &&
            searchChipsMirrored &&
            string.Equals(_searchText.Text, "fixture", StringComparison.OrdinalIgnoreCase) &&
            chipTagsAfterRemove.Count == 1 &&
            chipTagsAfterRemove.Any(static tag => string.Equals(tag, "fixture", StringComparison.OrdinalIgnoreCase)) &&
            _visibleImages.Any(image => string.Equals(image.AbsolutePath, metadataTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase));
        Require(searchChips, "search chips did not mirror and remove query tags");
        _searchText.Text = $" fixture, Fixture, {normalizedPromptTag},";
        var trailingCommaCommitTags = _searchChipPanel.Controls
            .OfType<Button>()
            .Select(static button => button.Tag as string)
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        var trailingCommaCommit = string.Equals(_searchText.Text, $"fixture, {normalizedPromptTag}", StringComparison.Ordinal) &&
            trailingCommaCommitTags.Count == 2 &&
            trailingCommaCommitTags.Any(static tag => string.Equals(tag, "fixture", StringComparison.OrdinalIgnoreCase)) &&
            trailingCommaCommitTags.Any(tag => string.Equals(tag, normalizedPromptTag, StringComparison.OrdinalIgnoreCase));
        _searchText.Text = $"{normalizedPromptTag}, {normalizedPromptTag.ToUpperInvariant()}, fixture";
        var enterArgs = new KeyEventArgs(Keys.Enter);
        HandleSearchTextKeyDown(enterArgs);
        var enterCommitTags = _searchChipPanel.Controls
            .OfType<Button>()
            .Select(static button => button.Tag as string)
            .Where(static tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        var enterCommit = enterArgs.Handled &&
            enterArgs.SuppressKeyPress &&
            string.Equals(_searchText.Text, $"{normalizedPromptTag}, fixture", StringComparison.Ordinal) &&
            enterCommitTags.Count == 2 &&
            enterCommitTags.Any(tag => string.Equals(tag, normalizedPromptTag, StringComparison.OrdinalIgnoreCase)) &&
            enterCommitTags.Any(static tag => string.Equals(tag, "fixture", StringComparison.OrdinalIgnoreCase));
        var searchChipCommit = trailingCommaCommit && enterCommit;
        Require(searchChipCommit, "search chip commit normalization failed");
        _clearSearchButton.PerformClick();
        Require(_searchText.Text.Length == 0 && _visibleImages.Count == initialVisible, "prompt tag search reset failed");
        var searchSuggestion = SelectSearchSuggestion(normalizedPromptTag) &&
            AddSelectedSearchSuggestion() &&
            string.Equals(_searchText.Text, normalizedPromptTag, StringComparison.OrdinalIgnoreCase) &&
            _visibleImages.Any(image => string.Equals(image.AbsolutePath, metadataTarget.AbsolutePath, StringComparison.OrdinalIgnoreCase));
        Require(searchSuggestion, "search suggestion add failed");
        _clearSearchButton.PerformClick();
        Require(_searchText.Text.Length == 0 && _visibleImages.Count == initialVisible, "search suggestion reset failed");
        SelectImage(metadataTarget.AbsolutePath);
        await LoadSelectedPreviewAsync();

        var detailReport = RunDetailModalSmoke();
        Require(detailReport.ModalOpened, "detail modal did not load image");
        Require(detailReport.Navigation, "detail modal navigation failed");
        Require(detailReport.Zoom, "detail modal zoom failed");
        Require(detailReport.Reset, "detail modal reset failed");
        Require(detailReport.Pan, "detail modal pan failed");
        Require(detailReport.Flip, "detail modal flip failed");
        Require(detailReport.Favorite, "detail modal favorite control failed");
        Require(detailReport.OpenExternal, "detail modal open-external target failed");
        Require(detailReport.MetadataDisplay, "detail modal metadata display failed");
        Require(detailReport.PromptTags, "detail modal prompt tags failed");
        var metadataDisplay = metadataPreview && detailReport.MetadataDisplay;

        var settingsSnapshot = BuildNativeSettingsSnapshot();
        var originalKeyBindingsJson = settingsSnapshot.KeyBindingsJson;
        var keybindingInputs = ExtractKeyBindingTextMap(originalKeyBindingsJson);
        foreach (var fallback in ExtractKeyBindingTextMap(NativeImageStore.DefaultKeyBindingsJson))
        {
            keybindingInputs.TryAdd(fallback.Key, fallback.Value);
        }

        keybindingInputs["next"] = "Shift+Right";
        keybindingInputs["detailZoomIn"] = "Shift+F2";
        var customKeyBindingsJson = "";
        var keybindingRecorder = settingsSnapshot.KeyBindingMode.Contains("editable", StringComparison.OrdinalIgnoreCase)
            && settingsSnapshot.KeyBindingsJson.Contains("openDetail", StringComparison.OrdinalIgnoreCase)
            && settingsSnapshot.KeyBindingsJson.Contains("detailZoomIn", StringComparison.OrdinalIgnoreCase)
            && settingsSnapshot.KeyBindingMode.Contains("detailPan deferred", StringComparison.OrdinalIgnoreCase)
            && TryBuildKeyBindingsJson(originalKeyBindingsJson, keybindingInputs, out customKeyBindingsJson, out _);
        Require(keybindingRecorder, "keybinding recorder validation failed");

        var duplicateInputs = new Dictionary<string, string>(keybindingInputs, StringComparer.OrdinalIgnoreCase)
        {
            ["previous"] = "Shift+Right",
        };
        var detailDuplicateInputs = new Dictionary<string, string>(keybindingInputs, StringComparer.OrdinalIgnoreCase)
        {
            ["detailZoomOut"] = "Shift+F2",
        };
        var keybindingConflictRejected =
            !TryBuildKeyBindingsJson(originalKeyBindingsJson, duplicateInputs, out _, out _) &&
            !TryBuildKeyBindingsJson(originalKeyBindingsJson, detailDuplicateInputs, out _, out _);
        Require(keybindingConflictRejected, "keybinding duplicate conflict was not rejected");

        SelectOffset(1);
        await LoadSelectedPreviewAsync();
        var selectedAfterButtonNavigation = GetSelectedIndex();
        var keyboardMessage = Message.Create(Handle, 0, IntPtr.Zero, IntPtr.Zero);
        var keyboardHandled = ProcessCmdKey(ref keyboardMessage, Keys.Left);
        await LoadSelectedPreviewAsync();
        var keyboardNavigation = keyboardHandled && selectedAfterButtonNavigation == 1 && GetSelectedIndex() == 0;
        Require(keyboardNavigation, "keyboard previous navigation failed");

        var favoriteBefore = (int)_favoriteLevel.Value;
        keyboardMessage = Message.Create(Handle, 0, IntPtr.Zero, IntPtr.Zero);
        var favoriteHandled = ProcessCmdKey(ref keyboardMessage, Keys.Control | Keys.Down);
        var favoriteAfter = (int)_favoriteLevel.Value;
        var keyboardFavorite = favoriteHandled && favoriteBefore > favoriteAfter;
        Require(keyboardFavorite, "keyboard favorite shortcut failed");

        keyboardMessage = Message.Create(Handle, 0, IntPtr.Zero, IntPtr.Zero);
        ApplyViewMode("details");
        var gridHandled = ProcessCmdKey(ref keyboardMessage, Keys.Control | Keys.G);
        var gridToggle = gridHandled && _list.View == View.LargeIcon;
        Require(gridToggle, "keyboard grid toggle failed");
        ApplyViewMode("details");

        _store.SaveSetting("keybindings_json", customKeyBindingsJson);
        RefreshKeyBindings();
        SelectImage(_visibleImages[0].AbsolutePath);
        ActiveControl = _list;
        keyboardMessage = Message.Create(Handle, 0, IntPtr.Zero, IntPtr.Zero);
        var customNextHandled = ProcessCmdKey(ref keyboardMessage, Keys.Shift | Keys.Right);
        var keybindingRuntime = customNextHandled && GetSelectedIndex() == 1;
        using var customDetail = CreateDetailModal(GetSelectedIndex());
        customDetail.Show(this);
        Application.DoEvents();
        var detailKeybindingRuntime = customDetail.RunCustomKeyBindingSmoke(Keys.Shift | Keys.F2);
        customDetail.Close();
        Application.DoEvents();
        _store.SaveSetting("keybindings_json", originalKeyBindingsJson);
        RefreshKeyBindings();
        Require(keybindingRuntime, "custom keybinding runtime dispatch failed");
        Require(detailKeybindingRuntime, "custom detail keybinding runtime dispatch failed");

        _favoritesOnly.Checked = false;
        SelectFavoriteFilter("all");
        _searchText.Text = searchQuery;
        ApplyFilter();
        var searchMatches = _visibleImages.Count;
        Require(searchMatches > 0, "search produced no fixture matches");
        _clearSearchButton.PerformClick();
        var clearSearch = _searchText.Text.Length == 0 && _visibleImages.Count == initialVisible;
        Require(clearSearch, "clear search control failed");

        _searchText.Text = searchQuery;
        ApplyFilter();
        _favoritesOnly.Checked = true;
        ApplyFilter();
        var favoriteMatches = _visibleImages.Count;
        Require(favoriteMatches > 0, "favorites filter produced no matches");

        _favoritesOnly.Checked = false;
        _searchText.Text = "";
        var smokeFavoriteLevel = _allImages.Where(static item => item.FavoriteLevel > 0).Select(static item => item.FavoriteLevel).DefaultIfEmpty(0).Max();
        Require(smokeFavoriteLevel > 0, "fixture has no favorite level for filter smoke");
        SelectFavoriteFilter(smokeFavoriteLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ApplyFilter();
        var favoriteLevelFilter = _visibleImages.Count > 0 && _visibleImages.All(item => item.FavoriteLevel == smokeFavoriteLevel);
        Require(favoriteLevelFilter, "favorite level filter failed");
        SelectFavoriteFilter("unrated");
        ApplyFilter();
        var unratedFilter = _visibleImages.Count > 0 && _visibleImages.All(static item => item.FavoriteLevel == 0);
        Require(unratedFilter, "unrated filter failed");
        var favoriteFilterCounts = _favoriteFilter.Items
            .Cast<FavoriteFilterOption>()
            .Any(item =>
                string.Equals(item.Key, smokeFavoriteLevel.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) &&
                item.Label.Contains($"({_allImages.Count(image => image.FavoriteLevel == smokeFavoriteLevel):n0})", StringComparison.Ordinal));
        Require(favoriteFilterCounts, "favorite filter count label failed");
        SelectFavoriteFilter("all");
        _enhancedOnly.Checked = true;
        ApplyFilter();
        var enhancedSources = NativeEnhancementState.LoadSucceededSourceIds(_projectRoot);
        var enhancedOnlyFilter = _visibleImages.Count > 0
            && _visibleImages.Count < initialVisible
            && _visibleImages.All(image => enhancedSources.Contains(image.AbsolutePath));
        Require(enhancedOnlyFilter, "enhanced-only filter failed");
        _enhancedOnly.Checked = false;
        _searchText.Text = "__native_ui_no_results__";
        ApplyFilter();
        var noResultsState = _visibleImages.Count == 0 && _statusLabel.Text.Contains("Showing 0", StringComparison.OrdinalIgnoreCase);
        Require(noResultsState, "no-results state failed");

        _folderText.Text = Path.Combine(folder, "__missing__");
        await ScanCurrentFolderAsync();
        var folderErrorState = _statusLabel.Text.Contains("Folder not found", StringComparison.OrdinalIgnoreCase);
        Require(folderErrorState, "missing-folder error state failed");

        _folderText.Text = folder;
        _searchText.Text = "";
        _favoritesOnly.Checked = false;
        _enhancedOnly.Checked = false;
        ApplyFilter();

        var afterEnhancementState = EnhancementStateFingerprint();
        Require(beforeEnhancementState == afterEnhancementState, "enhancement state changed during native UI smoke");
        return new NativeUiSmokeReport(
            Folder: folder,
            ScannedImages: scannedImages,
            InitialVisible: initialVisible,
            PreviewLoaded: previewLoaded,
            PreviewTabs: previewTabs,
            PreviewTabPin: previewTabPin,
            PreviewTabClose: previewTabClose,
            PreviewTabRestore: previewTabRestore,
            HoverQuickPreview: hoverQuickPreview,
            NavigationButtons: navigationButtons,
            KeyboardNavigation: keyboardNavigation,
            KeyboardFavorite: keyboardFavorite,
            GridToggle: gridToggle,
            FolderBuckets: folderBuckets,
            FolderHideAll: folderHideAll,
            FolderSortMode: folderSortMode,
            SortName: sortName,
            RandomReshuffle: randomReshuffle,
            ThumbnailSize: thumbnailSize,
            GalleryWheelZoom: galleryWheelZoom,
            GalleryKeyboardZoom: galleryKeyboardZoom,
            DisplayModes: displayModes,
            DisplayModeVisuals: displayModeVisuals,
            CompactGridImageSize: FormatSize(compactGridImageSize),
            PosterGridImageSize: FormatSize(posterGridImageSize),
            AspectModes: aspectModes,
            PreviewToggle: previewToggle,
            DetailsToggle: detailsToggle,
            PreviewSplitter: previewSplitter,
            SelectedCount: selectedCount,
            GalleryStateRestore: galleryStateRestore,
            MultiSelection: multiSelection,
            BulkFavoriteSet: bulkFavoriteSet,
            BulkFavoriteClear: bulkFavoriteClear,
            BulkOpenFiles: bulkOpenFiles,
            BulkOpenFolders: bulkOpenFolders,
            BackgroundClear: backgroundClear,
            FavoriteFilterCounts: favoriteFilterCounts,
            FavoriteLevelFilter: favoriteLevelFilter,
            UnratedFilter: unratedFilter,
            EnhancedOnlyFilter: enhancedOnlyFilter,
            ClearSearch: clearSearch,
            FolderShowSelected: folderShowSelected,
            FolderHideSelected: folderHideSelected,
            FolderRangeSelection: folderRangeHide && folderRangeShow,
            FolderKeyboardRangeSelection: folderKeyboardRangeSelection,
            FolderClearSelection: folderClearSelection,
            DetailModal: detailReport.ModalOpened,
            DetailNavigation: detailReport.Navigation,
            DetailZoom: detailReport.Zoom,
            DetailReset: detailReport.Reset,
            DetailPan: detailReport.Pan,
            DetailFlip: detailReport.Flip,
            DetailFavorite: detailReport.Favorite,
            DetailOpenExternal: detailReport.OpenExternal,
            MetadataDisplay: metadataDisplay,
            MetadataCopy: metadataCopy,
            PromptTagAction: promptTagAction,
            SearchChips: searchChips,
            SearchChipCommit: searchChipCommit,
            SearchSuggestion: searchSuggestion,
            KeybindingRecorder: keybindingRecorder,
            KeybindingConflictRejected: keybindingConflictRejected,
            KeybindingRuntime: keybindingRuntime,
            DetailKeybindingRuntime: detailKeybindingRuntime,
            SearchMatches: searchMatches,
            FavoriteMatches: favoriteMatches,
            NoResultsState: noResultsState,
            FolderErrorState: folderErrorState,
            Albums: albums,
            AlbumImages: albumImages,
            BrowserStateKeys: browserStateKeys,
            SettingsImported: settingsImported,
            EnhancementStateUnchanged: beforeEnhancementState == afterEnhancementState);
    }

    private async Task<NativeUiScreenshotReport> RunUiScreenshotScenarioAsync(string folder, string outputPath, string searchQuery)
    {
        var beforeEnhancementState = EnhancementStateFingerprint();

        _folderText.Text = folder;
        _store.SaveSetting("hidden_folder_buckets", "");
        _searchText.Text = "";
        _favoritesOnly.Checked = false;
        _enhancedOnly.Checked = false;
        SelectFavoriteFilter("all");
        ApplyManualDateRange(null, null);
        ImportState();

        await ScanCurrentFolderAsync();
        Require(_allImages.Count > 0, "scan produced no images");
        Require(_visibleImages.Count > 0, "scan produced no visible images");

        ApplyViewMode("details");
        ApplySortMode("Modified");
        ApplyFilter();
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            _searchText.Text = searchQuery;
            ApplyFilter();
            Require(_visibleImages.Count > 0, "screenshot search produced no visible images");
        }

        var screenshotTarget = _visibleImages[0];
        SelectImage(screenshotTarget.AbsolutePath);
        Application.DoEvents();
        var previewLoaded = await WaitForPreviewAsync(screenshotTarget.Filename, timeoutMs: 5000);
        if (!previewLoaded)
        {
            await LoadSelectedPreviewAsync();
            previewLoaded = await WaitForPreviewAsync(screenshotTarget.Filename, timeoutMs: 5000);
        }

        if (!previewLoaded)
        {
            var previous = _preview.Image;
            _preview.Image = LoadImageCopy(screenshotTarget.AbsolutePath);
            previous?.Dispose();
            _previewLabel.Text = FormatPreviewDetails(screenshotTarget, FormatDimensions(screenshotTarget));
            UpdateSelectionActions();
            previewLoaded = true;
        }

        Require(previewLoaded, "preview did not load for screenshot");

        _searchText.Focus();
        ActiveControl = _searchText;
        Refresh();
        Update();
        Application.DoEvents();

        var textFitWarnings = CountTextFitWarnings(this);
        var overlapWarnings = CountSiblingOverlapWarnings(this);
        SaveScreenshot(outputPath);

        var afterEnhancementState = EnhancementStateFingerprint();
        Require(beforeEnhancementState == afterEnhancementState, "enhancement state changed during native UI screenshot capture");

        return new NativeUiScreenshotReport(
            Folder: folder,
            OutputPath: outputPath,
            Width: Width,
            Height: Height,
            ScannedImages: _allImages.Count,
            VisibleImages: _visibleImages.Count,
            PreviewLoaded: previewLoaded,
            TextFitWarnings: textFitWarnings,
            OverlapWarnings: overlapWarnings,
            FocusControl: ActiveControl?.GetType().Name ?? "",
            EnhancementStateUnchanged: beforeEnhancementState == afterEnhancementState);
    }

    private async Task<NativeEnhancedFilterSmokeReport> RunEnhancedFilterSmokeScenarioAsync(string folder)
    {
        var beforeEnhancementState = EnhancementStateFingerprint();

        _folderText.Text = folder;
        _store.SaveSetting("hidden_folder_buckets", "");
        _searchText.Text = "";
        _favoritesOnly.Checked = false;
        _enhancedOnly.Checked = false;
        SelectFavoriteFilter("all");
        ApplyManualDateRange(null, null);
        ImportState();

        await ScanCurrentFolderAsync();
        Require(_allImages.Count > 0, "scan produced no images");
        Require(_visibleImages.Count > 0, "scan produced no visible images");
        var totalImages = _allImages.Count;
        var initialVisible = _visibleImages.Count;
        var enhancedSources = NativeEnhancementState.LoadSucceededSourceIds(_projectRoot);
        Require(enhancedSources.Count > 0, "fixture has no succeeded enhancement source ids");

        _enhancedOnly.Checked = true;
        ApplyFilter();
        var enhancedMatches = _visibleImages.Count;
        var enhancedOnlyFilter = enhancedMatches > 0
            && enhancedMatches < initialVisible
            && _visibleImages.All(image => enhancedSources.Contains(image.AbsolutePath));
        Require(enhancedOnlyFilter, "enhanced-only filter did not restrict visible images to succeeded jobs");

        _searchText.Text = "fixture";
        ApplyFilter();
        var enhancedSearchMatches = _visibleImages.Count;
        var enhancedSearchFilter = enhancedSearchMatches == enhancedMatches
            && _visibleImages.All(image => enhancedSources.Contains(image.AbsolutePath));
        Require(enhancedSearchFilter, "enhanced-only filter did not compose with search");

        _favoritesOnly.Checked = true;
        ApplyFilter();
        var enhancedFavoriteMatches = _visibleImages.Count;
        var enhancedFavoriteFilter = enhancedFavoriteMatches > 0
            && _visibleImages.All(image => image.FavoriteLevel > 0 && enhancedSources.Contains(image.AbsolutePath));
        Require(enhancedFavoriteFilter, "enhanced-only filter did not compose with favorites");

        _favoritesOnly.Checked = false;
        _searchText.Text = "";
        _enhancedOnly.Checked = false;
        ApplyFilter();
        var clearMatches = _visibleImages.Count;
        var clearFilter = clearMatches == initialVisible;
        Require(clearFilter, "clearing enhanced-only did not restore visible images");

        var enhancedFilterPersisted = string.Equals(_store.GetSetting("enhanced_only_filter", ""), "0", StringComparison.OrdinalIgnoreCase);
        Require(enhancedFilterPersisted, "enhanced-only filter setting did not persist cleared state");

        var afterEnhancementState = EnhancementStateFingerprint();
        Require(beforeEnhancementState == afterEnhancementState, "enhancement state changed during enhanced-only filter smoke");

        return new NativeEnhancedFilterSmokeReport(
            Folder: folder,
            TotalImages: totalImages,
            InitialVisible: initialVisible,
            EnhancedSources: enhancedSources.Count,
            EnhancedMatches: enhancedMatches,
            EnhancedSearchMatches: enhancedSearchMatches,
            EnhancedFavoriteMatches: enhancedFavoriteMatches,
            ClearMatches: clearMatches,
            EnhancedOnlyFilter: enhancedOnlyFilter,
            EnhancedSearchFilter: enhancedSearchFilter,
            EnhancedFavoriteFilter: enhancedFavoriteFilter,
            ClearFilter: clearFilter,
            EnhancedFilterPersisted: enhancedFilterPersisted,
            EnhancementStateUnchanged: beforeEnhancementState == afterEnhancementState);
    }

    private async Task<NativeLargeScrollSmokeReport> RunLargeScrollSmokeScenarioAsync(string folder)
    {
        const int minimumLargeFixtureImages = 200;
        var beforeEnhancementState = EnhancementStateFingerprint();

        _folderText.Text = folder;
        _store.SaveSetting("hidden_folder_buckets", "");
        _searchText.Text = "";
        _favoritesOnly.Checked = false;
        SelectFavoriteFilter("all");
        ApplyManualDateRange(null, null);
        ApplyViewMode("details");
        ApplySortMode("Name");

        await ScanCurrentFolderAsync(forceFullRefresh: true);
        Require(_allImages.Count >= minimumLargeFixtureImages, $"large fixture needs at least {minimumLargeFixtureImages} images");
        Require(_visibleImages.Count == _allImages.Count, "large fixture filter did not show all images");
        Require(_list.VirtualMode, "large fixture list is not virtualized");
        Require(_list.VirtualListSize == _visibleImages.Count, "virtual list size does not match visible images");

        var targetIndex = Math.Clamp(_visibleImages.Count * 3 / 4, 150, _visibleImages.Count - 1);
        var target = _visibleImages[targetIndex];
        SelectImage(target.AbsolutePath);
        Application.DoEvents();
        var selectedBeforeRestore = GetSelectedIndex() == targetIndex;
        var visibleBeforeRestore = IsListIndexVisible(targetIndex);
        var topIndexBeforeRestore = TryGetTopItemIndex();
        var storedIndex = _store.GetSetting("last_visible_index", "");
        var storedPath = _store.GetSetting("last_selected_image", "");
        var statePersisted = string.Equals(storedPath, target.AbsolutePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(storedIndex, targetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
        Require(selectedBeforeRestore, "large fixture target selection failed");
        Require(statePersisted, "large fixture gallery state was not persisted");

        ClearImageSelection();
        ApplyFilter();
        Application.DoEvents();
        var restoredIndex = GetSelectedIndex();
        var restored = restoredIndex == targetIndex
            && string.Equals(GetSelectedImage()?.AbsolutePath, target.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        var ensureVisible = IsListIndexVisible(restoredIndex);
        var topIndexAfterRestore = TryGetTopItemIndex();
        Require(restored, "large fixture gallery state restore failed");
        Require(ensureVisible, "large fixture restored selection was not visible");

        ApplyViewMode("grid");
        ApplyThumbnailSize(GalleryThumbnailDefault);
        SaveThumbnailSize(GalleryThumbnailDefault);
        var selectedZoomHandled = HandleGalleryWheelZoom(120);
        var selectedZoomAnchored = selectedZoomHandled &&
            GetSelectedIndex() == targetIndex &&
            IsListIndexVisible(targetIndex);
        Require(selectedZoomAnchored, "large fixture selected gallery zoom anchor failed");

        ClearImageSelection();
        Application.DoEvents();
        var centerAnchorIndex = GetGalleryViewportAnchorIndex();
        Require(centerAnchorIndex >= 0, "large fixture center gallery zoom anchor was not found");
        var centerZoomHandled = HandleGalleryKeyboardZoom(Keys.Control | Keys.Oemplus);
        var centerZoomAnchored = centerZoomHandled && IsListIndexVisible(centerAnchorIndex);
        Require(centerZoomAnchored, "large fixture center gallery zoom anchor failed");

        var afterEnhancementState = EnhancementStateFingerprint();
        Require(beforeEnhancementState == afterEnhancementState, "enhancement state changed during native large-scroll smoke");

        return new NativeLargeScrollSmokeReport(
            Folder: folder,
            TotalImages: _allImages.Count,
            InitialVisible: _visibleImages.Count,
            TargetIndex: targetIndex,
            RestoredIndex: restoredIndex,
            TopIndexBeforeRestore: topIndexBeforeRestore,
            TopIndexAfterRestore: topIndexAfterRestore,
            VirtualMode: _list.VirtualMode,
            VirtualListSize: _list.VirtualListSize,
            StatePersisted: statePersisted,
            RestoreSelected: restored,
            EnsureVisible: ensureVisible,
            VisibleBeforeRestore: visibleBeforeRestore,
            GalleryZoomSelectedAnchor: selectedZoomAnchored,
            GalleryZoomCenterAnchor: centerZoomAnchored,
            EnhancementStateUnchanged: beforeEnhancementState == afterEnhancementState);
    }

    private async Task<NativeDateFilterSmokeReport> RunDateFilterSmokeScenarioAsync(string folder)
    {
        var beforeEnhancementState = EnhancementStateFingerprint();
        var today = DateTime.Today;

        _folderText.Text = folder;
        _store.SaveSetting("hidden_folder_buckets", "");
        _searchText.Text = "";
        _favoritesOnly.Checked = false;
        SelectFavoriteFilter("all");
        ApplyManualDateRange(null, null);
        ApplySortMode("Created");

        await ScanCurrentFolderAsync(forceFullRefresh: true);
        Require(_allImages.Count >= 4, "date filter smoke needs at least four fixture images");
        var totalImages = _allImages.Count;
        var initialVisible = _visibleImages.Count;

        SelectDateFilter("today");
        ApplyFilter();
        var todayMatches = _visibleImages.Count;
        var todayFilter = todayMatches == 1 && _visibleImages.All(image => IsImageWithinDateFilter(image, "today", today));
        Require(todayFilter, "today date filter failed");
        var dateFilterPersisted = string.Equals(_store.GetSetting("date_filter", ""), "today", StringComparison.OrdinalIgnoreCase);
        Require(dateFilterPersisted, "date filter setting did not persist");

        SelectDateFilter("7d");
        ApplyFilter();
        var last7Matches = _visibleImages.Count;
        var last7Filter = last7Matches == 2 && _visibleImages.All(image => IsImageWithinDateFilter(image, "7d", today));
        Require(last7Filter, "7d date filter failed");

        SelectDateFilter("30d");
        ApplyFilter();
        var last30Matches = _visibleImages.Count;
        var last30Filter = last30Matches == 3 && _visibleImages.All(image => IsImageWithinDateFilter(image, "30d", today));
        Require(last30Filter, "30d date filter failed");

        SelectDateFilter("year");
        ApplyFilter();
        var thisYearMatches = _visibleImages.Count;
        var thisYearFilter = thisYearMatches > 0 &&
            _visibleImages.All(image => IsImageWithinDateFilter(image, "year", today)) &&
            _visibleImages.All(image => !image.Filename.Contains("last-year", StringComparison.OrdinalIgnoreCase));
        Require(thisYearFilter, "this-year date filter failed");

        ApplyManualDateRange(today.AddDays(-20), today.AddDays(-6));
        var manualRangeMatches = _visibleImages.Count;
        var manualRangeFilter = manualRangeMatches == 2 &&
            _visibleImages.All(image => IsImageWithinDateRange(image, today.AddDays(-20), today.AddDays(-6)));
        Require(manualRangeFilter, "manual date range filter failed");
        var manualRangePersisted =
            string.Equals(_store.GetSetting("date_filter", ""), "custom", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_store.GetSetting("date_from", ""), FormatDateInput(today.AddDays(-20)), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_store.GetSetting("date_to", ""), FormatDateInput(today.AddDays(-6)), StringComparison.OrdinalIgnoreCase);
        Require(manualRangePersisted, "manual date range setting did not persist");

        _searchText.Text = "last";
        ApplyFilter();
        var manualSearchMatches = _visibleImages.Count;
        var manualSearchFilter = manualSearchMatches == 2 &&
            _visibleImages.All(image => image.Filename.Contains("last", StringComparison.OrdinalIgnoreCase)) &&
            _visibleImages.All(image => IsImageWithinDateRange(image, today.AddDays(-20), today.AddDays(-6)));
        Require(manualSearchFilter, "manual date range did not compose with search");

        _searchText.Text = "";
        ApplyManualDateRange(today.AddDays(-20), today.AddDays(-6));
        var favoriteTarget = _visibleImages.First(image => image.Filename.Contains("last-7d", StringComparison.OrdinalIgnoreCase));
        _store.SetFavoriteLevel(favoriteTarget.AbsolutePath, 5);
        _favorites = _store.LoadFavorites();
        _allImages = ReapplyFavorites(_allImages);
        RefreshFavoriteFilterOptions("5");
        SelectFavoriteFilter("5");
        ApplyManualDateRange(today.AddDays(-20), today.AddDays(-6));
        var manualFavoriteMatches = _visibleImages.Count;
        var manualFavoriteFilter = manualFavoriteMatches == 1 &&
            _visibleImages.All(image => image.FavoriteLevel == 5) &&
            _visibleImages.All(image => IsImageWithinDateRange(image, today.AddDays(-20), today.AddDays(-6)));
        Require(manualFavoriteFilter, "manual date range did not compose with favorite filter");
        SelectFavoriteFilter("all");

        ApplyManualDateRange(today.AddDays(-6), null);
        var manualFromOnlyMatches = _visibleImages.Count;
        var manualFromOnlyFilter = manualFromOnlyMatches == 2 &&
            _visibleImages.All(image => IsImageWithinDateRange(image, today.AddDays(-6), null));
        Require(manualFromOnlyFilter, "manual date-from-only filter failed");

        ApplyManualDateRange(null, today.AddDays(-20));
        var manualToOnlyMatches = _visibleImages.Count;
        var manualToOnlyFilter = manualToOnlyMatches == 2 &&
            _visibleImages.All(image => IsImageWithinDateRange(image, null, today.AddDays(-20)));
        Require(manualToOnlyFilter, "manual date-to-only filter failed");

        SelectDateFilter("all");
        ApplyFilter();
        var clearMatches = _visibleImages.Count;
        var clearFilter = clearMatches == totalImages;
        Require(clearFilter, "clear date filter failed");

        var afterEnhancementState = EnhancementStateFingerprint();
        Require(beforeEnhancementState == afterEnhancementState, "enhancement state changed during date filter smoke");

        return new NativeDateFilterSmokeReport(
            Folder: folder,
            TotalImages: totalImages,
            InitialVisible: initialVisible,
            TodayMatches: todayMatches,
            Last7Matches: last7Matches,
            Last30Matches: last30Matches,
            ThisYearMatches: thisYearMatches,
            ManualRangeMatches: manualRangeMatches,
            ManualFromOnlyMatches: manualFromOnlyMatches,
            ManualToOnlyMatches: manualToOnlyMatches,
            ManualSearchMatches: manualSearchMatches,
            ManualFavoriteMatches: manualFavoriteMatches,
            ClearMatches: clearMatches,
            TodayFilter: todayFilter,
            Last7Filter: last7Filter,
            Last30Filter: last30Filter,
            ThisYearFilter: thisYearFilter,
            ManualRangeFilter: manualRangeFilter,
            ManualFromOnlyFilter: manualFromOnlyFilter,
            ManualToOnlyFilter: manualToOnlyFilter,
            ManualSearchFilter: manualSearchFilter,
            ManualFavoriteFilter: manualFavoriteFilter,
            ManualRangePersisted: manualRangePersisted,
            ClearFilter: clearFilter,
            DateFilterPersisted: dateFilterPersisted,
            EnhancementStateUnchanged: beforeEnhancementState == afterEnhancementState);
    }

    private async Task<NativeDateSectionSmokeReport> RunDateSectionSmokeScenarioAsync(string folder)
    {
        var beforeEnhancementState = EnhancementStateFingerprint();
        var today = DateTime.Today;

        _folderText.Text = folder;
        _store.SaveSetting("hidden_folder_buckets", "");
        _searchText.Text = "";
        _favoritesOnly.Checked = false;
        SelectFavoriteFilter("all");
        ApplyManualDateRange(null, null);
        ApplyViewMode("details");
        ApplySortMode("Created");

        await ScanCurrentFolderAsync(forceFullRefresh: true);
        Require(_allImages.Count >= 4, "date section smoke needs at least four fixture images");
        Require(_visibleImages.Count == _allImages.Count, "date section smoke did not show all fixture images");

        var totalImages = _allImages.Count;
        var initialVisible = _visibleImages.Count;
        var expectedGroupKeys = _visibleImages
            .Select(FormatDateSectionKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var expectedGroupLabels = expectedGroupKeys
            .Select(key => FormatDateSectionLabel(_visibleImages.First(image => string.Equals(FormatDateSectionKey(image), key, StringComparison.Ordinal)).CreatedAtUtc))
            .ToList();
        var expectedFirstHeader = FormatDateSectionLabel(_visibleImages[0].CreatedAtUtc);
        var firstItem = CreateListItem(_visibleImages[0]);
        var headerLabels = _dateSectionHeadersByPath.Values.ToList();
        var firstHeader = _dateSectionHeadersByPath.TryGetValue(_visibleImages[0].AbsolutePath, out var firstHeaderValue)
            ? firstHeaderValue
            : "";
        var showGroups = _dateSectionHeadersByPath.Count > 0;
        var headerGroups = _dateSectionHeadersByPath.Count;
        var firstItemGrouped = string.Equals(firstHeader, expectedFirstHeader, StringComparison.Ordinal) &&
            firstItem.Text.Contains(expectedFirstHeader, StringComparison.Ordinal);
        var createdSortOrder = _visibleImages.SequenceEqual(
            _visibleImages
                .OrderByDescending(static item => item.CreatedAtUtc)
                .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase));
        var headersMatchDates = headerGroups == expectedGroupKeys.Count &&
            expectedGroupLabels.All(label => headerLabels.Contains(label, StringComparer.Ordinal));

        Require(showGroups, "date section headers are not enabled for Created list view");
        Require(
            headersMatchDates,
            $"date section headers do not match visible image dates expected={string.Join(",", expectedGroupLabels)} actual={string.Join(",", headerLabels)} keys={string.Join(",", expectedGroupKeys)} headers={headerGroups} sort={_sortMode.SelectedItem}");
        Require(firstItemGrouped, "first date section item does not show its header label");
        Require(createdSortOrder, "date section smoke is not sorted by Created descending");

        SelectDateFilter("today");
        ApplyFilter();
        var filteredGroups = _dateSectionHeadersByPath.Count;
        var filteredGroupHeaders = _dateSectionHeadersByPath.Values.ToList();
        var todaySingleGroup = _visibleImages.Count == 1 &&
            filteredGroups == 1 &&
            _visibleImages.All(image => filteredGroupHeaders.Contains(FormatDateSectionLabel(image.CreatedAtUtc), StringComparer.Ordinal));
        Require(todaySingleGroup, "date section headers did not follow the Today filter");

        ApplyViewMode("grid");
        var gridFilteredGroups = _dateSectionHeadersByPath.Count;
        var gridFirstItem = CreateListItem(_visibleImages[0]);
        var gridFirstHeader = _dateSectionHeadersByPath.TryGetValue(_visibleImages[0].AbsolutePath, out var gridFirstHeaderValue)
            ? gridFirstHeaderValue
            : "";
        var gridFirstItemGrouped = string.Equals(gridFirstHeader, FormatDateSectionLabel(_visibleImages[0].CreatedAtUtc), StringComparison.Ordinal) &&
            gridFirstItem.Text.Contains(gridFirstHeader, StringComparison.Ordinal);
        var gridTodaySingleGroup = _visibleImages.Count == 1 &&
            gridFilteredGroups == 1 &&
            gridFirstItemGrouped;
        Require(gridTodaySingleGroup, "grid date section headers did not follow the Today filter");

        SelectDateFilter("all");
        ApplyFilter();
        var gridHeaderGroups = _dateSectionHeadersByPath.Count;
        var gridHeaderLabels = _dateSectionHeadersByPath.Values.ToList();
        var gridHeadersMatchDates = gridHeaderGroups == expectedGroupKeys.Count &&
            expectedGroupLabels.All(label => gridHeaderLabels.Contains(label, StringComparer.Ordinal));
        Require(gridHeadersMatchDates, "grid date section headers do not match visible image dates");

        ApplyViewMode("details");
        ApplyManualDateRange(today.AddDays(-20), today.AddDays(-6));
        var manualRangeHeaderGroups = _dateSectionHeadersByPath.Count;
        var manualRangeExpectedLabels = _visibleImages
            .Select(image => FormatDateSectionLabel(image.CreatedAtUtc))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var manualRangeListHeaders = _visibleImages.Count == 2 &&
            manualRangeHeaderGroups == manualRangeExpectedLabels.Count &&
            manualRangeExpectedLabels.All(label => _dateSectionHeadersByPath.Values.Contains(label, StringComparer.Ordinal));
        Require(manualRangeListHeaders, "manual date range did not compose with list date headers");

        ApplyViewMode("grid");
        var manualRangeGridHeaderGroups = _dateSectionHeadersByPath.Count;
        var manualRangeGridHeaders = _visibleImages.Count == 2 &&
            manualRangeGridHeaderGroups == manualRangeExpectedLabels.Count &&
            manualRangeExpectedLabels.All(label => _dateSectionHeadersByPath.Values.Contains(label, StringComparer.Ordinal));
        Require(manualRangeGridHeaders, "manual date range did not compose with grid date headers");

        SelectDateFilter("all");
        ApplyFilter();
        ApplyViewMode("details");
        ApplyFilter();

        var afterEnhancementState = EnhancementStateFingerprint();
        Require(beforeEnhancementState == afterEnhancementState, "enhancement state changed during date section smoke");

        return new NativeDateSectionSmokeReport(
            Folder: folder,
            TotalImages: totalImages,
            InitialVisible: initialVisible,
            HeaderGroups: headerGroups,
            FirstHeader: firstHeader,
            ShowDateHeaders: showGroups,
            FirstItemGrouped: firstItemGrouped,
            CreatedSortOrder: createdSortOrder,
            FilteredGroups: filteredGroups,
            TodaySingleGroup: todaySingleGroup,
            GridHeaderGroups: gridHeaderGroups,
            GridFirstItemGrouped: gridFirstItemGrouped,
            GridTodaySingleGroup: gridTodaySingleGroup,
            ManualRangeHeaderGroups: manualRangeHeaderGroups,
            ManualRangeListHeaders: manualRangeListHeaders,
            ManualRangeGridHeaderGroups: manualRangeGridHeaderGroups,
            ManualRangeGridHeaders: manualRangeGridHeaders,
            EnhancementStateUnchanged: beforeEnhancementState == afterEnhancementState);
    }

    private async Task<NativeFolderSetSmokeReport> RunFolderSetSmokeScenarioAsync(IReadOnlyList<string> roots, string searchQuery)
    {
        var beforeEnhancementState = EnhancementStateFingerprint();
        var normalizedRoots = NativeFolderSet.NormalizeDistinct(roots);
        _folderText.Text = NativeFolderSet.FormatForDisplay(normalizedRoots);
        _store.SaveSetting("hidden_folder_buckets", "");
        _searchText.Text = "";
        _favoritesOnly.Checked = false;
        SelectFavoriteFilter("all");
        ApplyManualDateRange(null, null);

        await ScanCurrentFolderAsync();
        Require(_currentRoots.Count >= 2, "folder set did not retain multiple roots");
        Require(_allImages.Count > 0, "folder set scan produced no images");
        var initialRootCount = _currentRoots.Count;
        var initialImages = _allImages.Count;
        var folderBuckets = _folderBuckets.Items.Count;
        Require(folderBuckets >= initialRootCount, "folder set did not build per-root folder buckets");
        var persistedRoots = _store.LoadRecentFolderSet();
        var recentSetPersisted = initialRootCount == persistedRoots.Count &&
            _currentRoots.All(root => persistedRoots.Contains(root, StringComparer.OrdinalIgnoreCase));
        Require(recentSetPersisted, "folder set was not persisted as recent set");

        _searchText.Text = searchQuery;
        ApplyFilter();
        var searchMatches = _visibleImages.Count;
        Require(searchMatches > 0, "folder set search produced no matches");

        var watcherRoots = _folderWatcher.WatchedRootCount == initialRootCount;
        Require(watcherRoots, "folder watcher did not track all roots");

        var rootToRemove = _currentRoots[0];
        RemoveFolderRoot(rootToRemove);
        var removeFolder = _currentRoots.Count == initialRootCount - 1 &&
            _allImages.All(image => !NativeFolderSet.IsPathUnderRoot(image.AbsolutePath, rootToRemove));
        Require(removeFolder, "remove-folder did not remove the selected root from the active set");

        _folderText.Text = "";
        _currentRoots = [];
        _currentFolder = "";
        _allImages = [];
        _visibleImages = [];
        _list.VirtualListSize = 0;
        await OpenRecentFolderSetAsync();
        var openRecentSet = _currentRoots.Count == initialRootCount - 1 &&
            _allImages.Count > 0 &&
            _currentRoots.All(root => !string.Equals(root, rootToRemove, StringComparison.OrdinalIgnoreCase));
        Require(openRecentSet, "open recent folder set did not restore the persisted set");

        var refreshRoot = _currentRoots[^1];
        var probePath = Path.Combine(refreshRoot, "m11-folder-set-refresh-probe.png");
        var beforeRefreshCount = _allImages.Count;
        try
        {
            WriteSmokeProbePng(probePath, Color.MediumPurple);
            await RefreshCurrentFolderSetAsync();
            var manualRefreshAdded = _allImages.Count == beforeRefreshCount + 1 &&
                _allImages.Any(image => string.Equals(image.AbsolutePath, Path.GetFullPath(probePath), StringComparison.OrdinalIgnoreCase));
            Require(manualRefreshAdded, "manual refresh did not add the probe image");

            File.Delete(probePath);
            await RefreshCurrentFolderSetAsync();
            var manualRefreshRemoved = _allImages.Count == beforeRefreshCount &&
                _allImages.All(image => !string.Equals(image.AbsolutePath, Path.GetFullPath(probePath), StringComparison.OrdinalIgnoreCase));
            Require(manualRefreshRemoved, "manual refresh did not remove the deleted probe image");

            var afterEnhancementState = EnhancementStateFingerprint();
            Require(beforeEnhancementState == afterEnhancementState, "enhancement state changed during folder-set smoke");
            return new NativeFolderSetSmokeReport(
                Roots: initialRootCount,
                RemovedRoots: 1,
                FolderBuckets: folderBuckets,
                ImagesBeforeRemove: initialImages,
                ImagesAfterRemove: _allImages.Count,
                SearchMatches: searchMatches,
                RecentSetPersisted: recentSetPersisted,
                RemoveFolder: removeFolder,
                OpenRecentSet: openRecentSet,
                ManualRefreshAdded: manualRefreshAdded,
                ManualRefreshRemoved: manualRefreshRemoved,
                WatcherRoots: watcherRoots,
                EnhancementStateUnchanged: beforeEnhancementState == afterEnhancementState);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
            catch
            {
                // Smoke cleanup is best-effort inside the ignored fixture tree.
            }
        }
    }

    private void ApplyStateSummary(NativeImportReport? report = null)
    {
        report ??= _store.ImportProjectState();
        var warningText = report.WarningCount > 0 ? $" warn {report.WarningCount:n0}" : "";
        _stateLabel.Text = $"db {report.ImageCount:n0} fav {report.FavoriteCount:n0} seen {report.SeenImageCount:n0} alb {report.AlbumCount:n0}/{report.AlbumImageCount:n0} pvu {report.BrowserStateKeyCount:n0}{warningText}";
    }

    private void ImportState()
    {
        var report = _store.ImportProjectState();
        _favorites = _store.LoadFavorites();
        _allImages = ReapplyFavorites(_allImages);
        RefreshSearchTagSuggestions();
        BuildFolderBuckets();
        ApplyFilter();
        ApplyStateSummary(report);
        var warningText = report.WarningCount > 0
            ? $" Import warnings: {report.RecoverySummary}"
            : "";
        SetStatus($"Imported state: {report.FavoriteCount:n0} favorites, {report.SeenImageCount:n0} seen images, {report.AlbumCount:n0} albums, {report.AlbumImageCount:n0} album images, {report.BrowserStateKeyCount:n0} pvu keys, db {report.ImageCount:n0} images.{warningText}");
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select an image folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(NativeFolderSet.Parse(_folderText.Text).FirstOrDefault() ?? "") ? NativeFolderSet.Parse(_folderText.Text)[0] : _projectRoot,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderText.Text = dialog.SelectedPath;
            _store.SaveRecentFolderSet([dialog.SelectedPath]);
        }
    }

    private void AddFolderToSet()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Add an image folder to the current folder set",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_projectRoot) ? _projectRoot : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var roots = CurrentFolderSetRoots();
        roots.Add(dialog.SelectedPath);
        roots = NativeFolderSet.NormalizeDistinct(roots);
        _folderText.Text = NativeFolderSet.FormatForDisplay(roots);
        _store.SaveRecentFolderSet(roots);
        SetStatus($"Added folder to set: {dialog.SelectedPath}");
    }

    private void RemoveSelectedFolderFromSet()
    {
        var roots = CurrentFolderSetRoots();
        if (roots.Count == 0)
        {
            SetStatus("No folder set to remove from.");
            return;
        }

        var selected = GetSelectedImage();
        var rootToRemove = selected is not null
            ? NativeFolderSet.FindRootForPath(selected.AbsolutePath, roots)
            : roots[^1];
        if (string.IsNullOrWhiteSpace(rootToRemove))
        {
            SetStatus("Could not resolve selected image root.");
            return;
        }

        RemoveFolderRoot(rootToRemove);
    }

    private void RemoveFolderRoot(string rootToRemove)
    {
        var roots = CurrentFolderSetRoots()
            .Where(root => !string.Equals(root, rootToRemove, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _currentRoots = roots;
        _currentFolder = roots.FirstOrDefault() ?? "";
        _folderText.Text = NativeFolderSet.FormatForDisplay(roots);
        if (roots.Count > 0)
        {
            _store.SaveRecentFolderSet(roots);
            _allImages = _store.LoadImagesForRoots(roots);
            RefreshSearchTagSuggestions();
            _folderWatcher.Watch(roots);
        }
        else
        {
            _allImages = [];
            RefreshSearchTagSuggestions();
            _folderWatcher.Watch([]);
        }

        BuildFolderBuckets();
        ApplyFilter();
        ApplyStateSummary();
        SetStatus(roots.Count > 0
            ? $"Removed folder from set: {rootToRemove}"
            : $"Removed final folder from set: {rootToRemove}");
    }

    private async Task OpenRecentFolderSetAsync()
    {
        var roots = _store.LoadRecentFolderSet().Where(Directory.Exists).ToList();
        if (roots.Count == 0)
        {
            SetStatus("No recent folder set found.");
            return;
        }

        _folderText.Text = NativeFolderSet.FormatForDisplay(roots);
        await ScanCurrentFolderAsync();
    }

    private async Task RefreshCurrentFolderSetAsync()
    {
        var roots = CurrentFolderSetRoots();
        if (roots.Count == 0)
        {
            SetStatus("No folder set to refresh.");
            return;
        }

        _folderText.Text = NativeFolderSet.FormatForDisplay(roots);
        await ScanCurrentFolderAsync(forceFullRefresh: false);
    }

    private List<string> CurrentFolderSetRoots()
    {
        return _currentRoots.Count > 0
            ? NativeFolderSet.NormalizeDistinct(_currentRoots)
            : NativeFolderSet.Parse(_folderText.Text);
    }

    private async Task ScanCurrentFolderAsync()
    {
        await ScanCurrentFolderAsync(forceFullRefresh: false);
    }

    private async Task ScanCurrentFolderAsync(bool forceFullRefresh)
    {
        var roots = NativeFolderSet.Parse(_folderText.Text);
        if (roots.Count == 0)
        {
            SetStatus("No folder set selected.");
            return;
        }

        var missing = roots.FirstOrDefault(root => !Directory.Exists(root));
        if (!string.IsNullOrWhiteSpace(missing))
        {
            SetStatus($"Folder not found: {missing}");
            return;
        }

        _currentRoots = roots;
        _currentFolder = roots[0];
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        _scanButton.Enabled = false;
        _cancelButton.Enabled = true;
        _importButton.Enabled = false;
        _previewRing.Clear();
        ClearPreview("Scanning...");

        var stopwatch = Stopwatch.StartNew();
        var progress = new Progress<NativeScanProgress>(item =>
        {
            SetStatus($"Scanning {item.Count:n0} images - {item.CurrentFolder}");
        });

        try
        {
            _favorites = _store.LoadFavorites();
            var totalChanged = 0;
            var totalRemoved = 0;
            var totalUnchanged = 0;
            foreach (var root in roots)
            {
                var existing = forceFullRefresh
                    ? new Dictionary<string, NativeImageRecord>(StringComparer.OrdinalIgnoreCase)
                    : _store.LoadImagesForRoot(root).ToDictionary(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase);
                if (existing.Count > 0)
                {
                    var incremental = await NativeIncrementalScanner.ScanAsync(root, existing, _favorites, progress, _scanCancellation.Token);
                    _store.ApplyIncrementalScan(root, incremental, stopwatch.Elapsed, fullRescan: false);
                    totalChanged += incremental.AddedOrUpdated.Count;
                    totalRemoved += incremental.RemovedPaths.Count;
                    totalUnchanged += incremental.UnchangedCount;
                }
                else
                {
                    var scanned = await NativeImageScanner.ScanAsync(root, _favorites, progress, _scanCancellation.Token);
                    _store.SaveScanResult(root, scanned, stopwatch.Elapsed);
                    totalChanged += scanned.Count;
                }
            }

            stopwatch.Stop();
            _store.SaveRecentFolderSet(roots);
            _allImages = _store.LoadImagesForRoots(roots);
            RefreshSearchTagSuggestions();
            BuildFolderBuckets();
            ApplyFilter();
            ApplyStateSummary();
            _folderWatcher.Watch(roots);
            SetStatus(
                roots.Count == 1
                    ? $"Scan complete: {_allImages.Count:n0} images, {totalChanged:n0} changed, {totalRemoved:n0} removed, {totalUnchanged:n0} unchanged in {stopwatch.Elapsed.TotalSeconds:n1}s. Saved to {_store.DatabasePath}"
                    : $"Folder set scan: {roots.Count:n0} roots, {_allImages.Count:n0} images, {totalChanged:n0} changed, {totalRemoved:n0} removed, {totalUnchanged:n0} unchanged in {stopwatch.Elapsed.TotalSeconds:n1}s.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Scan cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Scan failed: {ex.Message}");
        }
        finally
        {
            _scanButton.Enabled = true;
            _cancelButton.Enabled = false;
            _importButton.Enabled = true;
        }
    }

    private async Task RunIncrementalRescanAsync(string folder)
    {
        if (!_currentRoots.Contains(folder, StringComparer.OrdinalIgnoreCase) || !_scanButton.Enabled)
        {
            return;
        }

        _folderText.Text = NativeFolderSet.FormatForDisplay(_currentRoots);
        await ScanCurrentFolderAsync(forceFullRefresh: false);
    }

    private void OnFolderChangesDetected(object? sender, string folder)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => _ = RunIncrementalRescanAsync(folder));
            return;
        }

        _ = RunIncrementalRescanAsync(folder);
    }

    private void ApplyFilter()
    {
        var preferredSelectionPath = GetSelectedImage()?.AbsolutePath;
        var query = _searchText.Text.Trim();
        if (_currentRoots.Count > 0 && _currentRoots.All(Directory.Exists) && _allImages.Count > 0)
        {
            _visibleImages = ApplySort(ApplyFolderBucketFilter(ApplyEnhancedFilter(ApplyDateFilter(ApplyFavoriteFilter(_store.SearchImagesIndexed(
                _currentRoots,
                query,
                ShouldPrefilterFavoritesForIndexedSearch(),
                limit: 100_000)))))).ToList();
            RefreshVisibleList();
            RestoreGalleryStateSelection(preferredSelectionPath);
            UpdateSelectionActions();
            SetStatus($"Showing {_visibleImages.Count:n0} / {_allImages.Count:n0} images (indexed search).");
            return;
        }

        IEnumerable<NativeImageRecord> source = _allImages;
        source = ApplyFavoriteFilter(source);
        source = ApplyDateFilter(source);
        source = ApplyEnhancedFilter(source);

        if (query.Length > 0)
        {
            source = source.Where(item =>
                item.Filename.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Folder.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.AbsolutePath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _visibleImages = ApplySort(ApplyFolderBucketFilter(source)).ToList();
        RefreshVisibleList();
        RestoreGalleryStateSelection(preferredSelectionPath);
        UpdateSelectionActions();
        SetStatus($"Showing {_visibleImages.Count:n0} / {_allImages.Count:n0} images.");
    }

    private void RefreshVisibleList()
    {
        RefreshDateSectionHeaders();
        _list.VirtualListSize = _visibleImages.Count;
        _list.Invalidate();
    }

    private bool ShouldPrefilterFavoritesForIndexedSearch()
    {
        var key = CurrentFavoriteFilterKey();
        return _favoritesOnly.Checked || string.Equals(key, "favorites", StringComparison.OrdinalIgnoreCase) || IsFavoriteLevelKey(key);
    }

    private IEnumerable<NativeImageRecord> ApplyFavoriteFilter(IEnumerable<NativeImageRecord> images)
    {
        var key = CurrentFavoriteFilterKey();
        if (_favoritesOnly.Checked || string.Equals(key, "favorites", StringComparison.OrdinalIgnoreCase))
        {
            images = images.Where(static item => item.FavoriteLevel > 0);
        }

        if (string.Equals(key, "unrated", StringComparison.OrdinalIgnoreCase))
        {
            return images.Where(static item => item.FavoriteLevel == 0);
        }

        return int.TryParse(key, out var level) && level is >= 1 and <= 5
            ? images.Where(item => item.FavoriteLevel == level)
            : images;
    }

    private IEnumerable<NativeImageRecord> ApplyDateFilter(IEnumerable<NativeImageRecord> images)
    {
        var (from, to) = CurrentDateRange();
        if (!from.HasValue && !to.HasValue)
        {
            return images;
        }

        return images.Where(image => IsImageWithinDateRange(image, from, to));
    }

    private IEnumerable<NativeImageRecord> ApplyEnhancedFilter(IEnumerable<NativeImageRecord> images)
    {
        if (!_enhancedOnly.Checked)
        {
            return images;
        }

        var enhancedSources = NativeEnhancementState.LoadSucceededSourceIds(_projectRoot);
        return images.Where(image => enhancedSources.Contains(image.AbsolutePath));
    }

    private static bool IsImageWithinDateFilter(NativeImageRecord image, string key, DateTime today)
    {
        var (from, to) = DateRangeForFilterKey(key, today);
        return IsImageWithinDateRange(image, from, to);
    }

    private static bool IsImageWithinDateRange(NativeImageRecord image, DateTime? from, DateTime? to)
    {
        var imageDate = image.CreatedAtUtc.ToLocalTime().Date;
        if (from.HasValue && imageDate < from.Value.Date)
        {
            return false;
        }

        if (to.HasValue && imageDate > to.Value.Date)
        {
            return false;
        }

        return true;
    }

    private static (DateTime? From, DateTime? To) DateRangeForFilterKey(string key, DateTime today)
    {
        if (string.Equals(key, "today", StringComparison.OrdinalIgnoreCase))
        {
            return (today, today);
        }

        if (string.Equals(key, "7d", StringComparison.OrdinalIgnoreCase))
        {
            return (today.AddDays(-6), today);
        }

        if (string.Equals(key, "30d", StringComparison.OrdinalIgnoreCase))
        {
            return (today.AddDays(-29), today);
        }

        if (string.Equals(key, "year", StringComparison.OrdinalIgnoreCase))
        {
            return (new DateTime(today.Year, 1, 1), today);
        }

        return (null, null);
    }

    private string CurrentDateFilterKey()
    {
        return _dateFilter.SelectedItem is DateFilterOption option ? option.Key : "all";
    }

    private void SelectDateFilter(string key)
    {
        var match = _dateFilter.Items
            .Cast<DateFilterOption>()
            .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            var changed = !Equals(_dateFilter.SelectedItem, match);
            _dateFilter.SelectedItem = match;
            if (!changed)
            {
                ApplyDateRangeForSelectedFilter();
            }
        }
    }

    private void RefreshDateFilterOptions(string? preferredKey = null)
    {
        var selectedKey = preferredKey ?? CurrentDateFilterKey();
        var options = new List<DateFilterOption>
        {
            new("all", "All dates"),
            new("today", "Today"),
            new("7d", "7d"),
            new("30d", "30d"),
            new("year", "This year"),
            new("custom", "Custom range"),
        };

        _updatingDateFilter = true;
        try
        {
            _dateFilter.Items.Clear();
            foreach (var option in options)
            {
                _dateFilter.Items.Add(option);
            }

            _dateFilter.SelectedItem = options.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase)) ?? options[0];
        }
        finally
        {
            _updatingDateFilter = false;
        }
    }

    private void ConfigureDateRangePicker(DateTimePicker picker)
    {
        picker.Format = DateTimePickerFormat.Custom;
        picker.CustomFormat = "yyyy-MM-dd";
        picker.ShowCheckBox = true;
        picker.Width = 158;
        picker.Value = DateTime.Today;
        picker.Checked = false;
        picker.ValueChanged += (_, _) => ManualDateRangeChanged();
    }

    private void ManualDateRangeChanged()
    {
        if (_updatingDateRange)
        {
            return;
        }

        var (from, to) = CurrentDateRange();
        SelectDateFilterSilently(from.HasValue || to.HasValue ? "custom" : "all");
        ApplyFilter();
        SaveViewState();
    }

    private (DateTime? From, DateTime? To) CurrentDateRange()
    {
        return (
            _dateFromPicker.Checked ? _dateFromPicker.Value.Date : null,
            _dateToPicker.Checked ? _dateToPicker.Value.Date : null);
    }

    private void ApplyDateRangeForSelectedFilter()
    {
        var key = CurrentDateFilterKey();
        if (string.Equals(key, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ApplyDateRange(DateRangeForFilterKey(key, DateTime.Today));
    }

    private void ApplyDateRange((DateTime? From, DateTime? To) range)
    {
        _updatingDateRange = true;
        try
        {
            ApplyDatePickerValue(_dateFromPicker, range.From);
            ApplyDatePickerValue(_dateToPicker, range.To);
        }
        finally
        {
            _updatingDateRange = false;
        }
    }

    private void ApplyManualDateRange(DateTime? from, DateTime? to)
    {
        ApplyDateRange((from, to));
        SelectDateFilterSilently(from.HasValue || to.HasValue ? "custom" : "all");
        ApplyFilter();
        SaveViewState();
    }

    private static void ApplyDatePickerValue(DateTimePicker picker, DateTime? value)
    {
        picker.Checked = value.HasValue;
        if (value.HasValue)
        {
            picker.Value = value.Value.Date;
        }
    }

    private void ApplyStoredDateRange(string fromValue, string toValue, string dateFilterKey)
    {
        var hasFrom = TryParseDateInput(fromValue, out var from);
        var hasTo = TryParseDateInput(toValue, out var to);
        if (hasFrom || hasTo)
        {
            ApplyDateRange((hasFrom ? from : null, hasTo ? to : null));
            if (string.Equals(dateFilterKey, "all", StringComparison.OrdinalIgnoreCase))
            {
                SelectDateFilterSilently("custom");
            }

            return;
        }

        ApplyDateRange(DateRangeForFilterKey(dateFilterKey, DateTime.Today));
    }

    private static bool TryParseDateInput(string value, out DateTime date)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out date);
    }

    private void SelectDateFilterSilently(string key)
    {
        var match = _dateFilter.Items
            .Cast<DateFilterOption>()
            .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        _updatingDateFilter = true;
        try
        {
            _dateFilter.SelectedItem = match;
        }
        finally
        {
            _updatingDateFilter = false;
        }
    }

    private string CurrentFavoriteFilterKey()
    {
        return _favoriteFilter.SelectedItem is FavoriteFilterOption option ? option.Key : "all";
    }

    private void SelectFavoriteFilter(string key)
    {
        var match = _favoriteFilter.Items
            .Cast<FavoriteFilterOption>()
            .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            _favoriteFilter.SelectedItem = match;
        }
    }

    private void RefreshFavoriteFilterOptions(string? preferredKey = null)
    {
        var selectedKey = preferredKey ?? CurrentFavoriteFilterKey();
        var levelCounts = Enumerable.Range(1, 5)
            .ToDictionary(level => level, level => _allImages.Count(item => item.FavoriteLevel == level));
        var options = new List<FavoriteFilterOption>
        {
            new("all", $"All ({_allImages.Count:n0})"),
            new("favorites", $"Favorites ({_allImages.Count(static item => item.FavoriteLevel > 0):n0})"),
            new("unrated", $"Unrated ({_allImages.Count(static item => item.FavoriteLevel == 0):n0})"),
        };
        options.AddRange(Enumerable.Range(1, 5).Select(level => new FavoriteFilterOption(level.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Level {level} ({levelCounts[level]:n0})")));

        _updatingFavoriteFilter = true;
        try
        {
            _favoriteFilter.Items.Clear();
            foreach (var option in options)
            {
                _favoriteFilter.Items.Add(option);
            }

            _favoriteFilter.SelectedItem = options.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase)) ?? options[0];
        }
        finally
        {
            _updatingFavoriteFilter = false;
        }
    }

    private void ClearSearchFilter()
    {
        if (_searchText.Text.Length == 0)
        {
            ApplyFilter();
            return;
        }

        _searchText.Clear();
        SaveViewState();
    }

    private static bool IsFavoriteLevelKey(string key)
    {
        return int.TryParse(key, out var level) && level is >= 1 and <= 5;
    }

    private ListViewItem CreateListItem(NativeImageRecord image)
    {
        var favorite = image.FavoriteLevel > 0 ? image.FavoriteLevel.ToString() : "";
        var seenPrefix = image.IsSeen ? "" : "NEW ";
        var favoritePrefix = favorite.Length > 0 ? $"★{favorite} " : "";
        var dateSectionHeader = GetDateSectionHeader(image);
        var filenameText = $"{seenPrefix}{favoritePrefix}{image.Filename}";
        if (dateSectionHeader.Length > 0)
        {
            filenameText = $"{dateSectionHeader}  {filenameText}";
        }

        var item = new ListViewItem(filenameText)
        {
            ImageIndex = 0,
        };
        item.SubItems.Add(favorite);
        item.SubItems.Add(FormatDimensions(image));
        item.SubItems.Add(image.Folder);
        item.SubItems.Add(image.ModifiedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
        item.SubItems.Add(FormatBytes(image.SizeBytes));
        return item;
    }

    private void RefreshDateSectionHeaders()
    {
        _dateSectionHeadersByPath.Clear();

        if (!ShouldShowDateSectionHeaders())
        {
            return;
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var image in _visibleImages)
        {
            var key = FormatDateSectionKey(image);
            if (!seenKeys.Add(key))
            {
                continue;
            }

            _dateSectionHeadersByPath[image.AbsolutePath] = FormatDateSectionLabel(image.CreatedAtUtc);
        }
    }

    private bool ShouldShowDateSectionHeaders()
    {
        return _visibleImages.Count > 0 &&
            (_list.View == View.Details || _list.View == View.LargeIcon) &&
            string.Equals(_sortMode.SelectedItem?.ToString(), "Created", StringComparison.OrdinalIgnoreCase);
    }

    private string GetDateSectionHeader(NativeImageRecord image)
    {
        return _dateSectionHeadersByPath.TryGetValue(image.AbsolutePath, out var header)
            ? header
            : "";
    }

    private static string FormatDateSectionKey(NativeImageRecord image)
    {
        return image.CreatedAtUtc.ToLocalTime().Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatDateSectionLabel(DateTime createdAtUtc)
    {
        var localDate = createdAtUtc.ToLocalTime().Date;
        return $"{localDate.Month}\u6708{localDate.Day}\u65e5";
    }

    private async Task LoadSelectedPreviewAsync()
    {
        _hoverPreviewPath = "";
        if (_list.SelectedIndices.Count == 0)
        {
            ClearPreview("Select an image.");
            return;
        }

        var index = _list.SelectedIndices[0];
        if (index < 0 || index >= _visibleImages.Count)
        {
            return;
        }

        var image = _visibleImages[index];
        var version = Interlocked.Increment(ref _previewVersion);
        var dimensionHint = FormatDimensions(image);
        _previewLabel.Text = dimensionHint.Length > 0
            ? $"Loading {image.Filename} ({dimensionHint})"
            : $"Loading {image.Filename}";

        try
        {
            Image loaded;
            if (_previewRing.TryGet(image.AbsolutePath, out var cached) && cached is not null)
            {
                loaded = new Bitmap(cached);
            }
            else
            {
                var decoded = await _cacheScheduler.ScheduleAsync(
                    NativeCacheJobKind.PreviewDecode,
                    image.AbsolutePath,
                    _ => LoadImageCopy(image.AbsolutePath));
                _previewRing.Store(image.AbsolutePath, decoded);
                loaded = new Bitmap(decoded);
            }

            if (version != Interlocked.Read(ref _previewVersion))
            {
                loaded.Dispose();
                return;
            }

            var previous = _preview.Image;
            _preview.Image = loaded;
            previous?.Dispose();

            _previewLabel.Text = FormatPreviewDetails(image, dimensionHint);
            EnsurePreviewTab(image);
            UpdateSelectionActions();
            WarmNeighborPreviews(index);
        }
        catch (Exception ex)
        {
            if (version == Interlocked.Read(ref _previewVersion))
            {
                ClearPreview($"Preview failed: {ex.Message}");
            }
        }
    }

    private void EnsurePreviewTab(NativeImageRecord image)
    {
        var page = FindPreviewTabPage(image.AbsolutePath);
        var state = new PreviewTabState(image.AbsolutePath, image.Filename, IsPinnedPreviewTab(page));

        _updatingPreviewTabs = true;
        try
        {
            if (page is null)
            {
                page = new TabPage(FormatPreviewTabText(state))
                {
                    Tag = state,
                    ToolTipText = image.AbsolutePath,
                };
                _previewTabs.TabPages.Add(page);
            }
            else
            {
                page.Tag = state;
                page.Text = FormatPreviewTabText(state);
                page.ToolTipText = image.AbsolutePath;
            }

            _previewTabs.SelectedTab = page;
        }
        finally
        {
            _updatingPreviewTabs = false;
        }

        UpdatePreviewTabActions();
    }

    private async Task ActivateSelectedPreviewTabAsync()
    {
        if (_updatingPreviewTabs)
        {
            return;
        }

        var state = ActivePreviewTabState();
        UpdatePreviewTabActions();
        if (state is null)
        {
            return;
        }

        if (!IsVisibleImagePath(state.Path))
        {
            SetStatus($"Preview tab outside current filter: {state.Filename}");
            return;
        }

        SelectImage(state.Path);
        await LoadSelectedPreviewAsync();
    }

    private bool ToggleActivePreviewTabPin()
    {
        var page = _previewTabs.SelectedTab;
        if (page?.Tag is not PreviewTabState state)
        {
            return false;
        }

        var updated = state with { IsPinned = !state.IsPinned };
        page.Tag = updated;
        page.Text = FormatPreviewTabText(updated);
        page.ToolTipText = updated.Path;
        UpdatePreviewTabActions();
        SetStatus(updated.IsPinned
            ? $"Pinned preview: {updated.Filename}"
            : $"Unpinned preview: {updated.Filename}");
        return true;
    }

    private bool CloseActivePreviewTab()
    {
        var page = _previewTabs.SelectedTab;
        if (page?.Tag is not PreviewTabState state)
        {
            return false;
        }

        var selectedIndex = _previewTabs.SelectedIndex;
        _updatingPreviewTabs = true;
        try
        {
            RememberClosedPreviewTab(state);
            _previewTabs.TabPages.Remove(page);
            page.Dispose();
            if (_previewTabs.TabPages.Count > 0)
            {
                _previewTabs.SelectedIndex = Math.Clamp(selectedIndex, 0, _previewTabs.TabPages.Count - 1);
            }
        }
        finally
        {
            _updatingPreviewTabs = false;
        }

        UpdatePreviewTabActions();
        if (ActivePreviewTabState() is { } nextState)
        {
            SelectImage(nextState.Path);
        }
        else
        {
            ClearImageSelection();
        }

        SetStatus($"Closed preview tab: {state.Filename}");
        return true;
    }

    private async Task<bool> RestoreLastClosedPreviewTabAsync()
    {
        while (_closedPreviewTabs.Count > 0)
        {
            var lastIndex = _closedPreviewTabs.Count - 1;
            var state = _closedPreviewTabs[lastIndex];
            _closedPreviewTabs.RemoveAt(lastIndex);

            var page = FindPreviewTabPage(state.Path);
            if (page is null && !IsVisibleImagePath(state.Path))
            {
                SetStatus($"Closed preview tab outside current filter: {state.Filename}");
                UpdatePreviewTabActions();
                return false;
            }

            _updatingPreviewTabs = true;
            try
            {
                if (page is null)
                {
                    page = new TabPage(FormatPreviewTabText(state))
                    {
                        Tag = state,
                        ToolTipText = state.Path,
                    };
                    _previewTabs.TabPages.Add(page);
                }
                else
                {
                    page.Tag = state;
                    page.Text = FormatPreviewTabText(state);
                    page.ToolTipText = state.Path;
                }

                _previewTabs.SelectedTab = page;
            }
            finally
            {
                _updatingPreviewTabs = false;
            }

            UpdatePreviewTabActions();
            SelectImage(state.Path);
            await LoadSelectedPreviewAsync();
            SetStatus($"Restored preview tab: {state.Filename}");
            return true;
        }

        UpdatePreviewTabActions();
        SetStatus("No closed preview tab to restore.");
        return false;
    }

    private void RememberClosedPreviewTab(PreviewTabState state)
    {
        _closedPreviewTabs.RemoveAll(item => string.Equals(item.Path, state.Path, StringComparison.OrdinalIgnoreCase));
        _closedPreviewTabs.Add(state);
        if (_closedPreviewTabs.Count > ClosedPreviewTabLimit)
        {
            _closedPreviewTabs.RemoveRange(0, _closedPreviewTabs.Count - ClosedPreviewTabLimit);
        }
    }

    private TabPage? FindPreviewTabPage(string absolutePath)
    {
        return _previewTabs.TabPages
            .Cast<TabPage>()
            .FirstOrDefault(page => page.Tag is PreviewTabState state &&
                string.Equals(state.Path, absolutePath, StringComparison.OrdinalIgnoreCase));
    }

    private PreviewTabState? ActivePreviewTabState()
    {
        return _previewTabs.SelectedTab?.Tag as PreviewTabState;
    }

    private bool IsPinnedPreviewTab(TabPage? page)
    {
        return page?.Tag is PreviewTabState { IsPinned: true };
    }

    private bool IsVisibleImagePath(string absolutePath)
    {
        return _visibleImages.Any(image => string.Equals(image.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdatePreviewTabActions()
    {
        var state = ActivePreviewTabState();
        _pinPreviewTabButton.Enabled = state is not null;
        _closePreviewTabButton.Enabled = state is not null;
        _restorePreviewTabButton.Enabled = _closedPreviewTabs.Count > 0;
        _pinPreviewTabButton.Text = state?.IsPinned == true ? "Unpin" : "Pin";
    }

    private static string FormatPreviewTabText(PreviewTabState state)
    {
        var text = state.IsPinned ? $"P {state.Filename}" : state.Filename;
        return text.Length <= 32 ? text : $"{text[..29]}...";
    }

    private async Task<bool> ShowHoverQuickPreviewAsync(int index)
    {
        if (index < 0 || index >= _visibleImages.Count)
        {
            return false;
        }

        var image = _visibleImages[index];
        if (string.Equals(_hoverPreviewPath, image.AbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        _hoverPreviewPath = image.AbsolutePath;
        var version = Interlocked.Increment(ref _previewVersion);
        var dimensionHint = FormatDimensions(image);
        _previewLabel.Text = dimensionHint.Length > 0
            ? $"Hover preview {image.Filename} ({dimensionHint})"
            : $"Hover preview {image.Filename}";

        try
        {
            Image loaded;
            if (_previewRing.TryGet(image.AbsolutePath, out var cached) && cached is not null)
            {
                loaded = new Bitmap(cached);
            }
            else
            {
                var decoded = await _cacheScheduler.ScheduleAsync(
                    NativeCacheJobKind.PreviewDecode,
                    image.AbsolutePath,
                    _ => LoadImageCopy(image.AbsolutePath));
                _previewRing.Store(image.AbsolutePath, decoded);
                loaded = new Bitmap(decoded);
            }

            if (version != Interlocked.Read(ref _previewVersion) ||
                !string.Equals(_hoverPreviewPath, image.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                loaded.Dispose();
                return false;
            }

            var previous = _preview.Image;
            _preview.Image = loaded;
            previous?.Dispose();
            _previewLabel.Text = $"Hover preview: {FormatPreviewDetails(image, dimensionHint)}";
            return true;
        }
        catch (Exception ex)
        {
            if (version == Interlocked.Read(ref _previewVersion) &&
                string.Equals(_hoverPreviewPath, image.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                _hoverPreviewPath = "";
                ClearPreview($"Hover preview failed: {ex.Message}");
            }

            return false;
        }
    }

    private async Task EndHoverQuickPreviewAsync()
    {
        if (_hoverPreviewPath.Length == 0)
        {
            return;
        }

        _hoverPreviewPath = "";
        if (GetSelectedIndex() >= 0)
        {
            await LoadSelectedPreviewAsync();
            return;
        }

        ClearPreview("Select an image.");
    }

    private async Task<bool> WaitForPreviewAsync(string expectedFilename, int timeoutMs = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 <= deadline)
        {
            if (_preview.Image is not null && _previewLabel.Text.Contains(expectedFilename, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            await Task.Delay(25);
            Application.DoEvents();
        }

        return _preview.Image is not null && _previewLabel.Text.Contains(expectedFilename, StringComparison.OrdinalIgnoreCase);
    }

    private void OpenSelectedFile()
    {
        TryOpenSelectedFiles(OpenExternalPath);
    }

    private bool CopySelectedMetadata(bool useSystemClipboard = true)
    {
        if (_list.SelectedIndices.Count != 1)
        {
            SetStatus("Select one image to copy PNG info.");
            return false;
        }

        var image = GetSelectedImage();
        if (image is null)
        {
            SetStatus("Select one image to copy PNG info.");
            return false;
        }

        var copyText = BuildMetadataCopyText(image);
        _lastMetadataCopyText = copyText;
        var copyKind = MetadataCopyKind(image);

        if (!useSystemClipboard)
        {
            SetStatus($"Prepared {copyKind} copy: {image.Filename}");
            return true;
        }

        try
        {
            Clipboard.SetText(copyText);
            SetStatus($"Copied {copyKind}: {image.Filename}");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Copy failed: {ex.Message}");
            return false;
        }
    }

    private bool AddPromptTagToSearch(string tag)
    {
        var normalized = NormalizePromptTag(tag);
        if (normalized.Length == 0)
        {
            return false;
        }

        var currentTags = ParseSearchTags(_searchText.Text);
        if (currentTags.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus($"Prompt tag already in search: {normalized}");
            return false;
        }

        currentTags.Add(normalized);
        _searchText.Text = string.Join(", ", currentTags);
        SetStatus($"Added prompt tag to search: {normalized}");
        return true;
    }

    private bool AddSelectedSearchSuggestion()
    {
        return _searchSuggestion.SelectedItem is SearchTagSuggestion suggestion && AddPromptTagToSearch(suggestion.Tag);
    }

    private void HandleSearchTextChanged()
    {
        if (_committingSearchText)
        {
            return;
        }

        if (_searchText.Text.EndsWith(",", StringComparison.Ordinal))
        {
            CommitSearchText();
            return;
        }

        RefreshSearchChipControls();
        ApplyFilter();
        SaveViewState();
    }

    private void HandleSearchTextKeyDown(KeyEventArgs args)
    {
        if (args.KeyCode != Keys.Enter)
        {
            return;
        }

        CommitSearchText();
        args.Handled = true;
        args.SuppressKeyPress = true;
    }

    private bool CommitSearchText()
    {
        var normalized = NormalizeSearchText(_searchText.Text);
        var changed = !string.Equals(_searchText.Text, normalized, StringComparison.Ordinal);

        if (changed)
        {
            _committingSearchText = true;
            try
            {
                _searchText.Text = normalized;
                _searchText.SelectionStart = _searchText.TextLength;
                _searchText.SelectionLength = 0;
            }
            finally
            {
                _committingSearchText = false;
            }
        }

        RefreshSearchChipControls();
        ApplyFilter();
        SaveViewState();

        if (changed)
        {
            SetStatus(normalized.Length == 0 ? "Cleared search tags." : $"Committed search tags: {normalized}");
        }

        return changed;
    }

    private void RemoveSearchTag(string tag)
    {
        var currentTags = ParseSearchTags(_searchText.Text);
        var nextTags = currentTags
            .Where(item => !string.Equals(item, tag, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nextTags.Count == currentTags.Count)
        {
            return;
        }

        _searchText.Text = string.Join(", ", nextTags);
        SetStatus($"Removed search tag: {tag}");
    }

    private void RefreshSearchTagSuggestions()
    {
        var counts = new Dictionary<string, SearchTagSuggestionAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in _allImages)
        {
            foreach (var rawTag in SplitPromptTags(image.Prompt))
            {
                var normalized = NormalizePromptTag(rawTag);
                if (normalized.Length < 2)
                {
                    continue;
                }

                if (counts.TryGetValue(normalized, out var current))
                {
                    counts[normalized] = current with { Count = current.Count + 1 };
                }
                else
                {
                    counts[normalized] = new SearchTagSuggestionAccumulator(normalized, 1);
                }
            }
        }

        _searchTagSuggestions = counts.Values
            .OrderByDescending(static item => item.Count)
            .ThenBy(static item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .Take(2000)
            .Select(static item => new SearchTagSuggestion(item.Tag, item.Count))
            .ToList();
        RefreshSearchSuggestionOptions();
    }

    private void RefreshSearchChipControls()
    {
        var tags = ParseSearchTags(_searchText.Text);
        _searchChipPanel.SuspendLayout();
        try
        {
            var oldControls = _searchChipPanel.Controls.Cast<Control>().ToArray();
            _searchChipPanel.Controls.Clear();
            foreach (var control in oldControls)
            {
                control.Dispose();
            }

            foreach (var tag in tags)
            {
                var button = new Button
                {
                    Text = $"{tag} x",
                    Tag = tag,
                    Height = 24,
                    Width = Math.Clamp(46 + tag.Length * 7, 76, 190),
                    Margin = new Padding(2, 1, 2, 1),
                    UseMnemonic = false,
                };
                button.Click += (_, _) => RemoveSearchTag(tag);
                _searchChipPanel.Controls.Add(button);
            }
        }
        finally
        {
            _searchChipPanel.ResumeLayout();
        }

        RefreshSearchSuggestionOptions();
    }

    private void RefreshSearchSuggestionOptions()
    {
        if (_updatingSearchSuggestions)
        {
            return;
        }

        _updatingSearchSuggestions = true;
        try
        {
            var selectedTag = (_searchSuggestion.SelectedItem as SearchTagSuggestion)?.Tag ?? "";
            var committedTags = ParseSearchTags(_searchText.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = _searchTagSuggestions
                .Where(item => !committedTags.Contains(item.Tag))
                .Take(200)
                .ToList();

            _searchSuggestion.BeginUpdate();
            _searchSuggestion.Items.Clear();
            foreach (var suggestion in candidates)
            {
                _searchSuggestion.Items.Add(suggestion);
            }

            _searchSuggestion.EndUpdate();

            if (_searchSuggestion.Items.Count == 0)
            {
                _searchSuggestion.SelectedIndex = -1;
            }
            else
            {
                var selectedIndex = 0;
                if (selectedTag.Length > 0)
                {
                    for (var index = 0; index < _searchSuggestion.Items.Count; index++)
                    {
                        if (_searchSuggestion.Items[index] is SearchTagSuggestion suggestion &&
                            string.Equals(suggestion.Tag, selectedTag, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = index;
                            break;
                        }
                    }
                }

                _searchSuggestion.SelectedIndex = selectedIndex;
            }

            _searchSuggestion.Enabled = _searchSuggestion.Items.Count > 0;
            _addSearchSuggestionButton.Enabled = _searchSuggestion.SelectedItem is SearchTagSuggestion;
        }
        finally
        {
            _updatingSearchSuggestions = false;
        }
    }

    private bool SelectSearchSuggestion(string tag)
    {
        for (var index = 0; index < _searchSuggestion.Items.Count; index++)
        {
            if (_searchSuggestion.Items[index] is SearchTagSuggestion suggestion &&
                string.Equals(suggestion.Tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                _searchSuggestion.SelectedIndex = index;
                return true;
            }
        }

        return false;
    }

    private bool TryOpenSelectedFiles(Action<string> openAction, bool updateStatus = true)
    {
        var selected = GetSelectedImages();
        if (selected.Count == 0)
        {
            if (updateStatus)
            {
                SetStatus("Select image(s) to open.");
            }

            return false;
        }

        return TryOpenBulkTargets(
            GetSelectedFileOpenTargets(selected),
            selected.Count,
            "file",
            "files",
            openAction,
            updateStatus);
    }

    private bool TryOpenSelectedFolders(Action<string> openAction, bool updateStatus = true)
    {
        var selected = GetSelectedImages();
        if (selected.Count == 0)
        {
            if (updateStatus)
            {
                SetStatus("Select image(s) to open folders.");
            }

            return false;
        }

        return TryOpenBulkTargets(
            GetSelectedFolderOpenTargets(selected),
            selected.Count,
            "folder",
            "folders",
            openAction,
            updateStatus);
    }

    private bool TryOpenBulkTargets(
        IReadOnlyList<string> targets,
        int selectedCount,
        string singularKind,
        string pluralKind,
        Action<string> openAction,
        bool updateStatus)
    {
        if (targets.Count == 0)
        {
            if (updateStatus)
            {
                SetStatus($"No existing selected {singularKind} target.");
            }

            return false;
        }

        if (targets.Count > BulkOpenExternalTargetLimit)
        {
            if (updateStatus)
            {
                SetStatus($"Select {BulkOpenExternalTargetLimit} or fewer {pluralKind} to open externally; selected {selectedCount:n0}, targets {targets.Count:n0}.");
            }

            return false;
        }

        try
        {
            foreach (var target in targets)
            {
                openAction(target);
            }

            if (updateStatus)
            {
                SetStatus(targets.Count == 1
                    ? $"Opened {singularKind}: {Path.GetFileName(targets[0])}"
                    : $"Opened {targets.Count:n0} {pluralKind}.");
            }

            return true;
        }
        catch (Exception ex)
        {
            if (updateStatus)
            {
                SetStatus($"Open {singularKind} failed: {ex.Message}");
            }

            return false;
        }
    }

    private static List<string> GetSelectedFileOpenTargets(IEnumerable<NativeImageRecord> images)
    {
        var targets = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in images)
        {
            if (File.Exists(image.AbsolutePath) && seen.Add(image.AbsolutePath))
            {
                targets.Add(image.AbsolutePath);
            }
        }

        return targets;
    }

    private static List<string> GetSelectedFolderOpenTargets(IEnumerable<NativeImageRecord> images)
    {
        var targets = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in images)
        {
            var folder = Path.GetDirectoryName(image.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) && seen.Add(folder))
            {
                targets.Add(folder);
            }
        }

        return targets;
    }

    private static void OpenExternalPath(string absolutePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = absolutePath,
            UseShellExecute = true,
        });
    }

    private void ShowDetailModal()
    {
        var index = GetSelectedIndex();
        if (index < 0)
        {
            return;
        }

        using var detail = CreateDetailModal(index);
        detail.ShowDialog(this);
        UpdateSelectionActions();
    }

    private DetailModalForm CreateDetailModal(int index)
    {
        return new DetailModalForm(
            _visibleImages,
            index,
            LoadKeyBindings(_store.GetSetting("keybindings_json", NativeImageStore.DefaultKeyBindingsJson), DetailKeyBindingActions),
            OpenExternalPath,
            SetFavoriteLevelForPath,
            AddPromptTagToSearch);
    }

    private DetailSmokeReport RunDetailModalSmoke()
    {
        var index = GetSelectedIndex();
        if (index < 0)
        {
            return new DetailSmokeReport(false, false, false, false, false, false, false, false, false, false);
        }

        using var detail = CreateDetailModal(index);
        detail.Show(this);
        Application.DoEvents();
        var report = detail.RunSmoke();
        detail.Close();
        Application.DoEvents();
        UpdateSelectionActions();
        return report;
    }

    private void OpenSelectedFolder()
    {
        var selected = GetSelectedImages();
        if (selected.Count == 0)
        {
            return;
        }

        if (selected.Count > 1)
        {
            TryOpenSelectedFolders(OpenExplorerFolder);
            return;
        }

        var image = selected[0];
        if (!File.Exists(image.AbsolutePath))
        {
            SetStatus("Selected file no longer exists.");
            return;
        }

        try
        {
            OpenExplorerSelectPath(image.AbsolutePath);
            SetStatus($"Opened folder for: {image.Filename}");
        }
        catch (Exception ex)
        {
            SetStatus($"Open folder failed: {ex.Message}");
        }
    }

    private static void OpenExplorerSelectPath(string absolutePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{absolutePath}\"",
            UseShellExecute = true,
        });
    }

    private static void OpenExplorerFolder(string folder)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folder}\"",
            UseShellExecute = true,
        });
    }

    private void DeleteSelectedImage()
    {
        var deletedIndex = GetSelectedIndex();
        var image = GetSelectedImage();
        if (image is null)
        {
            return;
        }

        try
        {
            FileSystem.DeleteFile(
                image.AbsolutePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException);
            _store.RemoveImage(image.AbsolutePath);
            _allImages.RemoveAll(item => string.Equals(item.AbsolutePath, image.AbsolutePath, StringComparison.OrdinalIgnoreCase));
            _visibleImages.RemoveAll(item => string.Equals(item.AbsolutePath, image.AbsolutePath, StringComparison.OrdinalIgnoreCase));
            _list.VirtualListSize = _visibleImages.Count;
            _list.Invalidate();
            ClearPreview("Deleted to Recycle Bin.");
            SelectNearestAfterDelete(deletedIndex);
            SetStatus($"Moved to Recycle Bin: {image.Filename}");
        }
        catch (Exception ex)
        {
            SetStatus($"Recycle failed; file was not hard-deleted: {ex.Message}");
        }
    }

    private void SetSelectedFavoriteLevel(int level)
    {
        var selectedPaths = GetSelectedImages()
            .Select(static item => item.AbsolutePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedPaths.Length == 0)
        {
            return;
        }

        SetFavoriteLevelForPaths(selectedPaths, level);
    }

    private void SetFavoriteLevelForPath(string absolutePath, int level)
    {
        SetFavoriteLevelForPaths([absolutePath], level);
    }

    private void SetFavoriteLevelForPaths(IReadOnlyCollection<string> absolutePaths, int level)
    {
        var clamped = Math.Clamp(level, 0, 5);
        var updatedPaths = new List<string>();
        string? firstUpdatedFilename = null;

        foreach (var absolutePath in absolutePaths)
        {
            var current = _visibleImages.FirstOrDefault(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase))
                ?? _allImages.FirstOrDefault(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
            if (current is null)
            {
                continue;
            }

            _store.SetFavoriteLevel(current.AbsolutePath, clamped);
            var updated = current with { FavoriteLevel = clamped };
            ReplaceImage(current.AbsolutePath, updated);
            updatedPaths.Add(updated.AbsolutePath);
            firstUpdatedFilename ??= updated.Filename;
        }

        if (updatedPaths.Count == 0)
        {
            return;
        }

        _favorites = _store.LoadFavorites();
        RefreshFavoriteFilterOptions();
        ApplyFilter();
        SelectImages(updatedPaths);
        SetStatus(updatedPaths.Count == 1
            ? $"Favorite level {clamped}: {firstUpdatedFilename}"
            : $"Favorite level {clamped}: {updatedPaths.Count:n0} images");
    }

    private void ClearPreview(string message)
    {
        _hoverPreviewPath = "";
        Interlocked.Increment(ref _previewVersion);
        var previous = _preview.Image;
        _preview.Image = null;
        previous?.Dispose();
        _previewLabel.Text = message;
        UpdatePreviewTabActions();
    }

    private void ClearImageSelection()
    {
        if (_list.SelectedIndices.Count == 0)
        {
            return;
        }

        _list.SelectedIndices.Clear();
        ClearPreview("Select an image.");
        UpdateSelectionActions();
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (TryDispatchKeyBinding(keyData))
        {
            return true;
        }

        switch (keyData)
        {
            case Keys.Right:
                SelectOffset(1);
                return true;
            case Keys.Left:
                SelectOffset(-1);
                return true;
            case Keys.Control | Keys.Up:
                SetSelectedFavoriteLevel(Math.Min(5, (int)_favoriteLevel.Value + 1));
                return true;
            case Keys.Control | Keys.Down:
                SetSelectedFavoriteLevel(Math.Max(0, (int)_favoriteLevel.Value - 1));
                return true;
            case Keys.Control | Keys.Oemplus:
            case Keys.Control | Keys.Shift | Keys.Oemplus:
            case Keys.Control | Keys.Add:
            case Keys.Control | Keys.OemMinus:
            case Keys.Control | Keys.Subtract:
            case Keys.Control | Keys.D0:
            case Keys.Control | Keys.NumPad0:
                if (HandleGalleryKeyboardZoom(keyData))
                {
                    return true;
                }

                break;
            case Keys.Delete:
                DeleteSelectedImage();
                return true;
            case Keys.Enter:
                OpenSelectedFile();
                return true;
            case Keys.Control | Keys.Enter:
                OpenSelectedFolder();
                return true;
            case Keys.Control | Keys.G:
                ApplyViewMode(_list.View == View.Details ? "grid" : "details");
                SaveViewState();
                return true;
            case Keys.Control | Keys.M:
                ShowDetailModal();
                return true;
            case Keys.Control | Keys.P:
                _previewVisible.Checked = !_previewVisible.Checked;
                return true;
            case Keys.Control | Keys.D:
                _detailsVisible.Checked = !_detailsVisible.Checked;
                return true;
            case Keys.Control | Keys.R:
                ReshuffleSort();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void RefreshKeyBindings()
    {
        _keyBindings = LoadKeyBindings(_store.GetSetting("keybindings_json", NativeImageStore.DefaultKeyBindingsJson), MainKeyBindingActions);
    }

    private static Dictionary<string, Keys> LoadKeyBindings(string json, IReadOnlyList<KeyBindingAction> actions)
    {
        var map = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions)
        {
            if (TryGetKeyBindingText(NativeImageStore.DefaultKeyBindingsJson, action.Key, out var defaultValue) &&
                TryNormalizeKeyBinding(defaultValue, out var defaultKeys, out _))
            {
                map[action.Key] = defaultKeys;
            }
        }

        foreach (var action in actions)
        {
            if (TryGetKeyBindingText(json, action.Key, out var value) &&
                TryNormalizeKeyBinding(value, out var keys, out _))
            {
                map[action.Key] = keys;
            }
        }

        return map;
    }

    private bool TryDispatchKeyBinding(Keys keyData)
    {
        if (IsTextEntryControl(ActiveControl))
        {
            return false;
        }

        var normalizedKeyData = NormalizeKeyData(keyData);
        foreach (var binding in _keyBindings)
        {
            if (binding.Value == normalizedKeyData)
            {
                return ExecuteMainKeyBindingAction(binding.Key);
            }
        }

        return false;
    }

    private bool ExecuteMainKeyBindingAction(string action)
    {
        switch (action)
        {
            case "next":
                SelectOffset(1);
                return true;
            case "previous":
                SelectOffset(-1);
                return true;
            case "favoriteUp":
                SetSelectedFavoriteLevel(Math.Min(5, (int)_favoriteLevel.Value + 1));
                return true;
            case "favoriteDown":
                SetSelectedFavoriteLevel(Math.Max(0, (int)_favoriteLevel.Value - 1));
                return true;
            case "delete":
                DeleteSelectedImage();
                return true;
            case "openFile":
                OpenSelectedFile();
                return true;
            case "openFolder":
                OpenSelectedFolder();
                return true;
            case "openDetail":
                ShowDetailModal();
                return true;
            case "toggleView":
                ApplyViewMode(_list.View == View.Details ? "grid" : "details");
                SaveViewState();
                return true;
            case "togglePreview":
                _previewVisible.Checked = !_previewVisible.Checked;
                return true;
            case "toggleDetails":
                _detailsVisible.Checked = !_detailsVisible.Checked;
                return true;
            case "reshuffleSort":
                ReshuffleSort();
                return true;
            default:
                return false;
        }
    }

    private static bool IsTextEntryControl(Control? control)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (current is TextBoxBase or ComboBox)
            {
                return true;
            }
        }

        return false;
    }

    private bool HandleGalleryKeyboardZoom(Keys keyData)
    {
        if (_list.View != View.LargeIcon)
        {
            return false;
        }

        switch (keyData)
        {
            case Keys.Control | Keys.Oemplus:
            case Keys.Control | Keys.Shift | Keys.Oemplus:
            case Keys.Control | Keys.Add:
                return ApplyGalleryThumbnailSize((int)_thumbnailSize.Value + GalleryThumbnailStep);
            case Keys.Control | Keys.OemMinus:
            case Keys.Control | Keys.Subtract:
                return ApplyGalleryThumbnailSize((int)_thumbnailSize.Value - GalleryThumbnailStep);
            case Keys.Control | Keys.D0:
            case Keys.Control | Keys.NumPad0:
                return ApplyGalleryThumbnailSize(GalleryThumbnailReset);
            default:
                return false;
        }
    }

    private bool HandleGalleryWheelZoom(int wheelDelta)
    {
        if (_list.View != View.LargeIcon || wheelDelta == 0)
        {
            return false;
        }

        var delta = wheelDelta > 0 ? GalleryThumbnailStep : -GalleryThumbnailStep;
        return ApplyGalleryThumbnailSize((int)_thumbnailSize.Value + delta);
    }

    private bool ApplyGalleryThumbnailSize(int size)
    {
        if (_list.View != View.LargeIcon)
        {
            return false;
        }

        var anchorIndex = GetGalleryViewportAnchorIndex();
        var clamped = Math.Clamp(size, GalleryThumbnailMin, GalleryThumbnailMax);
        ApplyThumbnailSize(clamped);
        SaveThumbnailSize(clamped);
        if (anchorIndex >= 0 && anchorIndex < _list.VirtualListSize)
        {
            _list.EnsureVisible(anchorIndex);
        }

        SetStatus($"Thumb {clamped}");
        return true;
    }

    private int GetGalleryViewportAnchorIndex()
    {
        var selectedIndex = GetSelectedIndex();
        if (selectedIndex >= 0)
        {
            return selectedIndex;
        }

        var center = new Point(Math.Max(0, _list.ClientSize.Width / 2), Math.Max(0, _list.ClientSize.Height / 2));
        var centeredItem = _list.GetItemAt(center.X, center.Y);
        if (centeredItem is not null)
        {
            return centeredItem.Index;
        }

        var client = _list.ClientRectangle;
        var bestIndex = -1;
        var bestDistance = int.MaxValue;
        for (var index = 0; index < _list.VirtualListSize; index++)
        {
            Rectangle rect;
            try
            {
                rect = _list.GetItemRect(index);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            if (rect.Bottom <= client.Top || rect.Top >= client.Bottom || rect.Right <= client.Left || rect.Left >= client.Right)
            {
                continue;
            }

            var distance = Math.Abs((rect.Left + rect.Width / 2) - center.X) +
                Math.Abs((rect.Top + rect.Height / 2) - center.Y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private int GetSelectedIndex()
    {
        if (_list.SelectedIndices.Count == 0)
        {
            return -1;
        }

        var index = _list.SelectedIndices[0];
        return index >= 0 && index < _visibleImages.Count ? index : -1;
    }

    private NativeImageRecord? GetSelectedImage()
    {
        var index = GetSelectedIndex();
        return index >= 0 ? _visibleImages[index] : null;
    }

    private List<NativeImageRecord> GetSelectedImages()
    {
        return _list.SelectedIndices
            .Cast<int>()
            .Where(index => index >= 0 && index < _visibleImages.Count)
            .Distinct()
            .OrderBy(static index => index)
            .Select(index => _visibleImages[index])
            .ToList();
    }

    private int FavoriteLevelForPath(string absolutePath)
    {
        return _allImages
            .FirstOrDefault(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase))
            ?.FavoriteLevel ?? 0;
    }

    private void SelectOffset(int offset)
    {
        if (_visibleImages.Count == 0)
        {
            return;
        }

        var current = GetSelectedIndex();
        var next = Math.Clamp(current < 0 ? 0 : current + offset, 0, _visibleImages.Count - 1);
        _list.SelectedIndices.Clear();
        _list.SelectedIndices.Add(next);
        _list.EnsureVisible(next);
        UpdateSelectionActions();
    }

    private void SelectImage(string absolutePath)
    {
        var index = _visibleImages.FindIndex(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        _list.SelectedIndices.Clear();
        _list.SelectedIndices.Add(index);
        _list.EnsureVisible(index);
        UpdateSelectionActions();
    }

    private void SelectImages(IEnumerable<string> absolutePaths)
    {
        var selectedIndices = absolutePaths
            .Select(path => _visibleImages.FindIndex(item => string.Equals(item.AbsolutePath, path, StringComparison.OrdinalIgnoreCase)))
            .Where(static index => index >= 0)
            .Distinct()
            .OrderBy(static index => index)
            .ToArray();
        if (selectedIndices.Length == 0)
        {
            UpdateSelectionActions();
            return;
        }

        _list.SelectedIndices.Clear();
        foreach (var index in selectedIndices)
        {
            _list.SelectedIndices.Add(index);
        }

        _list.EnsureVisible(selectedIndices[0]);
        UpdateSelectionActions();
    }

    private void SelectNearestAfterDelete(int deletedIndex)
    {
        if (_visibleImages.Count == 0)
        {
            UpdateSelectionActions();
            return;
        }

        _list.SelectedIndices.Clear();
        _list.SelectedIndices.Add(Math.Clamp(deletedIndex, 0, _visibleImages.Count - 1));
        UpdateSelectionActions();
    }

    private void UpdateSelectionActions()
    {
        var index = GetSelectedIndex();
        var selected = index >= 0 ? _visibleImages[index] : null;
        var selectedCount = _list.SelectedIndices.Count;
        if (selected is not null && selectedCount == 1)
        {
            selected = MarkImageSeenIfNeeded(selected, index);
        }

        _previousButton.Enabled = index > 0;
        _nextButton.Enabled = index >= 0 && index < _visibleImages.Count - 1;
        _detailButton.Enabled = selected is not null;
        _copyMetadataButton.Enabled = selected is not null && selectedCount == 1;
        _openFileButton.Enabled = selected is not null;
        _openFolderButton.Enabled = selected is not null;
        _deleteButton.Enabled = selected is not null;
        _removeFolderButton.Enabled = CurrentFolderSetRoots().Count > 0;
        _refreshButton.Enabled = CurrentFolderSetRoots().Count > 0;
        _recentSetButton.Enabled = true;
        _addFolderButton.Enabled = true;
        _favoriteLevel.Enabled = selectedCount > 0;
        _selectionLabel.Text = selectedCount > 0
            ? $"Selected {selectedCount:n0} / {_visibleImages.Count:n0}"
            : $"Selected 0 / {_visibleImages.Count:n0}";
        _updatingFavoriteControl = true;
        _favoriteLevel.Value = selected is null ? 0 : Math.Clamp(selected.FavoriteLevel, 0, 5);
        _updatingFavoriteControl = false;
        UpdatePreviewTabActions();
        SaveGalleryStateIfChanged(selected, index);
    }

    private NativeImageRecord MarkImageSeenIfNeeded(NativeImageRecord selected, int visibleIndex)
    {
        if (selected.IsSeen)
        {
            return selected;
        }

        _store.MarkImageSeen(selected.AbsolutePath);
        var updated = selected with { IsSeen = true };
        _visibleImages[visibleIndex] = updated;
        var allIndex = _allImages.FindIndex(item => string.Equals(item.AbsolutePath, updated.AbsolutePath, StringComparison.OrdinalIgnoreCase));
        if (allIndex >= 0)
        {
            _allImages[allIndex] = updated;
        }

        if (visibleIndex >= 0 && visibleIndex < _list.VirtualListSize)
        {
            _list.RedrawItems(visibleIndex, visibleIndex, invalidateOnly: false);
        }

        return updated;
    }

    private void SaveGalleryStateIfChanged(NativeImageRecord? selected, int visibleIndex)
    {
        if (selected is null || visibleIndex < 0)
        {
            return;
        }

        if (string.Equals(_lastSavedSelectedPath, selected.AbsolutePath, StringComparison.OrdinalIgnoreCase) &&
            _lastSavedVisibleIndex == visibleIndex)
        {
            return;
        }

        _store.SaveGalleryState(selected.AbsolutePath, visibleIndex);
        _lastSavedSelectedPath = selected.AbsolutePath;
        _lastSavedVisibleIndex = visibleIndex;
    }

    private void RestoreGalleryStateSelection(string? preferredPath)
    {
        _list.SelectedIndices.Clear();
        if (_visibleImages.Count == 0)
        {
            ClearPreview("Select an image.");
            return;
        }

        var index = FindVisibleImageIndex(preferredPath);
        if (index < 0)
        {
            index = FindVisibleImageIndex(_store.GetSetting("last_selected_image", ""));
        }

        if (index < 0)
        {
            index = Math.Clamp(ParseSettingInt("last_visible_index", 0), 0, _visibleImages.Count - 1);
        }

        _list.SelectedIndices.Add(index);
        _list.EnsureVisible(index);
    }

    private bool IsListIndexVisible(int index)
    {
        if (index < 0 || index >= _list.VirtualListSize)
        {
            return false;
        }

        try
        {
            var itemRect = _list.GetItemRect(index);
            var clientRect = _list.ClientRectangle;
            return itemRect.Bottom > clientRect.Top && itemRect.Top < clientRect.Bottom;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private int TryGetTopItemIndex()
    {
        try
        {
            return _list.TopItem?.Index ?? -1;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private int FindVisibleImageIndex(string? absolutePath)
    {
        return string.IsNullOrWhiteSpace(absolutePath)
            ? -1
            : _visibleImages.FindIndex(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
    }

    private void ReplaceImage(string absolutePath, NativeImageRecord updated)
    {
        var allIndex = _allImages.FindIndex(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
        if (allIndex >= 0)
        {
            _allImages[allIndex] = updated;
        }

        var visibleIndex = _visibleImages.FindIndex(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
        if (visibleIndex >= 0)
        {
            _visibleImages[visibleIndex] = updated;
        }
    }

    private string CurrentDisplayStyle()
    {
        return NormalizeDisplayStyle(_displayStyle.SelectedItem?.ToString() ?? _store.GetSetting("display_style", "standard"));
    }

    private string CurrentAspectMode()
    {
        return NormalizeAspectMode(_aspectMode.SelectedItem?.ToString() ?? _store.GetSetting("aspect_mode", "original"));
    }

    private void ApplyDisplayStyle(string style, bool persist = true)
    {
        var normalized = NormalizeDisplayStyle(style);
        _updatingDisplayStyle = true;
        try
        {
            _displayStyle.SelectedItem = DisplayStyleLabel(normalized);
        }
        finally
        {
            _updatingDisplayStyle = false;
        }

        if (normalized == "compact")
        {
            ApplyViewMode("grid", persist);
            ApplyAspectMode("square", persist);
            ApplyThumbnailSize(GalleryThumbnailMin);
            if (persist)
            {
                SaveThumbnailSize(GalleryThumbnailMin);
            }
        }
        else if (normalized == "poster")
        {
            ApplyViewMode("grid", persist);
            ApplyAspectMode("portrait", persist);
            ApplyThumbnailSize(GalleryThumbnailMax);
            if (persist)
            {
                SaveThumbnailSize(GalleryThumbnailMax);
            }
        }

        if (persist)
        {
            _store.SaveSetting("display_style", normalized);
        }

        _list.Invalidate();
    }

    private void ApplyAspectMode(string mode, bool persist = true)
    {
        var normalized = NormalizeAspectMode(mode);
        _updatingAspectMode = true;
        try
        {
            _aspectMode.SelectedItem = AspectModeLabel(normalized);
        }
        finally
        {
            _updatingAspectMode = false;
        }

        ApplyThumbnailSize((int)_thumbnailSize.Value);
        if (persist)
        {
            _store.SaveSetting("aspect_mode", normalized);
        }

        _list.Invalidate();
    }

    private static string NormalizeDisplayStyle(string style)
    {
        return style.Trim().ToLowerInvariant() switch
        {
            "compact" => "compact",
            "poster" => "poster",
            _ => "standard",
        };
    }

    private static string DisplayStyleLabel(string style)
    {
        return NormalizeDisplayStyle(style) switch
        {
            "compact" => "Compact",
            "poster" => "Poster",
            _ => "Standard",
        };
    }

    private static string DefaultAspectModeForDisplayStyle(string style)
    {
        return NormalizeDisplayStyle(style) switch
        {
            "compact" => "square",
            "poster" => "portrait",
            _ => "original",
        };
    }

    private static string NormalizeAspectMode(string mode)
    {
        return mode.Trim().ToLowerInvariant() switch
        {
            "square" => "square",
            "1:1" => "square",
            "portrait" => "portrait",
            "2:3" => "portrait",
            _ => "original",
        };
    }

    private static string AspectModeLabel(string mode)
    {
        return NormalizeAspectMode(mode) switch
        {
            "square" => "1:1",
            "portrait" => "2:3",
            _ => "Original",
        };
    }

    private void ApplyViewMode(string mode, bool persist = true)
    {
        var grid = string.Equals(mode, "grid", StringComparison.OrdinalIgnoreCase);
        _list.View = grid ? View.LargeIcon : View.Details;
        _list.FullRowSelect = !grid;
        if (_viewMode.Items.Count > 0)
        {
            _viewMode.SelectedItem = grid ? "Grid" : "List";
        }

        if (persist)
        {
            _store.SaveSetting("view_mode", grid ? "grid" : "details");
        }

        RefreshDateSectionHeaders();
        _list.Invalidate();
    }

    private void SaveViewState()
    {
        var (dateFrom, dateTo) = CurrentDateRange();
        _store.SaveViewState(
            _list.View == View.LargeIcon ? "grid" : "details",
            _searchText.Text.Trim(),
            _favoritesOnly.Checked,
            CurrentFavoriteFilterKey(),
            _enhancedOnly.Checked,
            CurrentDateFilterKey(),
            FormatDateInput(dateFrom),
            FormatDateInput(dateTo));
    }

    private static string FormatDateInput(DateTime? date)
    {
        return date.HasValue
            ? date.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : "";
    }

    private void ApplySortMode(string mode)
    {
        var normalized = _sortMode.Items.Cast<object>()
            .Select(static item => item.ToString() ?? "")
            .FirstOrDefault(item => string.Equals(item, mode, StringComparison.OrdinalIgnoreCase));
        _sortMode.SelectedItem = string.IsNullOrWhiteSpace(normalized) ? "Modified" : normalized;
    }

    private void SaveSortState()
    {
        _store.SaveSetting("sort_mode", _sortMode.SelectedItem?.ToString() ?? "Modified");
        _reshuffleButton.Enabled = string.Equals(_sortMode.SelectedItem?.ToString(), "Random", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<NativeImageRecord> ApplySort(IEnumerable<NativeImageRecord> images)
    {
        var mode = _sortMode.SelectedItem?.ToString() ?? "Modified";
        return mode switch
        {
            "Created" => images
                .OrderByDescending(static item => item.CreatedAtUtc)
                .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase),
            "Name" => images
                .OrderBy(static item => item.Filename, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase),
            "Folder" => images
                .OrderBy(static item => item.Folder, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Filename, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase),
            "Size" => images
                .OrderByDescending(static item => item.SizeBytes)
                .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase),
            "Favorite" => images
                .OrderByDescending(static item => item.FavoriteLevel)
                .ThenByDescending(static item => item.ModifiedAtUtc)
                .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase),
            "Random" => images
                .OrderBy(item => StableRandomKey(item.AbsolutePath, _randomSortSeed))
                .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase),
            _ => images
                .OrderByDescending(static item => item.ModifiedAtUtc)
                .ThenBy(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase),
        };
    }

    private void ReshuffleSort()
    {
        _randomSortSeed++;
        if (!string.Equals(_sortMode.SelectedItem?.ToString(), "Random", StringComparison.OrdinalIgnoreCase))
        {
            ApplySortMode("Random");
        }

        SaveSortState();
        ApplyFilter();
    }

    private static int StableRandomKey(string value, int seed)
    {
        unchecked
        {
            var hash = 2166136261u ^ (uint)seed;
            foreach (var ch in value)
            {
                hash ^= char.ToUpperInvariant(ch);
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }

    private void BuildFolderBuckets()
    {
        RefreshFavoriteFilterOptions();
        var hidden = new HashSet<string>(
            _store.GetSetting("hidden_folder_buckets", "")
                .Split(['\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
        var unsortedBuckets = _allImages
            .GroupBy(static item => item.Folder, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FolderBucket(group.Key, FormatFolderBucketLabel(group.Key), group.Count()));
        var buckets = SortFolderBuckets(unsortedBuckets, _folderSortMode.SelectedItem?.ToString() ?? "NameAsc").ToList();

        _updatingFolderBuckets = true;
        try
        {
            _folderBuckets.Items.Clear();
            foreach (var bucket in buckets)
            {
                _folderBuckets.Items.Add(bucket, !hidden.Contains(bucket.Folder));
            }

            _folderBucketRangeAnchorIndex = -1;
        }
        finally
        {
            _updatingFolderBuckets = false;
        }

        _folderBucketLabel.Text = $"Folders ({buckets.Count:n0})";
        UpdateFolderBucketButtons();
    }

    private void ApplyFolderSortMode(string mode)
    {
        var normalized = _folderSortMode.Items.Cast<object>()
            .Select(static item => item.ToString() ?? "")
            .FirstOrDefault(item => string.Equals(item, mode, StringComparison.OrdinalIgnoreCase));
        _folderSortMode.SelectedItem = string.IsNullOrWhiteSpace(normalized) ? "NameAsc" : normalized;
    }

    private void SaveFolderSortState()
    {
        _store.SaveSetting("folder_sort_mode", _folderSortMode.SelectedItem?.ToString() ?? "NameAsc");
    }

    private static IEnumerable<FolderBucket> SortFolderBuckets(IEnumerable<FolderBucket> buckets, string mode)
    {
        return mode switch
        {
            "NameDesc" => buckets
                .OrderByDescending(static item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Folder, StringComparer.OrdinalIgnoreCase),
            "CountDesc" => buckets
                .OrderByDescending(static item => item.Count)
                .ThenBy(static item => item.Label, StringComparer.OrdinalIgnoreCase),
            "CountAsc" => buckets
                .OrderBy(static item => item.Count)
                .ThenBy(static item => item.Label, StringComparer.OrdinalIgnoreCase),
            _ => buckets
                .OrderBy(static item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.Folder, StringComparer.OrdinalIgnoreCase),
        };
    }

    private IEnumerable<NativeImageRecord> ApplyFolderBucketFilter(IEnumerable<NativeImageRecord> images)
    {
        if (_folderBuckets.Items.Count == 0 || _folderBuckets.CheckedItems.Count == _folderBuckets.Items.Count)
        {
            return images;
        }

        var visibleFolders = _folderBuckets.CheckedItems
            .Cast<FolderBucket>()
            .Select(static item => item.Folder)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return visibleFolders.Count == 0
            ? []
            : images.Where(item => visibleFolders.Contains(item.Folder));
    }

    private void SetAllFolderBuckets(bool visible)
    {
        _updatingFolderBuckets = true;
        try
        {
            for (var i = 0; i < _folderBuckets.Items.Count; i++)
            {
                _folderBuckets.SetItemChecked(i, visible);
            }
        }
        finally
        {
            _updatingFolderBuckets = false;
        }

        SaveFolderBucketState();
        ApplyFilter();
    }

    private void InvertFolderBuckets()
    {
        _updatingFolderBuckets = true;
        try
        {
            for (var i = 0; i < _folderBuckets.Items.Count; i++)
            {
                _folderBuckets.SetItemChecked(i, !_folderBuckets.GetItemChecked(i));
            }
        }
        finally
        {
            _updatingFolderBuckets = false;
        }

        SaveFolderBucketState();
        ApplyFilter();
    }

    private void HandleFolderBucketMouseUp(MouseEventArgs args)
    {
        if (args.Button != MouseButtons.Left)
        {
            return;
        }

        var clickedIndex = _folderBuckets.IndexFromPoint(args.Location);
        if (clickedIndex < 0 || clickedIndex >= _folderBuckets.Items.Count)
        {
            return;
        }

        if ((ModifierKeys & Keys.Shift) == Keys.Shift && _folderBucketRangeAnchorIndex >= 0)
        {
            ApplyFolderBucketRange(_folderBucketRangeAnchorIndex, clickedIndex, _folderBuckets.GetItemChecked(clickedIndex));
            return;
        }

        _folderBucketRangeAnchorIndex = clickedIndex;
    }

    private void HandleFolderBucketKeyDown(KeyEventArgs args)
    {
        if (_folderBuckets.Items.Count == 0)
        {
            return;
        }

        if ((args.KeyData & Keys.Shift) == Keys.Shift &&
            (args.KeyCode == Keys.Up || args.KeyCode == Keys.Down))
        {
            var current = _folderBuckets.SelectedIndex >= 0 ? _folderBuckets.SelectedIndex : 0;
            if (_folderBucketRangeAnchorIndex < 0)
            {
                _folderBucketRangeAnchorIndex = current;
            }

            var delta = args.KeyCode == Keys.Up ? -1 : 1;
            var next = Math.Clamp(current + delta, 0, _folderBuckets.Items.Count - 1);
            SelectFolderBucketKeyboardRange(_folderBucketRangeAnchorIndex, next);
            args.Handled = true;
            args.SuppressKeyPress = true;
            return;
        }

        if (args.KeyCode == Keys.Space &&
            _folderBucketRangeAnchorIndex >= 0 &&
            _folderBuckets.SelectedIndex >= 0)
        {
            var visible = !_folderBuckets.GetItemChecked(_folderBuckets.SelectedIndex);
            if (ApplyFolderBucketRange(_folderBucketRangeAnchorIndex, _folderBuckets.SelectedIndex, visible))
            {
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
        }
    }

    private void SelectFolderBucketKeyboardRange(int anchorIndex, int focusIndex)
    {
        if (_folderBuckets.Items.Count == 0)
        {
            return;
        }

        var focus = Math.Clamp(focusIndex, 0, _folderBuckets.Items.Count - 1);
        _updatingFolderBucketKeyboardRange = true;
        try
        {
            _folderBuckets.ClearSelected();
            _folderBuckets.SelectedIndex = focus;
        }
        finally
        {
            _updatingFolderBucketKeyboardRange = false;
        }

        _folderBucketRangeAnchorIndex = Math.Clamp(anchorIndex, 0, _folderBuckets.Items.Count - 1);
        UpdateFolderBucketButtons();
    }

    private bool ApplyFolderBucketRange(int anchorIndex, int clickedIndex, bool visible)
    {
        if (_folderBuckets.Items.Count == 0)
        {
            return false;
        }

        var start = Math.Clamp(Math.Min(anchorIndex, clickedIndex), 0, _folderBuckets.Items.Count - 1);
        var end = Math.Clamp(Math.Max(anchorIndex, clickedIndex), 0, _folderBuckets.Items.Count - 1);
        if (start > end)
        {
            return false;
        }

        _updatingFolderBuckets = true;
        try
        {
            for (var index = start; index <= end; index++)
            {
                _folderBuckets.SetItemChecked(index, visible);
            }
        }
        finally
        {
            _updatingFolderBuckets = false;
        }

        SaveFolderBucketState();
        ApplyFilter();
        UpdateFolderBucketButtons();
        return true;
    }

    private void SetSelectedFolderBuckets(bool visible)
    {
        var selectedIndices = _folderBuckets.SelectedIndices.Cast<int>().ToArray();
        if (selectedIndices.Length == 0 && _folderBuckets.SelectedIndex >= 0)
        {
            selectedIndices = [_folderBuckets.SelectedIndex];
        }

        if (selectedIndices.Length == 0)
        {
            return;
        }

        _updatingFolderBuckets = true;
        try
        {
            foreach (var index in selectedIndices)
            {
                _folderBuckets.SetItemChecked(index, visible);
            }
        }
        finally
        {
            _updatingFolderBuckets = false;
        }

        SaveFolderBucketState();
        ApplyFilter();
    }

    private void ClearFolderBucketSelection()
    {
        _folderBuckets.ClearSelected();
        UpdateFolderBucketButtons();
    }

    private void SaveFolderBucketState()
    {
        var hidden = _folderBuckets.Items
            .Cast<FolderBucket>()
            .Where((_, index) => !_folderBuckets.GetItemChecked(index))
            .Select(static item => item.Folder);
        _store.SaveSetting("hidden_folder_buckets", string.Join('\n', hidden));
        UpdateFolderBucketButtons();
    }

    private void UpdateFolderBucketButtons()
    {
        var hasBuckets = _folderBuckets.Items.Count > 0;
        var hasSelectedBuckets = _folderBuckets.SelectedIndices.Count > 0 || _folderBuckets.SelectedIndex >= 0;
        _showAllFoldersButton.Enabled = hasBuckets;
        _hideAllFoldersButton.Enabled = hasBuckets;
        _invertFoldersButton.Enabled = hasBuckets;
        _showSelectedFoldersButton.Enabled = hasSelectedBuckets;
        _hideSelectedFoldersButton.Enabled = hasSelectedBuckets;
        _clearFolderSelectionButton.Enabled = hasSelectedBuckets;
    }

    private string FormatFolderBucketLabel(string folder)
    {
        if (_currentRoots.Count > 0)
        {
            return NativeFolderSet.FormatFolderLabel(folder, _currentRoots);
        }

        return folder;
    }

    private void ApplyThumbnailSize(int size)
    {
        var clamped = Math.Clamp(size, GalleryThumbnailMin, GalleryThumbnailMax);
        _updatingThumbnailSize = true;
        try
        {
            if ((int)_thumbnailSize.Value != clamped)
            {
                _thumbnailSize.Value = clamped;
            }
        }
        finally
        {
            _updatingThumbnailSize = false;
        }

        var imageSize = GridImageSizeFor(clamped, CurrentAspectMode());
        if (_gridImages.ImageSize == imageSize && _gridImages.Images.Count > 0)
        {
            return;
        }

        _gridImages.Images.Clear();
        _gridImages.ImageSize = imageSize;
        _gridImages.Images.Add(CreateGridPlaceholder(imageSize));
        _list.Invalidate();
    }

    private void SaveThumbnailSize(int size)
    {
        _store.SaveSetting("thumbnail_size", size.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static Size GridImageSizeFor(int thumbnailSize, string aspectMode)
    {
        var width = Math.Clamp(thumbnailSize, GalleryThumbnailMin, GalleryThumbnailMax);
        if (NormalizeAspectMode(aspectMode) == "square")
        {
            return new Size(width, width);
        }

        var height = Math.Min(MaxGridImageListDimension, Math.Max(width, (int)Math.Round(width * 1.5d)));
        return new Size(width, height);
    }

    private void ApplyPreviewVisibility()
    {
        if (_mainSplit is not null)
        {
            _mainSplit.Panel2Collapsed = !_previewVisible.Checked;
        }
    }

    private void ApplyDetailsVisibility()
    {
        _previewLabel.Visible = _detailsVisible.Checked;
    }

    private void ApplyPreviewSplitterDistance(int distance)
    {
        if (_mainSplit is null)
        {
            return;
        }

        try
        {
            _mainSplit.SplitterDistance = ClampPreviewSplitterDistance(distance);
        }
        catch (InvalidOperationException)
        {
            // The splitter can be measured as width 0 before the first layout pass.
        }
    }

    private int ClampPreviewSplitterDistance(int distance)
    {
        if (_mainSplit is null || _mainSplit.Width <= 0)
        {
            return Math.Max(120, distance);
        }

        var min = Math.Max(120, _mainSplit.Panel1MinSize);
        var max = Math.Max(min, _mainSplit.Width - Math.Max(160, _mainSplit.Panel2MinSize));
        return Math.Clamp(distance, min, max);
    }

    private void SavePreviewSplitterDistance()
    {
        if (_mainSplit is null || _mainSplit.Panel2Collapsed)
        {
            return;
        }

        _store.SaveSetting("preview_splitter_distance", _mainSplit.SplitterDistance.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private bool VerifyPreviewSplitterPersistence()
    {
        if (_mainSplit is null)
        {
            return false;
        }

        var original = _mainSplit.SplitterDistance;
        var target = ClampPreviewSplitterDistance(original + 24);
        if (target == original)
        {
            target = ClampPreviewSplitterDistance(original - 24);
        }

        if (target == original)
        {
            SavePreviewSplitterDistance();
            return _store.GetSetting("preview_splitter_distance", "")
                .Equals(original.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        ApplyPreviewSplitterDistance(target);
        SavePreviewSplitterDistance();
        var persisted = _store.GetSetting("preview_splitter_distance", "")
            .Equals(target.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        ApplyPreviewSplitterDistance(original);
        SavePreviewSplitterDistance();
        return persisted;
    }

    private void ShowNativeSettings()
    {
        using var dialog = new NativeSettingsDialog(BuildNativeSettingsSnapshot());
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.KeyBindingsJson is not null)
        {
            _store.SaveSetting("keybindings_json", dialog.KeyBindingsJson);
            RefreshKeyBindings();
            SetStatus("Saved native key bindings.");
        }
    }

    private NativeSettingsSnapshot BuildNativeSettingsSnapshot()
    {
        return new NativeSettingsSnapshot(
            DatabasePath: _store.DatabasePath,
            BrowserSettingsImported: _store.GetSetting("browser_settings_imported", _store.GetSetting("browser_settings_found", "0")) == "1",
            KeyBindingsJson: _store.GetSetting("keybindings_json", NativeImageStore.DefaultKeyBindingsJson),
            KeyBindingMode: "editable main and detail keyboard actions; detailPan deferred",
            ImportWarningCount: ParseSettingInt("import_warning_count", 0),
            ImportRecoverySummary: _store.GetSetting("import_recovery_summary", ""));
    }

    private static Dictionary<string, string> ExtractKeyBindingTextMap(string json)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    map[property.Name] = property.Value.GetString() ?? "";
                }
            }
        }
        catch (JsonException)
        {
            return map;
        }

        return map;
    }

    private static bool TryBuildKeyBindingsJson(
        string existingJson,
        IReadOnlyDictionary<string, string> updates,
        out string json,
        out string error)
    {
        json = "";
        error = "";

        var merged = ExtractKeyBindingTextMap(existingJson);
        foreach (var fallback in ExtractKeyBindingTextMap(NativeImageStore.DefaultKeyBindingsJson))
        {
            merged.TryAdd(fallback.Key, fallback.Value);
        }

        var usedBySurface = new Dictionary<string, Dictionary<Keys, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in EditableKeyBindingActions)
        {
            if (!updates.TryGetValue(action.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                error = $"{action.Label} needs a key binding.";
                return false;
            }

            if (!TryNormalizeKeyBinding(raw, out var keys, out var normalized))
            {
                error = $"{action.Label} has an unsupported key binding.";
                return false;
            }

            if (!usedBySurface.TryGetValue(action.Surface, out var used))
            {
                used = new Dictionary<Keys, string>();
                usedBySurface[action.Surface] = used;
            }

            if (used.TryGetValue(keys, out var existingAction))
            {
                error = $"{action.Label} conflicts with {existingAction}.";
                return false;
            }

            used[keys] = action.Label;
            merged[action.Key] = normalized;
        }

        json = JsonSerializer.Serialize(merged);
        return true;
    }

    private static bool TryGetKeyBindingText(string json, string action, out string value)
    {
        value = "";
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(action, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                value = property.GetString() ?? "";
                return !string.IsNullOrWhiteSpace(value);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryNormalizeKeyBinding(string value, out Keys keys, out string normalized)
    {
        keys = Keys.None;
        normalized = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Trim().Equals("+", StringComparison.Ordinal))
        {
            keys = Keys.Oemplus;
            normalized = "Plus";
            return true;
        }

        if (value.Trim().Equals("-", StringComparison.Ordinal))
        {
            keys = Keys.OemMinus;
            normalized = "Minus";
            return true;
        }

        var modifiers = Keys.None;
        var key = Keys.None;
        foreach (var rawPart in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= Keys.Control;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= Keys.Shift;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= Keys.Alt;
            }
            else if (key == Keys.None && TryParseKeyToken(part, out var parsedKey))
            {
                key = parsedKey;
            }
            else
            {
                return false;
            }
        }

        if (key == Keys.None)
        {
            return false;
        }

        keys = NormalizeKeyData(modifiers | key);
        return TryFormatKeyBinding(keys, out normalized);
    }

    private static bool TryFormatKeyBinding(Keys keyData, out string normalized)
    {
        normalized = "";
        var key = NormalizeKeyData(keyData) & Keys.KeyCode;
        if (!TryFormatKeyToken(key, out var keyToken))
        {
            return false;
        }

        var parts = new List<string>();
        if ((keyData & Keys.Control) == Keys.Control)
        {
            parts.Add("Ctrl");
        }

        if ((keyData & Keys.Shift) == Keys.Shift)
        {
            parts.Add("Shift");
        }

        if ((keyData & Keys.Alt) == Keys.Alt)
        {
            parts.Add("Alt");
        }

        parts.Add(keyToken);
        normalized = string.Join('+', parts);
        return true;
    }

    private static Keys NormalizeKeyData(Keys keyData)
    {
        return (keyData & Keys.KeyCode) |
            (keyData & Keys.Control) |
            (keyData & Keys.Shift) |
            (keyData & Keys.Alt);
    }

    private static bool TryParseKeyToken(string token, out Keys key)
    {
        key = token.ToUpperInvariant() switch
        {
            "RIGHT" => Keys.Right,
            "LEFT" => Keys.Left,
            "UP" => Keys.Up,
            "DOWN" => Keys.Down,
            "ESC" or "ESCAPE" => Keys.Escape,
            "ENTER" or "RETURN" => Keys.Enter,
            "SPACE" => Keys.Space,
            "DELETE" or "DEL" => Keys.Delete,
            "PLUS" or "OEMPLUS" => Keys.Oemplus,
            "MINUS" or "OEMMINUS" => Keys.OemMinus,
            _ => Keys.None,
        };

        if (key != Keys.None)
        {
            return true;
        }

        if (token.Length == 1)
        {
            var ch = char.ToUpperInvariant(token[0]);
            if (ch is >= 'A' and <= 'Z')
            {
                key = Keys.A + (ch - 'A');
                return true;
            }

            if (ch is >= '0' and <= '9')
            {
                key = Keys.D0 + (ch - '0');
                return true;
            }
        }

        if (token.StartsWith('F') &&
            int.TryParse(token[1..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var functionKey) &&
            functionKey is >= 1 and <= 24)
        {
            key = Keys.F1 + (functionKey - 1);
            return true;
        }

        if (token.StartsWith("NumPad", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token[6..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var numPadKey) &&
            numPadKey is >= 0 and <= 9)
        {
            key = Keys.NumPad0 + numPadKey;
            return true;
        }

        return false;
    }

    private static bool TryFormatKeyToken(Keys key, out string token)
    {
        token = key switch
        {
            Keys.Right => "Right",
            Keys.Left => "Left",
            Keys.Up => "Up",
            Keys.Down => "Down",
            Keys.Escape => "Escape",
            Keys.Enter => "Enter",
            Keys.Space => "Space",
            Keys.Delete => "Delete",
            Keys.Oemplus => "Plus",
            Keys.OemMinus => "Minus",
            _ => "",
        };

        if (token.Length > 0)
        {
            return true;
        }

        if (key is >= Keys.A and <= Keys.Z)
        {
            token = ((char)('A' + key - Keys.A)).ToString();
            return true;
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            token = ((char)('0' + key - Keys.D0)).ToString();
            return true;
        }

        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            token = $"NumPad{key - Keys.NumPad0}";
            return true;
        }

        if (key is >= Keys.F1 and <= Keys.F24)
        {
            token = $"F{key - Keys.F1 + 1}";
            return true;
        }

        return false;
    }

    private void WarmNeighborPreviews(int index)
    {
        var neighbors = new List<string>();
        foreach (var neighbor in new[] { index - 1, index + 1 })
        {
            if (neighbor < 0 || neighbor >= _visibleImages.Count)
            {
                continue;
            }

            var path = _visibleImages[neighbor].AbsolutePath;
            if (_previewRing.TryGet(path, out _))
            {
                continue;
            }

            neighbors.Add(path);
        }

        foreach (var path in neighbors)
        {
            _ = _cacheScheduler.ScheduleAsync(
                NativeCacheJobKind.NeighborDecode,
                path,
                ct =>
                {
                    if (!_previewRing.TryGet(path, out _))
                    {
                        _previewRing.Store(path, LoadImageCopy(path));
                    }
                });
        }
    }

    private static string FormatDimensions(NativeImageRecord image)
    {
        return image.Width is > 0 && image.Height is > 0 ? $"{image.Width}x{image.Height}" : "";
    }

    private static string FormatSize(Size size)
    {
        return $"{size.Width}x{size.Height}";
    }

    private static string FormatPreviewDetails(NativeImageRecord image, string dimensionHint)
    {
        var details = string.IsNullOrWhiteSpace(dimensionHint)
            ? $"{image.Filename}  {FormatBytes(image.SizeBytes)}  fav {image.FavoriteLevel}"
            : $"{image.Filename}  {FormatBytes(image.SizeBytes)}  {dimensionHint}  fav {image.FavoriteLevel}";
        var metadata = FormatMetadataDisplay(image, maxValueLength: 72);
        return string.IsNullOrWhiteSpace(metadata) ? details : $"{details}  {metadata}";
    }

    private static string BuildMetadataCopyText(NativeImageRecord image)
    {
        var dimensions = FormatDimensions(image);
        var lines = new List<string>
        {
            MetadataCopyKind(image),
            $"File: {image.Filename}",
            $"Path: {image.AbsolutePath}",
            $"Size: {FormatBytes(image.SizeBytes)}",
            $"Favorite: {image.FavoriteLevel}",
        };

        if (!string.IsNullOrWhiteSpace(dimensions))
        {
            lines.Insert(4, $"Dimensions: {dimensions}");
        }

        AddMetadataCopyBlock(lines, "Prompt", image.Prompt);
        AddMetadataCopyBlock(lines, "Negative prompt", image.NegativePrompt);
        AddMetadataCopyBlock(lines, "Settings", image.MetadataSettingsSummary);
        AddMetadataCopyBlock(lines, "Raw parameters", image.MetadataRaw);
        return string.Join(Environment.NewLine, lines);
    }

    private static string MetadataCopyKind(NativeImageRecord image)
    {
        return string.Equals(Path.GetExtension(image.AbsolutePath), ".png", StringComparison.OrdinalIgnoreCase)
            ? "PNG info"
            : "Image info";
    }

    private static string FormatMetadataDisplay(NativeImageRecord image, int maxValueLength)
    {
        var parts = new List<string>();
        AddMetadataPart(parts, "Prompt", image.Prompt, maxValueLength);
        AddMetadataPart(parts, "Negative prompt", image.NegativePrompt, maxValueLength);
        AddMetadataPart(parts, "Settings", image.MetadataSettingsSummary, maxValueLength);
        return string.Join("  ", parts);
    }

    private static bool HasMetadata(NativeImageRecord image)
    {
        return !string.IsNullOrWhiteSpace(image.Prompt) ||
            !string.IsNullOrWhiteSpace(image.NegativePrompt) ||
            !string.IsNullOrWhiteSpace(image.MetadataSettingsSummary) ||
            !string.IsNullOrWhiteSpace(image.MetadataRaw);
    }

    private static List<string> SplitPromptTags(string prompt)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tags = new List<string>();
        foreach (var rawTag in prompt.Split(','))
        {
            var tag = NormalizePromptTag(rawTag);
            if (tag.Length < 2 || tag.Contains('\n', StringComparison.Ordinal) || !seen.Add(tag))
            {
                continue;
            }

            tags.Add(tag);
            if (tags.Count >= 160)
            {
                break;
            }
        }

        return tags;
    }

    private static List<string> ParseSearchTags(string query)
    {
        return query
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length > 0)
            .ToList();
    }

    private static string NormalizeSearchText(string query)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tags = new List<string>();
        foreach (var tag in ParseSearchTags(query))
        {
            if (seen.Add(tag))
            {
                tags.Add(tag);
            }
        }

        return string.Join(", ", tags);
    }

    private static string NormalizePromptTag(string tag)
    {
        return tag
            .TrimStart(PromptTagLeadingTrimChars)
            .TrimEnd(PromptTagTrailingTrimChars)
            .Trim();
    }

    private static void AddMetadataCopyBlock(ICollection<string> lines, string label, string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return;
        }

        lines.Add($"{label}:");
        lines.Add(normalized);
    }

    private static void AddMetadataPart(ICollection<string> parts, string label, string value, int maxValueLength)
    {
        var normalized = NormalizeInlineText(value);
        if (normalized.Length == 0)
        {
            return;
        }

        parts.Add($"{label}: {TrimForInlineDisplay(normalized, maxValueLength)}");
    }

    private static string NormalizeInlineText(string value)
    {
        return string.Join(" ", value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();
    }

    private static string TrimForInlineDisplay(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(1, maxLength - 3)] + "...";
    }

    private static Bitmap CreateGridPlaceholder(Size size)
    {
        var bitmap = new Bitmap(size.Width, size.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(38, 38, 38));
        using var border = new Pen(Color.FromArgb(80, 80, 80));
        var inset = Math.Max(8, Math.Min(size.Width, size.Height) / 12);
        graphics.DrawRectangle(
            border,
            inset,
            inset,
            Math.Max(1, size.Width - (inset * 2)),
            Math.Max(1, size.Height - (inset * 2)));
        return bitmap;
    }

    private static string PrepareDateFilterSmokeFolder(string projectRoot)
    {
        var runId = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Environment.ProcessId}";
        var folder = Path.Combine(projectRoot, ".cache", "native-date-filter-smoke", runId);
        var today = DateTime.Today;
        WriteSmokeProbePng(Path.Combine(folder, "m15-date-today.png"), Color.DarkCyan, ToUtcNoon(today));
        WriteSmokeProbePng(Path.Combine(folder, "m15-date-last-7d.png"), Color.ForestGreen, ToUtcNoon(today.AddDays(-6)));
        WriteSmokeProbePng(Path.Combine(folder, "m15-date-last-30d.png"), Color.SlateBlue, ToUtcNoon(today.AddDays(-20)));
        WriteSmokeProbePng(Path.Combine(folder, "m15-date-last-year.png"), Color.Firebrick, ToUtcNoon(today.AddYears(-1)));
        return folder;
    }

    private static DateTime ToUtcNoon(DateTime localDate)
    {
        var localNoon = DateTime.SpecifyKind(localDate.Date.AddHours(12), DateTimeKind.Local);
        return localNoon.ToUniversalTime();
    }

    private static Image LoadImageCopy(string filePath)
    {
        return NativeImageDecoder.LoadImageCopy(filePath);
    }

    private static void WriteSmokeProbePng(string path, Color color, DateTime? timestampUtc = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new Bitmap(18, 18);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(color);
        }

        bitmap.Save(path, ImageFormat.Png);
        if (!timestampUtc.HasValue)
        {
            return;
        }

        try
        {
            File.SetCreationTimeUtc(path, timestampUtc.Value);
            File.SetLastWriteTimeUtc(path, timestampUtc.Value);
            File.SetLastAccessTimeUtc(path, timestampUtc.Value);
        }
        catch
        {
            // Timestamp normalization is best-effort on local smoke fixtures.
        }
    }

    private List<NativeImageRecord> ReapplyFavorites(IEnumerable<NativeImageRecord> images)
    {
        return images.Select(image => image with
        {
            FavoriteLevel = _favorites.TryGetValue(image.AbsolutePath, out var level) ? level : 0,
        }).ToList();
    }

    private string EnhancementStateFingerprint()
    {
        var path = Path.Combine(_projectRoot, ".cache", "enhance", "jobs.json");
        if (!File.Exists(path))
        {
            return "missing";
        }

        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string Quote(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private void SaveScreenshot(string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private static int CountTextFitWarnings(Control root)
    {
        var warnings = IsTextFitWarning(root) ? 1 : 0;
        foreach (Control child in root.Controls)
        {
            warnings += CountTextFitWarnings(child);
        }

        return warnings;
    }

    private static bool IsTextFitWarning(Control control)
    {
        if (!control.Visible || string.IsNullOrWhiteSpace(control.Text))
        {
            return false;
        }

        if (control is not Button && control is not CheckBox)
        {
            return false;
        }

        var measured = TextRenderer.MeasureText(control.Text, control.Font);
        var extraWidth = control is CheckBox ? 26 : 8;
        return measured.Width + extraWidth > control.ClientSize.Width || measured.Height + 6 > control.ClientSize.Height;
    }

    private static int CountSiblingOverlapWarnings(Control parent)
    {
        var visibleChildren = parent.Controls
            .Cast<Control>()
            .Where(static child => child.Visible && child.Width > 0 && child.Height > 0)
            .ToList();
        var warnings = 0;
        for (var i = 0; i < visibleChildren.Count; i++)
        {
            for (var j = i + 1; j < visibleChildren.Count; j++)
            {
                if (visibleChildren[i].Bounds.IntersectsWith(visibleChildren[j].Bounds))
                {
                    warnings++;
                }
            }
        }

        foreach (var child in visibleChildren)
        {
            warnings += CountSiblingOverlapWarnings(child);
        }

        return warnings;
    }

    private static string FormatUiSmokeReport(NativeUiSmokeReport report)
    {
        return string.Join(" ", new[]
        {
            "native-ui-smoke complete",
            "runtime=winforms",
            $"folder=\"{Quote(report.Folder)}\"",
            $"scannedImages={report.ScannedImages}",
            $"initialVisible={report.InitialVisible}",
            $"previewLoaded={BoolText(report.PreviewLoaded)}",
            $"previewTabs={BoolText(report.PreviewTabs)}",
            $"previewTabPin={BoolText(report.PreviewTabPin)}",
            $"previewTabClose={BoolText(report.PreviewTabClose)}",
            $"previewTabRestore={BoolText(report.PreviewTabRestore)}",
            $"hoverQuickPreview={BoolText(report.HoverQuickPreview)}",
            $"navigationButtons={BoolText(report.NavigationButtons)}",
            $"keyboardNavigation={BoolText(report.KeyboardNavigation)}",
            $"keyboardFavorite={BoolText(report.KeyboardFavorite)}",
            $"gridToggle={BoolText(report.GridToggle)}",
            $"folderBuckets={report.FolderBuckets}",
            $"folderHideAll={BoolText(report.FolderHideAll)}",
            $"folderShowSelected={BoolText(report.FolderShowSelected)}",
            $"folderHideSelected={BoolText(report.FolderHideSelected)}",
            $"folderRangeSelection={BoolText(report.FolderRangeSelection)}",
            $"folderKeyboardRangeSelection={BoolText(report.FolderKeyboardRangeSelection)}",
            $"folderClearSelection={BoolText(report.FolderClearSelection)}",
            $"folderSortMode={BoolText(report.FolderSortMode)}",
            $"sortName={BoolText(report.SortName)}",
            $"randomReshuffle={BoolText(report.RandomReshuffle)}",
            $"thumbnailSize={BoolText(report.ThumbnailSize)}",
            $"galleryWheelZoom={BoolText(report.GalleryWheelZoom)}",
            $"galleryKeyboardZoom={BoolText(report.GalleryKeyboardZoom)}",
            $"displayModes={BoolText(report.DisplayModes)}",
            $"displayModeVisuals={BoolText(report.DisplayModeVisuals)}",
            $"compactGridImageSize={report.CompactGridImageSize}",
            $"posterGridImageSize={report.PosterGridImageSize}",
            $"aspectModes={BoolText(report.AspectModes)}",
            $"previewToggle={BoolText(report.PreviewToggle)}",
            $"detailsToggle={BoolText(report.DetailsToggle)}",
            $"previewSplitter={BoolText(report.PreviewSplitter)}",
            $"selectedCount={BoolText(report.SelectedCount)}",
            $"galleryStateRestore={BoolText(report.GalleryStateRestore)}",
            $"multiSelection={BoolText(report.MultiSelection)}",
            $"bulkFavoriteSet={BoolText(report.BulkFavoriteSet)}",
            $"bulkFavoriteClear={BoolText(report.BulkFavoriteClear)}",
            $"bulkOpenFiles={BoolText(report.BulkOpenFiles)}",
            $"bulkOpenFolders={BoolText(report.BulkOpenFolders)}",
            $"backgroundClear={BoolText(report.BackgroundClear)}",
            $"favoriteFilterCounts={BoolText(report.FavoriteFilterCounts)}",
            $"favoriteLevelFilter={BoolText(report.FavoriteLevelFilter)}",
            $"unratedFilter={BoolText(report.UnratedFilter)}",
            $"enhancedOnlyFilter={BoolText(report.EnhancedOnlyFilter)}",
            $"clearSearch={BoolText(report.ClearSearch)}",
            $"detailModal={BoolText(report.DetailModal)}",
            $"detailNavigation={BoolText(report.DetailNavigation)}",
            $"detailZoom={BoolText(report.DetailZoom)}",
            $"detailReset={BoolText(report.DetailReset)}",
            $"detailPan={BoolText(report.DetailPan)}",
            $"detailFlip={BoolText(report.DetailFlip)}",
            $"detailFavorite={BoolText(report.DetailFavorite)}",
            $"detailOpenExternal={BoolText(report.DetailOpenExternal)}",
            $"metadataDisplay={BoolText(report.MetadataDisplay)}",
            $"metadataCopy={BoolText(report.MetadataCopy)}",
            $"promptTagAction={BoolText(report.PromptTagAction)}",
            $"searchChips={BoolText(report.SearchChips)}",
            $"searchChipCommit={BoolText(report.SearchChipCommit)}",
            $"searchSuggestion={BoolText(report.SearchSuggestion)}",
            $"keybindingRecorder={BoolText(report.KeybindingRecorder)}",
            $"keybindingConflictRejected={BoolText(report.KeybindingConflictRejected)}",
            $"keybindingRuntime={BoolText(report.KeybindingRuntime)}",
            $"detailKeybindingRuntime={BoolText(report.DetailKeybindingRuntime)}",
            $"searchMatches={report.SearchMatches}",
            $"favoriteMatches={report.FavoriteMatches}",
            $"noResultsState={BoolText(report.NoResultsState)}",
            $"folderErrorState={BoolText(report.FolderErrorState)}",
            $"albums={report.Albums}",
            $"albumImages={report.AlbumImages}",
            $"browserStateKeys={report.BrowserStateKeys}",
            $"settingsImported={BoolText(report.SettingsImported)}",
            $"enhancementStateUnchanged={BoolText(report.EnhancementStateUnchanged)}",
            "browserRuntime=false",
            "localHttpServer=false",
            "nodeRuntime=false",
        });
    }

    private static string FormatUiScreenshotReport(NativeUiScreenshotReport report)
    {
        return string.Join(" ", new[]
        {
            "native-ui-screenshot complete",
            "runtime=winforms",
            $"folder=\"{Quote(report.Folder)}\"",
            $"output=\"{Quote(report.OutputPath)}\"",
            $"width={report.Width}",
            $"height={report.Height}",
            $"scannedImages={report.ScannedImages}",
            $"visibleImages={report.VisibleImages}",
            $"previewLoaded={BoolText(report.PreviewLoaded)}",
            $"textFitWarnings={report.TextFitWarnings}",
            $"overlapWarnings={report.OverlapWarnings}",
            $"focusControl={report.FocusControl}",
            $"enhancementStateUnchanged={BoolText(report.EnhancementStateUnchanged)}",
            "browserRuntime=false",
            "localHttpServer=false",
            "nodeRuntime=false",
        });
    }

    private static string FormatFolderSetSmokeReport(NativeFolderSetSmokeReport report)
    {
        return string.Join(" ", new[]
        {
            "native-folder-set-smoke complete",
            "runtime=winforms",
            $"roots={report.Roots}",
            $"removedRoots={report.RemovedRoots}",
            $"folderBuckets={report.FolderBuckets}",
            $"imagesBeforeRemove={report.ImagesBeforeRemove}",
            $"imagesAfterRemove={report.ImagesAfterRemove}",
            $"searchMatches={report.SearchMatches}",
            $"recentSetPersisted={BoolText(report.RecentSetPersisted)}",
            $"removeFolder={BoolText(report.RemoveFolder)}",
            $"openRecentSet={BoolText(report.OpenRecentSet)}",
            $"manualRefreshAdded={BoolText(report.ManualRefreshAdded)}",
            $"manualRefreshRemoved={BoolText(report.ManualRefreshRemoved)}",
            $"watcherRoots={BoolText(report.WatcherRoots)}",
            $"enhancementStateUnchanged={BoolText(report.EnhancementStateUnchanged)}",
            "browserRuntime=false",
            "localHttpServer=false",
            "nodeRuntime=false",
        });
    }

    private static string FormatLargeScrollSmokeReport(NativeLargeScrollSmokeReport report)
    {
        return string.Join(" ", new[]
        {
            "native-large-scroll-smoke complete",
            "runtime=winforms",
            $"folder=\"{Quote(report.Folder)}\"",
            $"totalImages={report.TotalImages}",
            $"initialVisible={report.InitialVisible}",
            $"targetIndex={report.TargetIndex}",
            $"restoredIndex={report.RestoredIndex}",
            $"topIndexBeforeRestore={report.TopIndexBeforeRestore}",
            $"topIndexAfterRestore={report.TopIndexAfterRestore}",
            $"virtualMode={BoolText(report.VirtualMode)}",
            $"virtualListSize={report.VirtualListSize}",
            $"statePersisted={BoolText(report.StatePersisted)}",
            $"restoreSelected={BoolText(report.RestoreSelected)}",
            $"ensureVisible={BoolText(report.EnsureVisible)}",
            $"visibleBeforeRestore={BoolText(report.VisibleBeforeRestore)}",
            $"galleryZoomSelectedAnchor={BoolText(report.GalleryZoomSelectedAnchor)}",
            $"galleryZoomCenterAnchor={BoolText(report.GalleryZoomCenterAnchor)}",
            $"enhancementStateUnchanged={BoolText(report.EnhancementStateUnchanged)}",
            "browserRuntime=false",
            "localHttpServer=false",
            "nodeRuntime=false",
        });
    }

    private static string FormatDateFilterSmokeReport(NativeDateFilterSmokeReport report)
    {
        return string.Join(" ", new[]
        {
            "native-date-filter-smoke complete",
            "runtime=winforms",
            $"folder=\"{Quote(report.Folder)}\"",
            $"totalImages={report.TotalImages}",
            $"initialVisible={report.InitialVisible}",
            $"todayMatches={report.TodayMatches}",
            $"last7Matches={report.Last7Matches}",
            $"last30Matches={report.Last30Matches}",
            $"thisYearMatches={report.ThisYearMatches}",
            $"manualRangeMatches={report.ManualRangeMatches}",
            $"manualFromOnlyMatches={report.ManualFromOnlyMatches}",
            $"manualToOnlyMatches={report.ManualToOnlyMatches}",
            $"manualSearchMatches={report.ManualSearchMatches}",
            $"manualFavoriteMatches={report.ManualFavoriteMatches}",
            $"clearMatches={report.ClearMatches}",
            $"todayFilter={BoolText(report.TodayFilter)}",
            $"last7Filter={BoolText(report.Last7Filter)}",
            $"last30Filter={BoolText(report.Last30Filter)}",
            $"thisYearFilter={BoolText(report.ThisYearFilter)}",
            $"manualRangeFilter={BoolText(report.ManualRangeFilter)}",
            $"manualFromOnlyFilter={BoolText(report.ManualFromOnlyFilter)}",
            $"manualToOnlyFilter={BoolText(report.ManualToOnlyFilter)}",
            $"manualSearchFilter={BoolText(report.ManualSearchFilter)}",
            $"manualFavoriteFilter={BoolText(report.ManualFavoriteFilter)}",
            $"manualRangePersisted={BoolText(report.ManualRangePersisted)}",
            $"clearFilter={BoolText(report.ClearFilter)}",
            $"dateFilterPersisted={BoolText(report.DateFilterPersisted)}",
            $"enhancementStateUnchanged={BoolText(report.EnhancementStateUnchanged)}",
            "browserRuntime=false",
            "localHttpServer=false",
            "nodeRuntime=false",
        });
    }

    private static string FormatDateSectionSmokeReport(NativeDateSectionSmokeReport report)
    {
        return string.Join(" ", new[]
        {
            "native-date-section-smoke complete",
            "runtime=winforms",
            $"folder=\"{Quote(report.Folder)}\"",
            $"totalImages={report.TotalImages}",
            $"initialVisible={report.InitialVisible}",
            $"headerGroups={report.HeaderGroups}",
            $"firstHeader=\"{Quote(report.FirstHeader)}\"",
            $"showDateHeaders={BoolText(report.ShowDateHeaders)}",
            $"firstItemGrouped={BoolText(report.FirstItemGrouped)}",
            $"createdSortOrder={BoolText(report.CreatedSortOrder)}",
            $"filteredGroups={report.FilteredGroups}",
            $"todaySingleGroup={BoolText(report.TodaySingleGroup)}",
            $"gridHeaderGroups={report.GridHeaderGroups}",
            $"gridFirstItemGrouped={BoolText(report.GridFirstItemGrouped)}",
            $"gridTodaySingleGroup={BoolText(report.GridTodaySingleGroup)}",
            $"manualRangeHeaderGroups={report.ManualRangeHeaderGroups}",
            $"manualRangeListHeaders={BoolText(report.ManualRangeListHeaders)}",
            $"manualRangeGridHeaderGroups={report.ManualRangeGridHeaderGroups}",
            $"manualRangeGridHeaders={BoolText(report.ManualRangeGridHeaders)}",
            $"enhancementStateUnchanged={BoolText(report.EnhancementStateUnchanged)}",
            "browserRuntime=false",
            "localHttpServer=false",
            "nodeRuntime=false",
        });
    }

    private static string FormatEnhancedFilterSmokeReport(NativeEnhancedFilterSmokeReport report)
    {
        return string.Join(" ", new[]
        {
            "native-enhanced-filter-smoke complete",
            "runtime=winforms",
            $"folder=\"{Quote(report.Folder)}\"",
            $"totalImages={report.TotalImages}",
            $"initialVisible={report.InitialVisible}",
            $"enhancedSources={report.EnhancedSources}",
            $"enhancedMatches={report.EnhancedMatches}",
            $"enhancedSearchMatches={report.EnhancedSearchMatches}",
            $"enhancedFavoriteMatches={report.EnhancedFavoriteMatches}",
            $"clearMatches={report.ClearMatches}",
            $"enhancedOnlyFilter={BoolText(report.EnhancedOnlyFilter)}",
            $"enhancedSearchFilter={BoolText(report.EnhancedSearchFilter)}",
            $"enhancedFavoriteFilter={BoolText(report.EnhancedFavoriteFilter)}",
            $"clearFilter={BoolText(report.ClearFilter)}",
            $"enhancedFilterPersisted={BoolText(report.EnhancedFilterPersisted)}",
            $"enhancementStateUnchanged={BoolText(report.EnhancementStateUnchanged)}",
            "browserRuntime=false",
            "localHttpServer=false",
            "nodeRuntime=false",
        });
    }

    private static string BoolText(bool value)
    {
        return value.ToString().ToLowerInvariant();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:n1} {units[unit]}";
    }

    private int ParseSettingInt(string key, int defaultValue)
    {
        return int.TryParse(_store.GetSetting(key, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture)), out var value)
            ? value
            : defaultValue;
    }

    private sealed class DetailModalForm : Form
    {
        private readonly List<NativeImageRecord> _images;
        private readonly IReadOnlyDictionary<string, Keys> _keyBindings;
        private readonly Action<string> _openExternal;
        private readonly Action<string, int> _favoriteChanged;
        private readonly Func<string, bool> _addPromptTagToSearch;
        private readonly FlowLayoutPanel _toolbar = new();
        private readonly Panel _imageHost = new();
        private readonly PictureBox _imageBox = new();
        private readonly FlowLayoutPanel _promptTags = new();
        private readonly Label _metaLabel = new();
        private Image? _sourceImage;
        private int _index;
        private float _zoom = 1f;
        private bool _flipped;
        private bool _dragging;
        private Point _dragStart;
        private Point _scrollStart;

        public DetailModalForm(
            IReadOnlyList<NativeImageRecord> images,
            int startIndex,
            IReadOnlyDictionary<string, Keys> keyBindings,
            Action<string> openExternal,
            Action<string, int> favoriteChanged,
            Func<string, bool> addPromptTagToSearch)
        {
            if (images.Count == 0)
            {
                throw new ArgumentException("Detail modal requires at least one image.", nameof(images));
            }

            _images = images.ToList();
            _index = Math.Clamp(startIndex, 0, _images.Count - 1);
            _keyBindings = keyBindings;
            _openExternal = openExternal;
            _favoriteChanged = favoriteChanged;
            _addPromptTagToSearch = addPromptTagToSearch;

            Text = "PhotoViewer Detail";
            Width = 1040;
            Height = 760;
            MinimumSize = new Size(720, 480);
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterParent;

            BuildLayout();
            LoadCurrentImage();
        }

        public DetailSmokeReport RunSmoke()
        {
            var modalOpened = _imageBox.Image is not null && File.Exists(CurrentImage.AbsolutePath);
            var startIndex = _index;
            var navigation = true;
            if (_images.Count > 1)
            {
                SelectOffset(1);
                navigation = _index != startIndex;
                SelectOffset(-1);
            }

            var beforeZoom = _zoom;
            ZoomIn();
            var zoom = _zoom > beforeZoom;
            ResetView();
            var reset = !_flipped && Math.Abs(_zoom - CalculateFitZoom()) < 0.001f;
            ToggleFlip();
            var flip = _flipped;
            ToggleFlip();
            var pan = TryPanForSmoke();

            var favoriteBefore = CurrentImage.FavoriteLevel;
            var favoriteTarget = favoriteBefore < 5 ? favoriteBefore + 1 : Math.Max(0, favoriteBefore - 1);
            SetCurrentFavoriteLevel(favoriteTarget);
            var favorite = CurrentImage.FavoriteLevel == favoriteTarget;
            SetCurrentFavoriteLevel(favoriteBefore);

            var openExternal = File.Exists(GetOpenExternalPathForSmoke());
            var metadataDisplay = !HasMetadata(CurrentImage) ||
                (_metaLabel.Text.Contains("Prompt:", StringComparison.OrdinalIgnoreCase) &&
                    _metaLabel.Text.Contains("Negative prompt:", StringComparison.OrdinalIgnoreCase));
            var promptTags = SplitPromptTags(CurrentImage.Prompt).Count == 0 || _promptTags.Controls.Count > 0;
            return new DetailSmokeReport(modalOpened, navigation, zoom, reset, pan, flip, favorite, openExternal, metadataDisplay, promptTags);
        }

        public bool RunCustomKeyBindingSmoke(Keys keyData)
        {
            var beforeZoom = _zoom;
            var message = Message.Create(Handle, 0, IntPtr.Zero, IntPtr.Zero);
            var handled = ProcessCmdKey(ref message, keyData);
            return handled && _zoom > beforeZoom;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (TryDispatchKeyBinding(keyData))
            {
                return true;
            }

            switch (keyData)
            {
                case Keys.Right:
                    SelectOffset(1);
                    return true;
                case Keys.Left:
                    SelectOffset(-1);
                    return true;
                case Keys.Oemplus:
                case Keys.Add:
                    ZoomIn();
                    return true;
                case Keys.OemMinus:
                case Keys.Subtract:
                    ZoomOut();
                    return true;
                case Keys.D0:
                    ResetView();
                    return true;
                case Keys.F:
                    ToggleFlip();
                    return true;
                case Keys.Enter:
                    _openExternal(CurrentImage.AbsolutePath);
                    return true;
                case Keys.Control | Keys.Up:
                    SetCurrentFavoriteLevel(CurrentImage.FavoriteLevel + 1);
                    return true;
                case Keys.Control | Keys.Down:
                    SetCurrentFavoriteLevel(CurrentImage.FavoriteLevel - 1);
                    return true;
                case Keys.Escape:
                    Close();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool TryDispatchKeyBinding(Keys keyData)
        {
            if (IsTextEntryControl(ActiveControl))
            {
                return false;
            }

            var normalizedKeyData = NormalizeKeyData(keyData);
            foreach (var binding in _keyBindings)
            {
                if (binding.Value == normalizedKeyData)
                {
                    return ExecuteKeyBindingAction(binding.Key);
                }
            }

            return false;
        }

        private bool ExecuteKeyBindingAction(string action)
        {
            switch (action)
            {
                case "detailNext":
                    SelectOffset(1);
                    return true;
                case "detailPrevious":
                    SelectOffset(-1);
                    return true;
                case "detailZoomIn":
                    ZoomIn();
                    return true;
                case "detailZoomOut":
                    ZoomOut();
                    return true;
                case "detailReset":
                    ResetView();
                    return true;
                case "detailFlip":
                    ToggleFlip();
                    return true;
                case "detailOpenExternal":
                    _openExternal(CurrentImage.AbsolutePath);
                    return true;
                case "detailFavoriteUp":
                    SetCurrentFavoriteLevel(CurrentImage.FavoriteLevel + 1);
                    return true;
                case "detailFavoriteDown":
                    SetCurrentFavoriteLevel(CurrentImage.FavoriteLevel - 1);
                    return true;
                default:
                    return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeLoadedImages();
            }

            base.Dispose(disposing);
        }

        private NativeImageRecord CurrentImage => _images[_index];

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

            _toolbar.Dock = DockStyle.Fill;
            _toolbar.FlowDirection = FlowDirection.LeftToRight;
            _toolbar.WrapContents = false;
            _toolbar.Padding = new Padding(8, 6, 8, 4);

            AddToolbarButton("Previous", 82, () => SelectOffset(-1));
            AddToolbarButton("Next", 64, () => SelectOffset(1));
            AddToolbarButton("Zoom +", 72, ZoomIn);
            AddToolbarButton("Zoom -", 72, ZoomOut);
            AddToolbarButton("Reset", 64, ResetView);
            AddToolbarButton("Flip H", 64, ToggleFlip);
            AddToolbarButton("Open External", 112, () => _openExternal(CurrentImage.AbsolutePath));
            AddToolbarButton("Fav +", 62, () => SetCurrentFavoriteLevel(CurrentImage.FavoriteLevel + 1));
            AddToolbarButton("Fav -", 62, () => SetCurrentFavoriteLevel(CurrentImage.FavoriteLevel - 1));

            _imageHost.Dock = DockStyle.Fill;
            _imageHost.AutoScroll = true;
            _imageHost.BackColor = Color.FromArgb(16, 16, 16);

            _imageBox.SizeMode = PictureBoxSizeMode.StretchImage;
            _imageBox.Location = new Point(0, 0);
            _imageBox.MouseDown += ImageBoxMouseDown;
            _imageBox.MouseMove += ImageBoxMouseMove;
            _imageBox.MouseUp += (_, _) => _dragging = false;
            _imageHost.Controls.Add(_imageBox);

            _promptTags.Dock = DockStyle.Fill;
            _promptTags.FlowDirection = FlowDirection.LeftToRight;
            _promptTags.WrapContents = false;
            _promptTags.AutoScroll = true;
            _promptTags.Padding = new Padding(8, 4, 8, 2);

            _metaLabel.Dock = DockStyle.Fill;
            _metaLabel.Padding = new Padding(8, 4, 8, 4);
            _metaLabel.AutoEllipsis = false;
            _metaLabel.TextAlign = ContentAlignment.MiddleLeft;

            root.Controls.Add(_toolbar, 0, 0);
            root.Controls.Add(_imageHost, 0, 1);
            root.Controls.Add(_promptTags, 0, 2);
            root.Controls.Add(_metaLabel, 0, 3);
            Controls.Add(root);
        }

        private void AddToolbarButton(string text, int width, Action action)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 28,
            };
            button.Click += (_, _) => action();
            _toolbar.Controls.Add(button);
        }

        private void LoadCurrentImage()
        {
            DisposeLoadedImages();
            var image = CurrentImage;
            try
            {
                _sourceImage = LoadImageCopy(image.AbsolutePath);
                _flipped = false;
                _zoom = CalculateFitZoom();
                ApplyDisplayImage();
                UpdatePromptTags();
                UpdateMeta();
            }
            catch (Exception ex)
            {
                _metaLabel.Text = $"Detail load failed: {ex.Message}";
            }
        }

        private void DisposeLoadedImages()
        {
            var display = _imageBox.Image;
            _imageBox.Image = null;
            display?.Dispose();
            _sourceImage?.Dispose();
            _sourceImage = null;
        }

        private void SelectOffset(int offset)
        {
            if (_images.Count == 0)
            {
                return;
            }

            _index = (_index + offset + _images.Count) % _images.Count;
            LoadCurrentImage();
        }

        private void ZoomIn()
        {
            SetZoom(_zoom * 1.25f);
        }

        private void ZoomOut()
        {
            SetZoom(_zoom / 1.25f);
        }

        private void SetZoom(float zoom)
        {
            _zoom = Math.Clamp(zoom, 0.05f, 8f);
            ApplyDisplayImage();
            UpdateMeta();
        }

        private void ResetView()
        {
            _flipped = false;
            _zoom = CalculateFitZoom();
            ApplyDisplayImage();
            _imageHost.AutoScrollPosition = Point.Empty;
            UpdateMeta();
        }

        private void ToggleFlip()
        {
            _flipped = !_flipped;
            ApplyDisplayImage();
            UpdateMeta();
        }

        private void ApplyDisplayImage()
        {
            if (_sourceImage is null)
            {
                return;
            }

            var display = new Bitmap(_sourceImage);
            if (_flipped)
            {
                display.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }

            var old = _imageBox.Image;
            _imageBox.Image = display;
            old?.Dispose();
            _imageBox.Size = new Size(
                Math.Max(1, (int)Math.Round(display.Width * _zoom)),
                Math.Max(1, (int)Math.Round(display.Height * _zoom)));
        }

        private float CalculateFitZoom()
        {
            if (_sourceImage is null)
            {
                return 1f;
            }

            var width = Math.Max(1, _imageHost.ClientSize.Width - 8);
            var height = Math.Max(1, _imageHost.ClientSize.Height - 8);
            var fit = Math.Min(width / (float)_sourceImage.Width, height / (float)_sourceImage.Height);
            if (float.IsNaN(fit) || float.IsInfinity(fit) || fit <= 0)
            {
                return 1f;
            }

            return Math.Clamp(Math.Min(1f, fit), 0.05f, 1f);
        }

        private bool TryPanForSmoke()
        {
            if (_imageBox.Image is null)
            {
                return false;
            }

            SetZoom(Math.Max(_zoom, 4f));
            _imageHost.AutoScrollPosition = new Point(24, 24);
            return _imageHost.AutoScroll && _imageBox.Image is not null;
        }

        private string GetOpenExternalPathForSmoke()
        {
            return CurrentImage.AbsolutePath;
        }

        private void SetCurrentFavoriteLevel(int level)
        {
            var clamped = Math.Clamp(level, 0, 5);
            var current = CurrentImage;
            var updated = current with { FavoriteLevel = clamped };
            _images[_index] = updated;
            _favoriteChanged(updated.AbsolutePath, clamped);
            UpdateMeta();
        }

        private void UpdatePromptTags()
        {
            _promptTags.SuspendLayout();
            try
            {
                _promptTags.Controls.Clear();
                foreach (var tag in SplitPromptTags(CurrentImage.Prompt))
                {
                    var button = new Button
                    {
                        Text = tag,
                        Tag = tag,
                        Width = Math.Clamp(32 + (tag.Length * 7), 64, 180),
                        Height = 28,
                        AutoEllipsis = true,
                    };
                    button.Click += (_, _) =>
                    {
                        if (_addPromptTagToSearch((string)button.Tag!))
                        {
                            Close();
                        }
                    };
                    _promptTags.Controls.Add(button);
                }

                _promptTags.Visible = _promptTags.Controls.Count > 0;
            }
            finally
            {
                _promptTags.ResumeLayout();
            }
        }

        private void UpdateMeta()
        {
            var image = CurrentImage;
            var dimensions = FormatDimensions(image);
            var zoomText = $"{_zoom * 100f:0}%";
            var flipText = _flipped ? "flipped" : "normal";
            var baseText = $"{_index + 1:n0}/{_images.Count:n0}  {image.Filename}  {FormatBytes(image.SizeBytes)}  {dimensions}  fav {image.FavoriteLevel}  zoom {zoomText}  {flipText}";
            var metadata = FormatMetadataDisplay(image, maxValueLength: 96);
            _metaLabel.Text = string.IsNullOrWhiteSpace(metadata) ? baseText : $"{baseText}\r\n{metadata}";
        }

        private void ImageBoxMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _dragging = true;
            _dragStart = e.Location;
            _scrollStart = new Point(_imageHost.HorizontalScroll.Value, _imageHost.VerticalScroll.Value);
        }

        private void ImageBoxMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            var dx = e.Location.X - _dragStart.X;
            var dy = e.Location.Y - _dragStart.Y;
            _imageHost.AutoScrollPosition = new Point(
                Math.Max(0, _scrollStart.X - dx),
                Math.Max(0, _scrollStart.Y - dy));
        }
    }

    private sealed class NativeSettingsDialog : Form
    {
        private readonly NativeSettingsSnapshot _snapshot;
        private readonly Dictionary<string, TextBox> _bindingInputs = new(StringComparer.OrdinalIgnoreCase);

        public string? KeyBindingsJson { get; private set; }

        public NativeSettingsDialog(NativeSettingsSnapshot snapshot)
        {
            _snapshot = snapshot;
            Text = "Native Settings";
            Width = 760;
            Height = 520;
            MinimumSize = new Size(620, 420);
            StartPosition = FormStartPosition.CenterParent;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10),
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            var text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = string.Join(Environment.NewLine, new[]
                {
                    $"Native SQLite: {snapshot.DatabasePath}",
                    $"Browser settings imported: {(snapshot.BrowserSettingsImported ? "yes" : "no")}",
                    $"Import warnings: {snapshot.ImportWarningCount:n0}",
                    $"Recovery: {(string.IsNullOrWhiteSpace(snapshot.ImportRecoverySummary) ? "none" : snapshot.ImportRecoverySummary)}",
                    $"Keybinding mode: {snapshot.KeyBindingMode}",
                }),
            };

            var bindings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 2,
                RowCount = EditableKeyBindingActions.Length,
            };
            bindings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            bindings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var bindingValues = ExtractKeyBindingTextMap(snapshot.KeyBindingsJson);
            var defaultValues = ExtractKeyBindingTextMap(NativeImageStore.DefaultKeyBindingsJson);
            for (var i = 0; i < EditableKeyBindingActions.Length; i++)
            {
                var action = EditableKeyBindingActions[i];
                var label = new Label
                {
                    Text = action.Label,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                };
                var input = new TextBox
                {
                    Dock = DockStyle.Top,
                    ReadOnly = true,
                    Tag = action.Key,
                    Text = bindingValues.TryGetValue(action.Key, out var currentValue)
                        ? currentValue
                        : defaultValues.GetValueOrDefault(action.Key, ""),
                };
                input.KeyDown += (_, args) =>
                {
                    if (TryFormatKeyBinding(args.KeyData, out var normalized))
                    {
                        input.Text = normalized;
                    }

                    args.Handled = true;
                    args.SuppressKeyPress = true;
                };
                _bindingInputs[action.Key] = input;
                bindings.Controls.Add(label, 0, i);
                bindings.Controls.Add(input, 1, i);
            }

            var save = new Button
            {
                Text = "Save",
                Width = 92,
                Height = 28,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
            };
            save.Click += (_, _) => SaveAndClose();

            var reset = new Button
            {
                Text = "Reset",
                Width = 92,
                Height = 28,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
            };
            reset.Click += (_, _) => ResetDefaults();

            var cancel = new Button
            {
                Text = "Cancel",
                Width = 92,
                Height = 28,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                DialogResult = DialogResult.Cancel,
            };

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
            };
            actions.Controls.Add(save);
            actions.Controls.Add(cancel);
            actions.Controls.Add(reset);

            root.Controls.Add(text, 0, 0);
            root.Controls.Add(bindings, 0, 1);
            root.Controls.Add(actions, 0, 2);
            Controls.Add(root);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void SaveAndClose()
        {
            var updates = _bindingInputs.ToDictionary(
                static item => item.Key,
                static item => item.Value.Text,
                StringComparer.OrdinalIgnoreCase);
            if (!TryBuildKeyBindingsJson(_snapshot.KeyBindingsJson, updates, out var json, out var error))
            {
                MessageBox.Show(this, error, "Invalid key binding", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            KeyBindingsJson = json;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ResetDefaults()
        {
            var defaults = ExtractKeyBindingTextMap(NativeImageStore.DefaultKeyBindingsJson);
            foreach (var action in EditableKeyBindingActions)
            {
                _bindingInputs[action.Key].Text = defaults.GetValueOrDefault(action.Key, "");
            }
        }
    }

    private sealed record DetailSmokeReport(
        bool ModalOpened,
        bool Navigation,
        bool Zoom,
        bool Reset,
        bool Pan,
        bool Flip,
        bool Favorite,
        bool OpenExternal,
        bool MetadataDisplay,
        bool PromptTags);

    private sealed record NativeSettingsSnapshot(
        string DatabasePath,
        bool BrowserSettingsImported,
        string KeyBindingsJson,
        string KeyBindingMode,
        int ImportWarningCount,
        string ImportRecoverySummary);

    private sealed record FolderBucket(string Folder, string Label, int Count)
    {
        public override string ToString()
        {
            return $"{Label} ({Count:n0})";
        }
    }

    private sealed record FavoriteFilterOption(string Key, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record DateFilterOption(string Key, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record SearchTagSuggestion(string Tag, int Count)
    {
        public override string ToString()
        {
            return $"{Tag} ({Count:n0})";
        }
    }

    private sealed record SearchTagSuggestionAccumulator(string Tag, int Count);

    private sealed record PreviewTabState(string Path, string Filename, bool IsPinned);

    private sealed record KeyBindingAction(string Key, string Label, string Surface);

    private sealed record NativeUiScreenshotReport(
        string Folder,
        string OutputPath,
        int Width,
        int Height,
        int ScannedImages,
        int VisibleImages,
        bool PreviewLoaded,
        int TextFitWarnings,
        int OverlapWarnings,
        string FocusControl,
        bool EnhancementStateUnchanged);

    private sealed record NativeUiSmokeReport(
        string Folder,
        int ScannedImages,
        int InitialVisible,
        bool PreviewLoaded,
        bool PreviewTabs,
        bool PreviewTabPin,
        bool PreviewTabClose,
        bool PreviewTabRestore,
        bool HoverQuickPreview,
        bool NavigationButtons,
        bool KeyboardNavigation,
        bool KeyboardFavorite,
        bool GridToggle,
        int FolderBuckets,
        bool FolderHideAll,
        bool FolderSortMode,
        bool SortName,
        bool RandomReshuffle,
        bool ThumbnailSize,
        bool GalleryWheelZoom,
        bool GalleryKeyboardZoom,
        bool DisplayModes,
        bool DisplayModeVisuals,
        string CompactGridImageSize,
        string PosterGridImageSize,
        bool AspectModes,
        bool PreviewToggle,
        bool DetailsToggle,
        bool PreviewSplitter,
        bool SelectedCount,
        bool GalleryStateRestore,
        bool MultiSelection,
        bool BulkFavoriteSet,
        bool BulkFavoriteClear,
        bool BulkOpenFiles,
        bool BulkOpenFolders,
        bool BackgroundClear,
        bool FavoriteFilterCounts,
        bool FavoriteLevelFilter,
        bool UnratedFilter,
        bool EnhancedOnlyFilter,
        bool ClearSearch,
        bool FolderShowSelected,
        bool FolderHideSelected,
        bool FolderRangeSelection,
        bool FolderKeyboardRangeSelection,
        bool FolderClearSelection,
        bool DetailModal,
        bool DetailNavigation,
        bool DetailZoom,
        bool DetailReset,
        bool DetailPan,
        bool DetailFlip,
        bool DetailFavorite,
        bool DetailOpenExternal,
        bool MetadataDisplay,
        bool MetadataCopy,
        bool PromptTagAction,
        bool SearchChips,
        bool SearchChipCommit,
        bool SearchSuggestion,
        bool KeybindingRecorder,
        bool KeybindingConflictRejected,
        bool KeybindingRuntime,
        bool DetailKeybindingRuntime,
        int SearchMatches,
        int FavoriteMatches,
        bool NoResultsState,
        bool FolderErrorState,
        int Albums,
        int AlbumImages,
        int BrowserStateKeys,
        bool SettingsImported,
        bool EnhancementStateUnchanged);

    private sealed record NativeFolderSetSmokeReport(
        int Roots,
        int RemovedRoots,
        int FolderBuckets,
        int ImagesBeforeRemove,
        int ImagesAfterRemove,
        int SearchMatches,
        bool RecentSetPersisted,
        bool RemoveFolder,
        bool OpenRecentSet,
        bool ManualRefreshAdded,
        bool ManualRefreshRemoved,
        bool WatcherRoots,
        bool EnhancementStateUnchanged);

    private sealed record NativeLargeScrollSmokeReport(
        string Folder,
        int TotalImages,
        int InitialVisible,
        int TargetIndex,
        int RestoredIndex,
        int TopIndexBeforeRestore,
        int TopIndexAfterRestore,
        bool VirtualMode,
        int VirtualListSize,
        bool StatePersisted,
        bool RestoreSelected,
        bool EnsureVisible,
        bool VisibleBeforeRestore,
        bool GalleryZoomSelectedAnchor,
        bool GalleryZoomCenterAnchor,
        bool EnhancementStateUnchanged);

    private sealed record NativeDateFilterSmokeReport(
        string Folder,
        int TotalImages,
        int InitialVisible,
        int TodayMatches,
        int Last7Matches,
        int Last30Matches,
        int ThisYearMatches,
        int ManualRangeMatches,
        int ManualFromOnlyMatches,
        int ManualToOnlyMatches,
        int ManualSearchMatches,
        int ManualFavoriteMatches,
        int ClearMatches,
        bool TodayFilter,
        bool Last7Filter,
        bool Last30Filter,
        bool ThisYearFilter,
        bool ManualRangeFilter,
        bool ManualFromOnlyFilter,
        bool ManualToOnlyFilter,
        bool ManualSearchFilter,
        bool ManualFavoriteFilter,
        bool ManualRangePersisted,
        bool ClearFilter,
        bool DateFilterPersisted,
        bool EnhancementStateUnchanged);

    private sealed record NativeEnhancedFilterSmokeReport(
        string Folder,
        int TotalImages,
        int InitialVisible,
        int EnhancedSources,
        int EnhancedMatches,
        int EnhancedSearchMatches,
        int EnhancedFavoriteMatches,
        int ClearMatches,
        bool EnhancedOnlyFilter,
        bool EnhancedSearchFilter,
        bool EnhancedFavoriteFilter,
        bool ClearFilter,
        bool EnhancedFilterPersisted,
        bool EnhancementStateUnchanged);

    private sealed record NativeDateSectionSmokeReport(
        string Folder,
        int TotalImages,
        int InitialVisible,
        int HeaderGroups,
        string FirstHeader,
        bool ShowDateHeaders,
        bool FirstItemGrouped,
        bool CreatedSortOrder,
        int FilteredGroups,
        bool TodaySingleGroup,
        int GridHeaderGroups,
        bool GridFirstItemGrouped,
        bool GridTodaySingleGroup,
        int ManualRangeHeaderGroups,
        bool ManualRangeListHeaders,
        int ManualRangeGridHeaderGroups,
        bool ManualRangeGridHeaders,
        bool EnhancementStateUnchanged);
}
