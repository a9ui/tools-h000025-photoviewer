using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    private const int MaxThumbnailDecodeWorkers = 4;
    private readonly ObservableCollection<Tile> _tiles = new();
    private readonly List<Tile> _allTiles = new();
    private Rect _restoreBounds;
    private bool _fakeMaximized;
    private bool _initializing = true;
    private bool _suppressStateSave;
    private bool _syncingSelection;
    private string? _currentFolder;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _modalCts;
    private int _previewUpdateCount;
    private long _previewMs;
    public LoadMetrics? LastLoadMetrics { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        RestoreState();
        BuildSampleTiles();
        _allTiles.AddRange(_tiles);
        _tiles.Clear();

        CardsList.ItemsSource = BuildGroupedView();
        RowsList.ItemsSource = BuildGroupedView();
        ApplyFilters(selectFirst: false);

        Loaded += (_, _) =>
        {
            if (CardsList.Items.Count > 0)
                CardsList.SelectedIndex = 0;
        };
        CardsList.MouseDoubleClick += (_, _) => OpenModal();
        RowsList.MouseDoubleClick += (_, _) => OpenModal();
        _initializing = false;
    }

    private System.ComponentModel.ICollectionView BuildGroupedView()
    {
        var cvs = new CollectionViewSource { Source = _tiles };
        cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Tile.Group)));
        return cvs.View;
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e) => await ChooseAndLoadFolderAsync();

    private async Task ChooseAndLoadFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select an image folder",
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) == true)
            await LoadFolderAsync(dialog.FolderName);
    }

    public async Task LoadFolderAsync(string folder)
    {
        var totalWatch = Stopwatch.StartNew();
        LastLoadMetrics = null;
        _previewUpdateCount = 0;
        _previewMs = 0;
        string resolvedFolder;
        try
        {
            resolvedFolder = Path.GetFullPath(folder);
        }
        catch
        {
            return;
        }

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        Landing.Visibility = Visibility.Visible;
        LandingPanel.IsEnabled = false;
        ScanPanel.Visibility = Visibility.Visible;
        ScanBar.Value = 0;
        ScanPercent.Text = "0%";
        ScanLabel.Text = "Scanning...";
        ScanMessage.Text = resolvedFolder;

        IReadOnlyList<FileInfo> files;
        var scanWatch = Stopwatch.StartNew();
        try
        {
            files = await Task.Run(
                () => EnumerateImageFiles(resolvedFolder)
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

        var materializeWatch = Stopwatch.StartNew();
        _currentFolder = resolvedFolder;
        _allTiles.Clear();
        _tiles.Clear();
        double width = SizeSlider?.Value ?? 190;
        foreach (var file in files)
            _allTiles.Add(MakeFileTile(file, width));

        FolderPathText.Text = resolvedFolder;
        ApplyFilters(selectFirst: false);
        UpdateFolderStats();
        SaveState();
        materializeWatch.Stop();

        if (files.Count == 0)
        {
            totalWatch.Stop();
            LastLoadMetrics = LoadMetrics.Create(
                resolvedFolder,
                files.Count,
                scanWatch.ElapsedMilliseconds,
                materializeWatch.ElapsedMilliseconds,
                thumbnailMs: 0,
                thumbnailWorkers: 0,
                thumbnailsCompleted: 0,
                previewMs: _previewMs,
                previewUpdates: _previewUpdateCount,
                totalWatch.ElapsedMilliseconds);
            LandingPanel.IsEnabled = true;
            ScanBar.Value = 0;
            ScanPercent.Text = "0%";
            ScanLabel.Text = "No images found";
            ScanMessage.Text = "Choose another folder.";
            return;
        }

        SetPhase(landing: false);
        SelectFirstAvailable();

        var thumbnails = await LoadThumbnailsAsync(cts.Token);
        totalWatch.Stop();
        LastLoadMetrics = LoadMetrics.Create(
            resolvedFolder,
            files.Count,
            scanWatch.ElapsedMilliseconds,
            materializeWatch.ElapsedMilliseconds,
            thumbnails.ElapsedMs,
            thumbnails.Workers,
            thumbnails.Completed,
            _previewMs,
            _previewUpdateCount,
            totalWatch.ElapsedMilliseconds);
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

        int workers = Math.Min(MaxThumbnailDecodeWorkers, Math.Max(1, total));
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

                    await Dispatcher.InvokeAsync(
                        () =>
                        {
                            tile.Thumbnail = thumbnail;
                            int completed = Interlocked.Increment(ref done);
                            UpdateThumbnailProgress(completed, total);
                        },
                        DispatcherPriority.Background,
                        itemToken);
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

        return new ThumbnailLoadMetrics(total, workers, done, watch.ElapsedMilliseconds);
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

    private static Tile MakeFileTile(FileInfo file, double width)
    {
        var modified = file.LastWriteTime;
        int paletteIndex = file.FullName.GetHashCode(StringComparison.OrdinalIgnoreCase) & int.MaxValue;
        return new Tile
        {
            ArtBase = MakeBaseBrush(paletteIndex),
            ArtGlow = MakeGlowBrush(paletteIndex),
            FileName = file.Name,
            Fav = 0,
            Unseen = false,
            Group = FormatGroup(modified),
            CardWidth = width,
            Prompt = file.FullName,
            Path = file.FullName,
            IsRealFile = true,
            SizeText = FormatBytes(file.Length),
            ModifiedText = modified.ToString("yyyy-MM-dd HH:mm"),
        };
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

    private static Tile MakeTile(int i, string group, int fav, bool unseen, int baseNum, double width, int offset = 0)
    {
        int idx = i + offset;
        string name = $"{(baseNum - i):00000}-{Names[idx % Names.Length]}.png";
        return new Tile
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
            SizeText = "832 x 1216",
            ModifiedText = "2026-07-08 14:22",
        };
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
    }

    private void UpdatePreview(Tile t)
    {
        var watch = Stopwatch.StartNew();
        var preview = t.IsRealFile ? LoadBitmap(t.Path, 900) : null;
        PreviewBitmap.Source = preview;
        PreviewBitmap.Visibility = preview is null ? Visibility.Collapsed : Visibility.Visible;
        PreviewArtBase.Visibility = preview is null ? Visibility.Visible : Visibility.Collapsed;
        PreviewArtGlow.Visibility = preview is null ? Visibility.Visible : Visibility.Collapsed;
        PreviewArtBase.Fill = t.ArtBase;
        PreviewArtGlow.Fill = t.ArtGlow;
        PreviewFileName.Text = t.FileName;
        PreviewTabName.Text = t.FileName;
        BottomSelectedTabName.Text = t.FileName;
        PreviewSizeText.Text = t.IsRealFile && TryReadBitmapSize(t.Path, out var width, out var height)
            ? $"{width} x {height}"
            : t.SizeText;
        PreviewModelText.Text = t.IsRealFile
            ? Path.GetExtension(t.Path).TrimStart('.').ToUpperInvariant()
            : "animagineXL_v31";
        PreviewDateText.Text = t.ModifiedText;
        PreviewPromptText.Text = string.IsNullOrWhiteSpace(t.Prompt) ? t.Path : t.Prompt;
        UpdateHeaderStats();
        ModalTitle.Text = $"{t.FileName} - {PreviewSizeText.Text}";
        watch.Stop();
        _previewUpdateCount++;
        _previewMs += watch.ElapsedMilliseconds;
    }

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing) return;
        ApplyFilters();
        SaveState();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        ApplyFilters();
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
            SearchInput.Text = query;
            ApplyFilters();
        }
        finally
        {
            _suppressStateSave = previous;
        }

        if (persist)
            SaveState();
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
        bool unseenOnly = UnseenOnlyFilter?.IsChecked == true;

        var filtered = _allTiles
            .Where(tile => MatchesSearch(tile, query))
            .Where(tile => !favoritesOnly || tile.Fav > 0)
            .Where(tile => !unseenOnly || tile.Unseen)
            .ToList();

        _tiles.Clear();
        foreach (var tile in filtered)
            _tiles.Add(tile);

        UpdateFolderStats();

        if (previous is not null && filtered.Contains(previous))
            SelectTile(previous);
        else if (selectFirst)
            SelectFirstAvailable();
        else if (filtered.Count == 0)
            SelectTile(null);
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

    private void SelectFirstAvailable()
    {
        SelectTile(_tiles.Count > 0 ? _tiles[0] : null);
    }

    private void SelectTile(Tile? tile)
    {
        CardsList.SelectedItem = tile;
        RowsList.SelectedItem = tile;
        if (tile is null)
            ClearPreview();
    }

    private void ClearPreview()
    {
        PreviewBitmap.Source = null;
        PreviewBitmap.Visibility = Visibility.Collapsed;
        PreviewArtBase.Visibility = Visibility.Visible;
        PreviewArtGlow.Visibility = Visibility.Visible;
        PreviewFileName.Text = "No matching image";
        PreviewTabName.Text = "No selection";
        BottomSelectedTabName.Text = "No selection";
        PreviewSizeText.Text = "-";
        PreviewModelText.Text = "-";
        PreviewDateText.Text = "-";
        PreviewPromptText.Text = "";
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
        string folderText = string.IsNullOrWhiteSpace(_currentFolder) ? "sample" : "1 folder";
        HeaderStats.Text = $"{selected:N0} selected - {imageText} - {folderText}";
    }

    // ─────────── Size slider ───────────
    private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        foreach (var t in _allTiles)
            t.CardWidth = e.NewValue;
        if (!_initializing)
            SaveState();
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
    private void Logo_Click(object sender, MouseButtonEventArgs e) => SetPhase(landing: true);

    private void SetPhase(bool landing)
    {
        ScanPanel.Visibility = Visibility.Collapsed;
        LandingPanel.IsEnabled = true;
        ScanBar.Value = 0;
        Landing.Visibility = landing ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void StartScan_Click(object sender, RoutedEventArgs e) => await ChooseAndLoadFolderAsync();

    private async void RefreshActiveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentFolder) && Directory.Exists(_currentFolder))
            await LoadFolderAsync(_currentFolder);
        else
            await ChooseAndLoadFolderAsync();
    }

    private async void OpenLastFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentFolder) && Directory.Exists(_currentFolder))
            await LoadFolderAsync(_currentFolder);
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

    private void RestoreState()
    {
        var state = ReadState();
        if (state is null) return;

        if (!string.IsNullOrWhiteSpace(state.LastFolder))
        {
            _currentFolder = state.LastFolder;
            FolderPathText.Text = state.LastFolder;
            FolderCountText.Text = Directory.Exists(state.LastFolder) ? "Last folder saved" : "Last folder unavailable";
        }

        if (state.CardWidth >= SizeSlider.Minimum && state.CardWidth <= SizeSlider.Maximum)
            SizeSlider.Value = state.CardWidth;

        if (!string.IsNullOrWhiteSpace(state.SearchQuery))
            SearchInput.Text = state.SearchQuery;
    }

    private static ViewerState? ReadState()
    {
        try
        {
            if (!File.Exists(StatePath)) return null;
            return JsonSerializer.Deserialize<ViewerState>(File.ReadAllText(StatePath));
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
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            var state = new ViewerState
            {
                LastFolder = _currentFolder,
                SearchQuery = SearchInput.Text,
                CardWidth = SizeSlider.Value,
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StatePath, json);
        }
        catch
        {
            // State persistence should never block passive browsing.
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
        if (e.Key == Key.Escape && CloseTopmostOverlay())
            e.Handled = true;
        base.OnPreviewKeyDown(e);
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
}

// Lightweight persisted shell state.
public sealed class ViewerState
{
    public string? LastFolder { get; set; }
    public string? SearchQuery { get; set; }
    public double CardWidth { get; set; } = 190;
}

public sealed class LoadMetrics
{
    public string Folder { get; set; } = "";
    public int FileCount { get; set; }
    public long ScanMs { get; set; }
    public long MaterializeMs { get; set; }
    public long ThumbnailMs { get; set; }
    public int ThumbnailWorkers { get; set; }
    public int ThumbnailsCompleted { get; set; }
    public long PreviewMs { get; set; }
    public int PreviewUpdates { get; set; }
    public long ModalOpenMs { get; set; }
    public bool ModalImmediateSource { get; set; }
    public bool ModalDeferredDecode { get; set; }
    public long TotalMs { get; set; }
    public string CompletedAtUtc { get; set; } = "";

    public static LoadMetrics Create(string folder, int fileCount, long scanMs, long materializeMs, long thumbnailMs, int thumbnailWorkers, int thumbnailsCompleted, long previewMs, int previewUpdates, long totalMs)
        => new()
        {
            Folder = folder,
            FileCount = fileCount,
            ScanMs = scanMs,
            MaterializeMs = materializeMs,
            ThumbnailMs = thumbnailMs,
            ThumbnailWorkers = thumbnailWorkers,
            ThumbnailsCompleted = thumbnailsCompleted,
            PreviewMs = previewMs,
            PreviewUpdates = previewUpdates,
            TotalMs = totalMs,
            CompletedAtUtc = DateTime.UtcNow.ToString("O"),
        };
}

public readonly record struct ThumbnailLoadMetrics(int Total, int Workers, int Completed, long ElapsedMs);

// ─────────── Tile view model ───────────
public sealed class Tile : INotifyPropertyChanged
{
    public Brush? ArtBase { get; set; }
    public Brush? ArtGlow { get; set; }
    public string FileName { get; set; } = "";
    public int Fav { get; set; }
    public bool Unseen { get; set; }
    public string Group { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsRealFile { get; set; }
    public string SizeText { get; set; } = "";
    public string ModifiedText { get; set; } = "";

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

    public event PropertyChangedEventHandler? PropertyChanged;
}
