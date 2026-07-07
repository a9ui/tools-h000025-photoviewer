using System.Diagnostics;
using System.Drawing;
using Microsoft.VisualBasic.FileIO;

namespace PhotoViewer.Native;

internal sealed class MainForm : Form
{
    private readonly TextBox _folderText = new();
    private readonly Button _browseButton = new();
    private readonly Button _scanButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _importButton = new();
    private readonly TextBox _searchText = new();
    private readonly CheckBox _favoritesOnly = new();
    private readonly ComboBox _viewMode = new();
    private readonly ComboBox _sortMode = new();
    private readonly Button _reshuffleButton = new();
    private readonly NumericUpDown _thumbnailSize = new();
    private readonly CheckBox _previewVisible = new();
    private readonly CheckBox _detailsVisible = new();
    private readonly CheckedListBox _folderBuckets = new();
    private readonly Button _showAllFoldersButton = new();
    private readonly Button _hideAllFoldersButton = new();
    private readonly Button _invertFoldersButton = new();
    private readonly Label _folderBucketLabel = new();
    private readonly Button _previousButton = new();
    private readonly Button _nextButton = new();
    private readonly Button _detailButton = new();
    private readonly NumericUpDown _favoriteLevel = new();
    private readonly Button _openFileButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _settingsButton = new();
    private readonly Label _stateLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ListView _list = new();
    private readonly PictureBox _preview = new();
    private readonly Label _selectionLabel = new();
    private readonly Label _previewLabel = new();
    private readonly ImageList _gridImages = new();
    private SplitContainer? _mainSplit;

    private readonly string _projectRoot;
    private readonly NativeImageStore _store;
    private readonly NativePreviewRingBuffer _previewRing = new(capacity: 5);
    private readonly NativeCacheScheduler _cacheScheduler = new();
    private readonly NativeFolderWatcher _folderWatcher = new();
    private Dictionary<string, int> _favorites;
    private List<NativeImageRecord> _allImages = [];
    private List<NativeImageRecord> _visibleImages = [];
    private string _currentFolder = "";
    private CancellationTokenSource? _scanCancellation;
    private long _previewVersion;
    private bool _updatingFavoriteControl;
    private bool _updatingFolderBuckets;
    private bool _updatingThumbnailSize;
    private int _randomSortSeed = Environment.TickCount;

    public MainForm(string? initialFolder)
    {
        Text = "PhotoViewer Local Native";
        Width = 1280;
        Height = 820;
        MinimumSize = new Size(900, 560);
        KeyPreview = true;

        _projectRoot = NativeStateBridge.ResolveProjectRoot();
        _store = new NativeImageStore(_projectRoot);
        _store.Initialize();
        var report = _store.ImportProjectState();
        _favorites = _store.LoadFavorites();

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
        ApplyViewMode(_store.GetSetting("view_mode", "details"));
        ApplySortMode(_store.GetSetting("sort_mode", "Modified"));
        ApplyThumbnailSize(ParseSettingInt("thumbnail_size", 96));
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
                Console.WriteLine(
                    $"native-ui-smoke complete runtime=winforms folder=\"{Quote(report.Folder)}\" scannedImages={report.ScannedImages} initialVisible={report.InitialVisible} previewLoaded={report.PreviewLoaded.ToString().ToLowerInvariant()} navigationButtons={report.NavigationButtons.ToString().ToLowerInvariant()} keyboardNavigation={report.KeyboardNavigation.ToString().ToLowerInvariant()} keyboardFavorite={report.KeyboardFavorite.ToString().ToLowerInvariant()} gridToggle={report.GridToggle.ToString().ToLowerInvariant()} folderBuckets={report.FolderBuckets} folderHideAll={report.FolderHideAll.ToString().ToLowerInvariant()} sortName={report.SortName.ToString().ToLowerInvariant()} randomReshuffle={report.RandomReshuffle.ToString().ToLowerInvariant()} thumbnailSize={report.ThumbnailSize.ToString().ToLowerInvariant()} previewToggle={report.PreviewToggle.ToString().ToLowerInvariant()} detailsToggle={report.DetailsToggle.ToString().ToLowerInvariant()} previewSplitter={report.PreviewSplitter.ToString().ToLowerInvariant()} selectedCount={report.SelectedCount.ToString().ToLowerInvariant()} detailModal={report.DetailModal.ToString().ToLowerInvariant()} detailNavigation={report.DetailNavigation.ToString().ToLowerInvariant()} detailZoom={report.DetailZoom.ToString().ToLowerInvariant()} detailReset={report.DetailReset.ToString().ToLowerInvariant()} detailPan={report.DetailPan.ToString().ToLowerInvariant()} detailFlip={report.DetailFlip.ToString().ToLowerInvariant()} detailFavorite={report.DetailFavorite.ToString().ToLowerInvariant()} detailOpenExternal={report.DetailOpenExternal.ToString().ToLowerInvariant()} settingsReadOnly={report.SettingsReadOnly.ToString().ToLowerInvariant()} searchMatches={report.SearchMatches} favoriteMatches={report.FavoriteMatches} noResultsState={report.NoResultsState.ToString().ToLowerInvariant()} folderErrorState={report.FolderErrorState.ToString().ToLowerInvariant()} albums={report.Albums} albumImages={report.AlbumImages} browserStateKeys={report.BrowserStateKeys} settingsImported={report.SettingsImported.ToString().ToLowerInvariant()} enhancementStateUnchanged={report.EnhancementStateUnchanged.ToString().ToLowerInvariant()} browserRuntime=false localHttpServer=false nodeRuntime=false");
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
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            Padding = new Padding(8, 6, 8, 4),
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));

        _folderText.Dock = DockStyle.Fill;
        _folderText.PlaceholderText = "Image folder path";

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
        _searchText.TextChanged += (_, _) =>
        {
            ApplyFilter();
            SaveViewState();
        };

        _favoritesOnly.Text = "Favorites";
        _favoritesOnly.Dock = DockStyle.Fill;
        _favoritesOnly.CheckedChanged += (_, _) =>
        {
            ApplyFilter();
            SaveViewState();
        };

        _stateLabel.Dock = DockStyle.Fill;
        _stateLabel.TextAlign = ContentAlignment.MiddleRight;
        _stateLabel.AutoEllipsis = true;

        toolbar.Controls.Add(_folderText, 0, 0);
        toolbar.Controls.Add(_browseButton, 1, 0);
        toolbar.Controls.Add(_scanButton, 2, 0);
        toolbar.Controls.Add(_cancelButton, 3, 0);
        toolbar.Controls.Add(_importButton, 4, 0);
        toolbar.Controls.Add(_searchText, 5, 0);
        toolbar.Controls.Add(_favoritesOnly, 6, 0);
        toolbar.Controls.Add(_stateLabel, 8, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 4, 8, 2),
        };

        _viewMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _viewMode.Width = 96;
        _viewMode.Items.AddRange(["List", "Grid"]);
        _viewMode.SelectedIndexChanged += (_, _) =>
        {
            ApplyViewMode(_viewMode.SelectedItem?.ToString() == "Grid" ? "grid" : "details");
            SaveViewState();
        };

        _previousButton.Text = "Previous";
        _previousButton.Width = 82;
        _previousButton.Click += (_, _) => SelectOffset(-1);

        _nextButton.Text = "Next";
        _nextButton.Width = 82;
        _nextButton.Click += (_, _) => SelectOffset(1);

        _detailButton.Text = "Detail";
        _detailButton.Width = 72;
        _detailButton.Click += (_, _) => ShowDetailModal();

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

        _openFileButton.Text = "Open File";
        _openFileButton.Width = 86;
        _openFileButton.Click += (_, _) => OpenSelectedFile();

        _openFolderButton.Text = "Open Folder";
        _openFolderButton.Width = 96;
        _openFolderButton.Click += (_, _) => OpenSelectedFolder();

        _deleteButton.Text = "Recycle";
        _deleteButton.Width = 76;
        _deleteButton.Click += (_, _) => DeleteSelectedImage();

        _settingsButton.Text = "Settings";
        _settingsButton.Width = 82;
        _settingsButton.Click += (_, _) => ShowNativeSettings();

        actions.Controls.Add(_viewMode);
        actions.Controls.Add(_previousButton);
        actions.Controls.Add(_nextButton);
        actions.Controls.Add(_detailButton);
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
            WrapContents = false,
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
        _sortMode.Width = 108;
        _sortMode.Items.AddRange(["Modified", "Created", "Name", "Folder", "Size", "Favorite", "Random"]);
        _sortMode.SelectedIndexChanged += (_, _) =>
        {
            SaveSortState();
            ApplyFilter();
        };

        _reshuffleButton.Text = "Reshuffle";
        _reshuffleButton.Width = 82;
        _reshuffleButton.Click += (_, _) => ReshuffleSort();

        var thumbLabel = new Label
        {
            Text = "Thumb",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(12, 6, 0, 0),
        };

        _thumbnailSize.Minimum = 64;
        _thumbnailSize.Maximum = 192;
        _thumbnailSize.Increment = 16;
        _thumbnailSize.Width = 58;
        _thumbnailSize.ValueChanged += (_, _) =>
        {
            if (_updatingThumbnailSize)
            {
                return;
            }

            ApplyThumbnailSize((int)_thumbnailSize.Value);
            _store.SaveSetting("thumbnail_size", ((int)_thumbnailSize.Value).ToString(System.Globalization.CultureInfo.InvariantCulture));
        };

        _previewVisible.Text = "Preview";
        _previewVisible.Width = 78;
        _previewVisible.Checked = true;
        _previewVisible.CheckedChanged += (_, _) =>
        {
            ApplyPreviewVisibility();
            _store.SaveSetting("preview_visible", _previewVisible.Checked ? "1" : "0");
        };

        _detailsVisible.Text = "Details";
        _detailsVisible.Width = 72;
        _detailsVisible.Checked = true;
        _detailsVisible.CheckedChanged += (_, _) =>
        {
            ApplyDetailsVisibility();
            _store.SaveSetting("preview_details_visible", _detailsVisible.Checked ? "1" : "0");
        };

        displayControls.Controls.Add(sortLabel);
        displayControls.Controls.Add(_sortMode);
        displayControls.Controls.Add(_reshuffleButton);
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
        _list.MultiSelect = false;
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
        folderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        folderPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        folderPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        _folderBucketLabel.Dock = DockStyle.Fill;
        _folderBucketLabel.Text = "Folders";
        _folderBucketLabel.TextAlign = ContentAlignment.MiddleLeft;
        _folderBucketLabel.AutoEllipsis = true;

        _folderBuckets.Dock = DockStyle.Fill;
        _folderBuckets.CheckOnClick = true;
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

        var folderActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        _showAllFoldersButton.Text = "All";
        _showAllFoldersButton.Width = 54;
        _showAllFoldersButton.Click += (_, _) => SetAllFolderBuckets(visible: true);

        _hideAllFoldersButton.Text = "None";
        _hideAllFoldersButton.Width = 58;
        _hideAllFoldersButton.Click += (_, _) => SetAllFolderBuckets(visible: false);

        _invertFoldersButton.Text = "Invert";
        _invertFoldersButton.Width = 64;
        _invertFoldersButton.Click += (_, _) => InvertFolderBuckets();

        folderActions.Controls.Add(_showAllFoldersButton);
        folderActions.Controls.Add(_hideAllFoldersButton);
        folderActions.Controls.Add(_invertFoldersButton);

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
            RowCount = 3,
        };
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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

        previewPanel.Controls.Add(_preview, 0, 0);
        previewPanel.Controls.Add(_selectionLabel, 0, 1);
        previewPanel.Controls.Add(_previewLabel, 0, 2);
        split.Panel2.Controls.Add(previewPanel);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Padding = new Padding(8, 0, 8, 0);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Text = "Ready.";

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(displayControls, 0, 2);
        root.Controls.Add(split, 0, 3);
        root.Controls.Add(_statusLabel, 0, 4);
        Controls.Add(root);
        ApplyThumbnailSize(96);
        UpdateSelectionActions();
    }

    private async Task<NativeUiSmokeReport> RunUiSmokeScenarioAsync(string folder, string searchQuery)
    {
        var beforeEnhancementState = EnhancementStateFingerprint();

        _folderText.Text = folder;
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
        SetAllFolderBuckets(visible: false);
        var folderHideAll = _visibleImages.Count == 0;
        Require(folderHideAll, "folder hide-all did not filter visible images");
        SetAllFolderBuckets(visible: true);
        Require(_visibleImages.Count == initialVisible, "folder show-all did not restore visible images");

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

        var navigationButtons = _nextButton.Enabled;

        await LoadSelectedPreviewAsync();
        var previewLoaded = _preview.Image is not null && _previewLabel.Text.Contains(".png", StringComparison.OrdinalIgnoreCase);
        Require(previewLoaded, "preview did not load fixture image");

        var selectedCount = _selectionLabel.Text.Contains("Selected 1", StringComparison.OrdinalIgnoreCase);
        Require(selectedCount, "selected count label failed");

        var detailReport = RunDetailModalSmoke();
        Require(detailReport.ModalOpened, "detail modal did not load image");
        Require(detailReport.Navigation, "detail modal navigation failed");
        Require(detailReport.Zoom, "detail modal zoom failed");
        Require(detailReport.Reset, "detail modal reset failed");
        Require(detailReport.Pan, "detail modal pan failed");
        Require(detailReport.Flip, "detail modal flip failed");
        Require(detailReport.Favorite, "detail modal favorite control failed");
        Require(detailReport.OpenExternal, "detail modal open-external target failed");

        var settingsSnapshot = BuildNativeSettingsSnapshot();
        var settingsReadOnly = settingsSnapshot.KeyBindingMode.Contains("read-only", StringComparison.OrdinalIgnoreCase)
            && settingsSnapshot.KeyBindingsJson.Contains("openDetail", StringComparison.OrdinalIgnoreCase)
            && settingsSnapshot.KeyBindingsJson.Contains("detailZoomIn", StringComparison.OrdinalIgnoreCase);
        Require(settingsReadOnly, "settings read-only keybinding decision missing");

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
        var gridHandled = ProcessCmdKey(ref keyboardMessage, Keys.Control | Keys.G);
        var gridToggle = gridHandled && _list.View == View.LargeIcon;
        Require(gridToggle, "keyboard grid toggle failed");
        ApplyViewMode("details");

        _favoritesOnly.Checked = false;
        _searchText.Text = searchQuery;
        ApplyFilter();
        var searchMatches = _visibleImages.Count;
        Require(searchMatches > 0, "search produced no fixture matches");

        _favoritesOnly.Checked = true;
        ApplyFilter();
        var favoriteMatches = _visibleImages.Count;
        Require(favoriteMatches > 0, "favorites filter produced no matches");

        _favoritesOnly.Checked = false;
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
        ApplyFilter();

        var afterEnhancementState = EnhancementStateFingerprint();
        Require(beforeEnhancementState == afterEnhancementState, "enhancement state changed during native UI smoke");
        return new NativeUiSmokeReport(
            Folder: folder,
            ScannedImages: scannedImages,
            InitialVisible: initialVisible,
            PreviewLoaded: previewLoaded,
            NavigationButtons: navigationButtons,
            KeyboardNavigation: keyboardNavigation,
            KeyboardFavorite: keyboardFavorite,
            GridToggle: gridToggle,
            FolderBuckets: folderBuckets,
            FolderHideAll: folderHideAll,
            SortName: sortName,
            RandomReshuffle: randomReshuffle,
            ThumbnailSize: thumbnailSize,
            PreviewToggle: previewToggle,
            DetailsToggle: detailsToggle,
            PreviewSplitter: previewSplitter,
            SelectedCount: selectedCount,
            DetailModal: detailReport.ModalOpened,
            DetailNavigation: detailReport.Navigation,
            DetailZoom: detailReport.Zoom,
            DetailReset: detailReport.Reset,
            DetailPan: detailReport.Pan,
            DetailFlip: detailReport.Flip,
            DetailFavorite: detailReport.Favorite,
            DetailOpenExternal: detailReport.OpenExternal,
            SettingsReadOnly: settingsReadOnly,
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

    private void ApplyStateSummary(NativeImportReport? report = null)
    {
        report ??= _store.ImportProjectState();
        _stateLabel.Text = $"db {report.ImageCount:n0} / fav {report.FavoriteCount:n0} / albums {report.AlbumCount:n0}/{report.AlbumImageCount:n0} / pvu {report.BrowserStateKeyCount:n0}";
    }

    private void ImportState()
    {
        var report = _store.ImportProjectState();
        _favorites = _store.LoadFavorites();
        _allImages = ReapplyFavorites(_allImages);
        BuildFolderBuckets();
        ApplyFilter();
        ApplyStateSummary(report);
        SetStatus($"Imported state: {report.FavoriteCount:n0} favorites, {report.AlbumCount:n0} albums, {report.AlbumImageCount:n0} album images, {report.BrowserStateKeyCount:n0} pvu keys, db {report.ImageCount:n0} images.");
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select an image folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_folderText.Text) ? _folderText.Text : _projectRoot,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _folderText.Text = dialog.SelectedPath;
            _store.SaveSetting("recent_folder", dialog.SelectedPath);
        }
    }

    private async Task ScanCurrentFolderAsync()
    {
        var folder = _folderText.Text.Trim();
        if (!Directory.Exists(folder))
        {
            SetStatus("Folder not found.");
            return;
        }

        _currentFolder = folder;
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
            var existing = _store.LoadImagesForRoot(folder).ToDictionary(static item => item.AbsolutePath, StringComparer.OrdinalIgnoreCase);
            if (existing.Count > 0)
            {
                var incremental = await NativeIncrementalScanner.ScanAsync(folder, existing, _favorites, progress, _scanCancellation.Token);
                stopwatch.Stop();
                _store.ApplyIncrementalScan(folder, incremental, stopwatch.Elapsed, fullRescan: false);
                _allImages = _store.LoadImagesForRoot(folder);
                BuildFolderBuckets();
                ApplyFilter();
                ApplyStateSummary();
                SetStatus(
                    $"Incremental scan: {_allImages.Count:n0} images, {incremental.AddedOrUpdated.Count:n0} changed, {incremental.RemovedPaths.Count:n0} removed, {incremental.UnchangedCount:n0} unchanged in {stopwatch.Elapsed.TotalSeconds:n1}s.");
            }
            else
            {
                var scanned = await NativeImageScanner.ScanAsync(folder, _favorites, progress, _scanCancellation.Token);
                stopwatch.Stop();
                _store.SaveScanResult(folder, scanned, stopwatch.Elapsed);
                _allImages = _store.LoadImagesForRoot(folder);
                BuildFolderBuckets();
                ApplyFilter();
                ApplyStateSummary();
                SetStatus($"Scan complete: {_allImages.Count:n0} images in {stopwatch.Elapsed.TotalSeconds:n1}s. Saved to {_store.DatabasePath}");
            }

            _folderWatcher.Watch(folder);
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
        if (!string.Equals(_currentFolder, folder, StringComparison.OrdinalIgnoreCase) || !_scanButton.Enabled)
        {
            return;
        }

        _folderText.Text = folder;
        await ScanCurrentFolderAsync();
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
        var query = _searchText.Text.Trim();
        if (!string.IsNullOrWhiteSpace(_currentFolder) && Directory.Exists(_currentFolder) && _allImages.Count > 0)
        {
            _visibleImages = ApplySort(ApplyFolderBucketFilter(_store.SearchImagesIndexed(_currentFolder, query, _favoritesOnly.Checked, limit: 100_000))).ToList();
            _list.VirtualListSize = _visibleImages.Count;
            _list.Invalidate();
            if (_visibleImages.Count > 0 && _list.SelectedIndices.Count == 0)
            {
                _list.SelectedIndices.Add(0);
            }

            UpdateSelectionActions();
            SetStatus($"Showing {_visibleImages.Count:n0} / {_allImages.Count:n0} images (indexed search).");
            return;
        }

        IEnumerable<NativeImageRecord> source = _allImages;
        if (_favoritesOnly.Checked)
        {
            source = source.Where(static item => item.FavoriteLevel > 0);
        }

        if (query.Length > 0)
        {
            source = source.Where(item =>
                item.Filename.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Folder.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.AbsolutePath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _visibleImages = ApplySort(ApplyFolderBucketFilter(source)).ToList();
        _list.VirtualListSize = _visibleImages.Count;
        _list.Invalidate();
        if (_visibleImages.Count > 0 && _list.SelectedIndices.Count == 0)
        {
            _list.SelectedIndices.Add(0);
        }

        UpdateSelectionActions();
        SetStatus($"Showing {_visibleImages.Count:n0} / {_allImages.Count:n0} images.");
    }

    private static ListViewItem CreateListItem(NativeImageRecord image)
    {
        var favorite = image.FavoriteLevel > 0 ? image.FavoriteLevel.ToString() : "";
        var item = new ListViewItem(favorite.Length > 0 ? $"★{favorite} {image.Filename}" : image.Filename)
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

    private async Task LoadSelectedPreviewAsync()
    {
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

            _previewLabel.Text = $"{image.Filename}  {FormatBytes(image.SizeBytes)}  {dimensionHint}  fav {image.FavoriteLevel}";
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

    private void OpenSelectedFile()
    {
        var image = GetSelectedImage();
        if (image is null)
        {
            return;
        }

        OpenExternalPath(image.AbsolutePath);
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
            OpenExternalPath,
            SetFavoriteLevelForPath);
    }

    private DetailSmokeReport RunDetailModalSmoke()
    {
        var index = GetSelectedIndex();
        if (index < 0)
        {
            return new DetailSmokeReport(false, false, false, false, false, false, false, false);
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
        var image = GetSelectedImage();
        if (image is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{image.AbsolutePath}\"",
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
        var index = GetSelectedIndex();
        if (index < 0)
        {
            return;
        }

        var current = _visibleImages[index];
        SetFavoriteLevelForPath(current.AbsolutePath, level);
    }

    private void SetFavoriteLevelForPath(string absolutePath, int level)
    {
        var current = _visibleImages.FirstOrDefault(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase))
            ?? _allImages.FirstOrDefault(item => string.Equals(item.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            return;
        }

        var clamped = Math.Clamp(level, 0, 5);
        _store.SetFavoriteLevel(current.AbsolutePath, clamped);
        _favorites = _store.LoadFavorites();
        var updated = current with { FavoriteLevel = clamped };
        ReplaceImage(current.AbsolutePath, updated);
        ApplyFilter();
        SelectImage(updated.AbsolutePath);
        SetStatus($"Favorite level {clamped}: {updated.Filename}");
    }

    private void ClearPreview(string message)
    {
        var previous = _preview.Image;
        _preview.Image = null;
        previous?.Dispose();
        _previewLabel.Text = message;
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
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
        _previousButton.Enabled = index > 0;
        _nextButton.Enabled = index >= 0 && index < _visibleImages.Count - 1;
        _detailButton.Enabled = selected is not null;
        _openFileButton.Enabled = selected is not null;
        _openFolderButton.Enabled = selected is not null;
        _deleteButton.Enabled = selected is not null;
        _favoriteLevel.Enabled = selected is not null;
        _selectionLabel.Text = index >= 0
            ? $"Selected 1 / {_visibleImages.Count:n0}"
            : $"Selected 0 / {_visibleImages.Count:n0}";
        _updatingFavoriteControl = true;
        _favoriteLevel.Value = selected is null ? 0 : Math.Clamp(selected.FavoriteLevel, 0, 5);
        _updatingFavoriteControl = false;
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

    private void ApplyViewMode(string mode)
    {
        var grid = string.Equals(mode, "grid", StringComparison.OrdinalIgnoreCase);
        _list.View = grid ? View.LargeIcon : View.Details;
        _list.FullRowSelect = !grid;
        if (_viewMode.Items.Count > 0)
        {
            _viewMode.SelectedItem = grid ? "Grid" : "List";
        }

        _store.SaveSetting("view_mode", grid ? "grid" : "details");
    }

    private void SaveViewState()
    {
        _store.SaveViewState(_list.View == View.LargeIcon ? "grid" : "details", _searchText.Text.Trim(), _favoritesOnly.Checked);
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
        var hidden = new HashSet<string>(
            _store.GetSetting("hidden_folder_buckets", "")
                .Split(['\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
        var buckets = _allImages
            .GroupBy(static item => item.Folder, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FolderBucket(group.Key, FormatFolderBucketLabel(group.Key), group.Count()))
            .OrderBy(static item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _updatingFolderBuckets = true;
        try
        {
            _folderBuckets.Items.Clear();
            foreach (var bucket in buckets)
            {
                _folderBuckets.Items.Add(bucket, !hidden.Contains(bucket.Folder));
            }
        }
        finally
        {
            _updatingFolderBuckets = false;
        }

        _folderBucketLabel.Text = $"Folders ({buckets.Count:n0})";
        UpdateFolderBucketButtons();
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
        _showAllFoldersButton.Enabled = hasBuckets;
        _hideAllFoldersButton.Enabled = hasBuckets;
        _invertFoldersButton.Enabled = hasBuckets;
    }

    private string FormatFolderBucketLabel(string folder)
    {
        if (!string.IsNullOrWhiteSpace(_currentFolder))
        {
            try
            {
                var relative = Path.GetRelativePath(_currentFolder, folder);
                if (relative == ".")
                {
                    return ".";
                }

                if (!relative.StartsWith("..", StringComparison.Ordinal))
                {
                    return relative;
                }
            }
            catch
            {
                // Fall back to the full folder path when relative formatting fails.
            }
        }

        return folder;
    }

    private void ApplyThumbnailSize(int size)
    {
        var clamped = Math.Clamp(size, 64, 192);
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

        if (_gridImages.ImageSize.Width == clamped && _gridImages.Images.Count > 0)
        {
            return;
        }

        _gridImages.Images.Clear();
        _gridImages.ImageSize = new Size(clamped, clamped);
        _gridImages.Images.Add(CreateGridPlaceholder(clamped));
        _list.Invalidate();
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
        dialog.ShowDialog(this);
    }

    private NativeSettingsSnapshot BuildNativeSettingsSnapshot()
    {
        return new NativeSettingsSnapshot(
            DatabasePath: _store.DatabasePath,
            BrowserSettingsImported: _store.GetSetting("browser_settings_found", "0") == "1",
            KeyBindingsJson: _store.GetSetting("keybindings_json", "{}"),
            KeyBindingMode: "read-only in M9; editable keybinding recorder deferred");
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

    private static Bitmap CreateGridPlaceholder(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(38, 38, 38));
        using var border = new Pen(Color.FromArgb(80, 80, 80));
        var inset = Math.Max(8, size / 12);
        graphics.DrawRectangle(border, inset, inset, size - (inset * 2), size - (inset * 2));
        return bitmap;
    }

    private static Image LoadImageCopy(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        using var source = Image.FromStream(memory);
        return new Bitmap(source);
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
        private readonly Action<string> _openExternal;
        private readonly Action<string, int> _favoriteChanged;
        private readonly FlowLayoutPanel _toolbar = new();
        private readonly Panel _imageHost = new();
        private readonly PictureBox _imageBox = new();
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
            Action<string> openExternal,
            Action<string, int> favoriteChanged)
        {
            if (images.Count == 0)
            {
                throw new ArgumentException("Detail modal requires at least one image.", nameof(images));
            }

            _images = images.ToList();
            _index = Math.Clamp(startIndex, 0, _images.Count - 1);
            _openExternal = openExternal;
            _favoriteChanged = favoriteChanged;

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
            return new DetailSmokeReport(modalOpened, navigation, zoom, reset, pan, flip, favorite, openExternal);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
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
                RowCount = 3,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

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

            _metaLabel.Dock = DockStyle.Fill;
            _metaLabel.Padding = new Padding(8, 4, 8, 4);
            _metaLabel.AutoEllipsis = true;
            _metaLabel.TextAlign = ContentAlignment.MiddleLeft;

            root.Controls.Add(_toolbar, 0, 0);
            root.Controls.Add(_imageHost, 0, 1);
            root.Controls.Add(_metaLabel, 0, 2);
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

        private void UpdateMeta()
        {
            var image = CurrentImage;
            var dimensions = FormatDimensions(image);
            var zoomText = $"{_zoom * 100f:0}%";
            var flipText = _flipped ? "flipped" : "normal";
            _metaLabel.Text = $"{_index + 1:n0}/{_images.Count:n0}  {image.Filename}  {FormatBytes(image.SizeBytes)}  {dimensions}  fav {image.FavoriteLevel}  zoom {zoomText}  {flipText}";
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
        public NativeSettingsDialog(NativeSettingsSnapshot snapshot)
        {
            Text = "Native Settings";
            Width = 760;
            Height = 420;
            MinimumSize = new Size(620, 320);
            StartPosition = FormStartPosition.CenterParent;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10),
            };
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
                    $"Keybinding mode: {snapshot.KeyBindingMode}",
                    "",
                    "Key bindings:",
                    snapshot.KeyBindingsJson,
                }),
            };

            var ok = new Button
            {
                Text = "OK",
                Width = 92,
                Height = 28,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                DialogResult = DialogResult.OK,
            };

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
            };
            actions.Controls.Add(ok);

            root.Controls.Add(text, 0, 0);
            root.Controls.Add(actions, 0, 1);
            Controls.Add(root);
            AcceptButton = ok;
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
        bool OpenExternal);

    private sealed record NativeSettingsSnapshot(
        string DatabasePath,
        bool BrowserSettingsImported,
        string KeyBindingsJson,
        string KeyBindingMode);

    private sealed record FolderBucket(string Folder, string Label, int Count)
    {
        public override string ToString()
        {
            return $"{Label} ({Count:n0})";
        }
    }

    private sealed record NativeUiSmokeReport(
        string Folder,
        int ScannedImages,
        int InitialVisible,
        bool PreviewLoaded,
        bool NavigationButtons,
        bool KeyboardNavigation,
        bool KeyboardFavorite,
        bool GridToggle,
        int FolderBuckets,
        bool FolderHideAll,
        bool SortName,
        bool RandomReshuffle,
        bool ThumbnailSize,
        bool PreviewToggle,
        bool DetailsToggle,
        bool PreviewSplitter,
        bool SelectedCount,
        bool DetailModal,
        bool DetailNavigation,
        bool DetailZoom,
        bool DetailReset,
        bool DetailPan,
        bool DetailFlip,
        bool DetailFavorite,
        bool DetailOpenExternal,
        bool SettingsReadOnly,
        int SearchMatches,
        int FavoriteMatches,
        bool NoResultsState,
        bool FolderErrorState,
        int Albums,
        int AlbumImages,
        int BrowserStateKeys,
        bool SettingsImported,
        bool EnhancementStateUnchanged);
}
