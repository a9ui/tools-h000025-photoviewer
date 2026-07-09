using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
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
    private readonly ObservableCollection<Tile> _tiles = new();
    private Rect _restoreBounds;
    private bool _fakeMaximized;
    private CancellationTokenSource? _loadCts;

    public MainWindow()
    {
        InitializeComponent();
        BuildSampleTiles();

        CardsList.ItemsSource = BuildGroupedView();
        RowsList.ItemsSource = BuildGroupedView();

        Loaded += (_, _) =>
        {
            if (CardsList.Items.Count > 0)
                CardsList.SelectedIndex = 0;
        };
        CardsList.MouseDoubleClick += (_, _) => OpenModal();
        RowsList.MouseDoubleClick += (_, _) => OpenModal();
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

        if (cts.IsCancellationRequested)
            return;

        _tiles.Clear();
        double width = SizeSlider?.Value ?? 190;
        foreach (var file in files)
            _tiles.Add(MakeFileTile(file, width));

        FolderPathText.Text = resolvedFolder;
        FolderCountText.Text = files.Count == MaxLoadedImages
            ? $"{files.Count:N0}+ images loaded"
            : $"{files.Count:N0} images loaded";
        HeaderStats.Text = $"0 selected - {files.Count:N0} images - 1 folder";

        if (files.Count == 0)
        {
            LandingPanel.IsEnabled = true;
            ScanBar.Value = 0;
            ScanPercent.Text = "0%";
            ScanLabel.Text = "No images found";
            ScanMessage.Text = "Choose another folder.";
            return;
        }

        SetPhase(landing: false);
        CardsList.SelectedIndex = 0;
        RowsList.SelectedIndex = 0;

        await LoadThumbnailsAsync(cts.Token);
    }

    private async Task LoadThumbnailsAsync(CancellationToken token)
    {
        var snapshot = _tiles.Where(static tile => tile.IsRealFile).ToList();
        int total = Math.Max(1, snapshot.Count);
        int done = 0;

        foreach (var tile in snapshot)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                int decodeWidth = (int)Math.Clamp(tile.CardWidth * 1.4, 180, 520);
                tile.Thumbnail = await Task.Run(() => LoadBitmap(tile.Path, decodeWidth), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                tile.Thumbnail = null;
            }

            done++;
            if (done % 8 == 0 || done == total)
            {
                double progress = done * 100.0 / total;
                ScanBar.Value = progress;
                ScanPercent.Text = $"{(int)progress}%";
                ScanLabel.Text = $"{done:N0} / {total:N0} thumbnails";
            }
        }
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
        if (sender is not ListBox lb || lb.SelectedItem is not Tile t) return;
        if (lb == CardsList && RowsList.SelectedItem != t)
            RowsList.SelectedItem = t;
        if (lb == RowsList && CardsList.SelectedItem != t)
            CardsList.SelectedItem = t;

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
        HeaderStats.Text = $"{(CardsList.SelectedItems?.Count ?? 0):N0} selected - {_tiles.Count:N0} images - 1 folder";
        ModalTitle.Text = $"{t.FileName} - {PreviewSizeText.Text}";
    }

    // ─────────── Size slider ───────────
    private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        foreach (var t in _tiles)
            t.CardWidth = e.NewValue;
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
        var bitmap = t.IsRealFile ? LoadBitmap(t.Path, 1400) : null;
        ModalBitmap.Source = bitmap;
        ModalBitmap.Visibility = bitmap is null ? Visibility.Collapsed : Visibility.Visible;
        ModalArtBase.Visibility = bitmap is null ? Visibility.Visible : Visibility.Collapsed;
        ModalArtGlow.Visibility = bitmap is null ? Visibility.Visible : Visibility.Collapsed;
        ModalArtBase.Fill = t.ArtBase;
        ModalArtGlow.Fill = t.ArtGlow;
        Modal.Visibility = Visibility.Visible;
    }

    private void CloseModal_Click(object sender, RoutedEventArgs e) => Modal.Visibility = Visibility.Collapsed;

    /// <summary>Used only by the --shot --modal smoke path to capture the modal state.</summary>
    public void ShowModalForShot()
    {
        if (CardsList.SelectedItem is null && CardsList.Items.Count > 0)
            CardsList.SelectedIndex = 0;
        OpenModal();
    }

    private Tile? SelectedTile() => CardsList.SelectedItem as Tile ?? RowsList.SelectedItem as Tile;

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
