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
    private readonly Button _previousButton = new();
    private readonly Button _nextButton = new();
    private readonly NumericUpDown _favoriteLevel = new();
    private readonly Button _openFileButton = new();
    private readonly Button _openFolderButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _settingsButton = new();
    private readonly Label _stateLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ListView _list = new();
    private readonly PictureBox _preview = new();
    private readonly Label _previewLabel = new();
    private readonly ImageList _gridImages = new();

    private readonly string _projectRoot;
    private readonly NativeImageStore _store;
    private Dictionary<string, int> _favorites;
    private List<NativeImageRecord> _allImages = [];
    private List<NativeImageRecord> _visibleImages = [];
    private CancellationTokenSource? _scanCancellation;
    private long _previewVersion;
    private bool _updatingFavoriteControl;

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
        ApplyStateSummary(report);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scanCancellation?.Cancel();
            _scanCancellation?.Dispose();
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
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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
        actions.Controls.Add(favoriteLabel);
        actions.Controls.Add(_favoriteLevel);
        actions.Controls.Add(_openFileButton);
        actions.Controls.Add(_openFolderButton);
        actions.Controls.Add(_deleteButton);
        actions.Controls.Add(_settingsButton);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 760,
        };

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.VirtualMode = true;
        _list.MultiSelect = false;
        _gridImages.ImageSize = new Size(96, 96);
        _gridImages.ColorDepth = ColorDepth.Depth32Bit;
        _gridImages.Images.Add(CreateGridPlaceholder());
        _list.SmallImageList = _gridImages;
        _list.LargeImageList = _gridImages;
        _list.Columns.Add("Name", 300);
        _list.Columns.Add("Fav", 48);
        _list.Columns.Add("Folder", 300);
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
        split.Panel1.Controls.Add(_list);

        var previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.FromArgb(20, 20, 20);
        _preview.SizeMode = PictureBoxSizeMode.Zoom;

        _previewLabel.Dock = DockStyle.Fill;
        _previewLabel.Padding = new Padding(8, 4, 8, 4);
        _previewLabel.AutoEllipsis = true;
        _previewLabel.Text = "Select an image.";

        previewPanel.Controls.Add(_preview, 0, 0);
        previewPanel.Controls.Add(_previewLabel, 0, 1);
        split.Panel2.Controls.Add(previewPanel);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Padding = new Padding(8, 0, 8, 0);
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.AutoEllipsis = true;
        _statusLabel.Text = "Ready.";

        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(actions, 0, 1);
        root.Controls.Add(split, 0, 2);
        root.Controls.Add(_statusLabel, 0, 3);
        Controls.Add(root);
        UpdateSelectionActions();
    }

    private void ApplyStateSummary(NativeImportReport? report = null)
    {
        report ??= _store.ImportProjectState();
        _stateLabel.Text = $"db {report.ImageCount:n0} / fav {report.FavoriteCount:n0} / albums {report.AlbumCount:n0} / settings {(report.SettingsFound ? "yes" : "no")}";
    }

    private void ImportState()
    {
        var report = _store.ImportProjectState();
        _favorites = _store.LoadFavorites();
        _allImages = ReapplyFavorites(_allImages);
        ApplyFilter();
        ApplyStateSummary(report);
        SetStatus($"Imported state: {report.FavoriteCount:n0} favorites, {report.AlbumCount:n0} albums, db {report.ImageCount:n0} images.");
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

        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        _scanButton.Enabled = false;
        _cancelButton.Enabled = true;
        _importButton.Enabled = false;
        _allImages = [];
        _visibleImages = [];
        _list.VirtualListSize = 0;
        ClearPreview("Scanning...");

        var stopwatch = Stopwatch.StartNew();
        var progress = new Progress<NativeScanProgress>(item =>
        {
            SetStatus($"Scanning {item.Count:n0} images - {item.CurrentFolder}");
        });

        try
        {
            _favorites = _store.LoadFavorites();
            var scanned = await NativeImageScanner.ScanAsync(folder, _favorites, progress, _scanCancellation.Token);
            stopwatch.Stop();
            _store.SaveScanResult(folder, scanned, stopwatch.Elapsed);
            _allImages = _store.LoadImagesForRoot(folder);
            ApplyFilter();
            ApplyStateSummary();
            SetStatus($"Scan complete: {_allImages.Count:n0} images in {stopwatch.Elapsed.TotalSeconds:n1}s. Saved to {_store.DatabasePath}");
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

    private void ApplyFilter()
    {
        var query = _searchText.Text.Trim();
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

        _visibleImages = source.ToList();
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
        _previewLabel.Text = $"Loading {image.Filename}";

        try
        {
            var loaded = await Task.Run(() => LoadImageCopy(image.AbsolutePath));
            if (version != Interlocked.Read(ref _previewVersion))
            {
                loaded.Dispose();
                return;
            }

            var previous = _preview.Image;
            _preview.Image = loaded;
            previous?.Dispose();
            _previewLabel.Text = $"{image.Filename}  {FormatBytes(image.SizeBytes)}  fav {image.FavoriteLevel}";
            UpdateSelectionActions();
            _ = Task.Run(() => WarmNeighborMetadata(index));
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

        Process.Start(new ProcessStartInfo
        {
            FileName = image.AbsolutePath,
            UseShellExecute = true,
        });
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
        _store.SetFavoriteLevel(current.AbsolutePath, level);
        _favorites = _store.LoadFavorites();
        var updated = current with { FavoriteLevel = level };
        ReplaceImage(current.AbsolutePath, updated);
        ApplyFilter();
        SelectImage(updated.AbsolutePath);
        SetStatus($"Favorite level {level}: {updated.Filename}");
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
        _openFileButton.Enabled = selected is not null;
        _openFolderButton.Enabled = selected is not null;
        _deleteButton.Enabled = selected is not null;
        _favoriteLevel.Enabled = selected is not null;
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

    private void ShowNativeSettings()
    {
        var bindings = _store.GetSetting("keybindings_json", "{}");
        var browserSettingsFound = _store.GetSetting("browser_settings_found", "0") == "1" ? "yes" : "no";
        MessageBox.Show(
            this,
            $"Native SQLite: {_store.DatabasePath}{Environment.NewLine}Browser settings imported: {browserSettingsFound}{Environment.NewLine}Key bindings: {bindings}",
            "Native Settings",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void WarmNeighborMetadata(int index)
    {
        foreach (var neighbor in new[] { index - 1, index + 1 })
        {
            if (neighbor < 0 || neighbor >= _visibleImages.Count)
            {
                continue;
            }

            try
            {
                _ = new FileInfo(_visibleImages[neighbor].AbsolutePath).Length;
            }
            catch
            {
                // Metadata warmup is best-effort only.
            }
        }
    }

    private static Bitmap CreateGridPlaceholder()
    {
        var bitmap = new Bitmap(96, 96);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(38, 38, 38));
        using var border = new Pen(Color.FromArgb(80, 80, 80));
        graphics.DrawRectangle(border, 8, 8, 80, 80);
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
}
