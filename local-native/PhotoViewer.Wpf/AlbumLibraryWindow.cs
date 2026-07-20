using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PhotoViewer.Wpf;

internal sealed class AlbumLibraryWindow : Window
{
    private readonly string _storePath = AlbumStore.ResolvePath();
    private readonly IReadOnlyList<string> _selectedPaths;
    private readonly HashSet<string> _catalogPaths;
    private readonly Action<AlbumEntry?> _activateAlbum;
    private readonly Action _libraryChanged;
    private readonly ListBox _albumList = new() { MinHeight = 250 };
    private readonly ListBox _memberList = new() { MinHeight = 250, SelectionMode = SelectionMode.Extended };
    private readonly TextBox _name = new() { MinWidth = 220, MaxLength = AlbumStore.MaxNameLength };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private AlbumDocumentSnapshot? _document;

    internal AlbumLibraryWindow(
        Window owner,
        IReadOnlyList<string> selectedPaths,
        IEnumerable<string> catalogPaths,
        Action<AlbumEntry?> activateAlbum,
        Action libraryChanged)
    {
        Owner = owner;
        _selectedPaths = selectedPaths;
        _catalogPaths = catalogPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _activateAlbum = activateAlbum;
        _libraryChanged = libraryChanged;
        Title = "Albums - PhotoViewer";
        Width = 860;
        Height = 620;
        MinWidth = 720;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(23, 26, 34));
        Foreground = Brushes.WhiteSmoke;
        Content = BuildContent();
        _albumList.SelectionChanged += (_, _) => RefreshMembers();
        Loaded += (_, _) => Reload();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var createRow = new StackPanel { Orientation = Orientation.Horizontal };
        createRow.Children.Add(_name);
        createRow.Children.Add(ActionButton("Create", (_, _) => CreateAlbum()));
        createRow.Children.Add(ActionButton("Rename", (_, _) => RenameAlbum()));
        createRow.Children.Add(ActionButton("Refresh", (_, _) => Reload()));
        Grid.SetRow(createRow, 0);
        root.Children.Add(createRow);

        var columns = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.43, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.57, GridUnitType.Star) });

        var albumPanel = new DockPanel();
        var albumActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        albumActions.Children.Add(ActionButton("Open", (_, _) => OpenAlbum()));
        albumActions.Children.Add(ActionButton("Add selection", (_, _) => AddSelection()));
        albumActions.Children.Add(ActionButton("Pin / unpin", (_, _) => TogglePin()));
        albumActions.Children.Add(ActionButton("Delete Album", (_, _) => DeleteAlbum()));
        albumActions.Children.Add(ActionButton("Return to catalog", (_, _) => { _activateAlbum(null); _status.Text = "Catalog source restored."; }));
        DockPanel.SetDock(albumActions, Dock.Bottom);
        albumPanel.Children.Add(albumActions);
        albumPanel.Children.Add(_albumList);
        Grid.SetColumn(albumPanel, 0);
        columns.Children.Add(albumPanel);

        var memberPanel = new DockPanel();
        var memberActions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        memberActions.Children.Add(ActionButton("Remove from Album", (_, _) => RemoveMembers()));
        memberActions.Children.Add(ActionButton("Use as cover", (_, _) => SetCover()));
        DockPanel.SetDock(memberActions, Dock.Bottom);
        memberPanel.Children.Add(memberActions);
        memberPanel.Children.Add(_memberList);
        Grid.SetColumn(memberPanel, 2);
        columns.Children.Add(memberPanel);
        Grid.SetRow(columns, 1);
        root.Children.Add(columns);

        Grid.SetRow(_status, 2);
        root.Children.Add(_status);
        return root;
    }

    private static Button ActionButton(string text, RoutedEventHandler click)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 7, 0), Padding = new Thickness(10, 5, 10, 5) };
        button.Click += click;
        return button;
    }

    private AlbumEntry? SelectedAlbum => (_albumList.SelectedItem as AlbumListItem)?.Album;

    private void Reload(string? selectAlbumId = null, string? message = null)
    {
        AlbumReadResult read = AlbumStore.Read(_storePath);
        if (!read.Supported || read.Document is null)
        {
            _document = null;
            _albumList.ItemsSource = null;
            _memberList.ItemsSource = null;
            _status.Text = $"Shared Album state is protected and was not changed. {read.Error}";
            return;
        }

        string? selectedId = selectAlbumId ?? SelectedAlbum?.Id;
        _document = read.Document;
        var recentOrder = read.Document.RecentAlbumIds
            .Select((id, index) => (id, index))
            .ToDictionary(static item => item.id, static item => item.index, StringComparer.Ordinal);
        var items = read.Document.Albums
            .OrderByDescending(static album => album.Pinned)
            .ThenBy(album => recentOrder.TryGetValue(album.Id, out int index) ? index : int.MaxValue)
            .ThenBy(static album => album.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static album => new AlbumListItem(album))
            .ToList();
        _albumList.ItemsSource = items;
        _albumList.SelectedItem = items.FirstOrDefault(item => item.Album.Id == selectedId) ?? items.FirstOrDefault();
        _status.Text = message ?? $"Shared revision {read.Document.Revision}. {_selectedPaths.Count:N0} current selection(s) can be added.";
        RefreshMembers();
    }

    private void RefreshMembers()
    {
        AlbumEntry? album = SelectedAlbum;
        if (album is null)
        {
            _memberList.ItemsSource = null;
            return;
        }
        _name.Text = album.Name;
        _memberList.ItemsSource = album.Members.Select(member => new AlbumMemberListItem(member, Availability(member.ImagePath))).ToList();
    }

    private string Availability(string imagePath)
        => !File.Exists(imagePath) ? "missing" : _catalogPaths.Contains(imagePath) ? "current" : "outside catalog - unavailable in this WPF session";

    private void Apply(AlbumMutationResult result, string? albumId, string success)
    {
        if (!result.Ok)
        {
            Reload(albumId, result.Status == AlbumMutationStatus.Conflict
                ? "The Album library changed in another process. Latest state was reloaded; retry the operation."
                : $"Album operation failed without overwriting shared state: {result.Error ?? result.Status.ToString()}.");
            return;
        }
        _libraryChanged();
        Reload(albumId, success);
    }

    private void CreateAlbum()
    {
        string name = _name.Text.Trim();
        if (name.Length == 0) { _status.Text = "Enter an Album name."; return; }
        AlbumMutationResult result = AlbumStore.Create(_storePath, name, _document?.Revision);
        Apply(result, result.Album?.Id, $"Created Album {name}.");
    }

    private void RenameAlbum()
    {
        AlbumEntry? album = SelectedAlbum;
        if (album is null) return;
        Apply(AlbumStore.Update(_storePath, album.Id, _document?.Revision, name: _name.Text), album.Id, "Album renamed.");
    }

    private void TogglePin()
    {
        AlbumEntry? album = SelectedAlbum;
        if (album is null) return;
        Apply(AlbumStore.Update(_storePath, album.Id, _document?.Revision, pinned: !album.Pinned), album.Id, album.Pinned ? "Album unpinned." : "Album pinned.");
    }

    private void AddSelection()
    {
        AlbumEntry? album = SelectedAlbum;
        if (album is null || _selectedPaths.Count == 0) { _status.Text = "Select images in the gallery before opening Albums."; return; }
        Apply(AlbumStore.AddMembers(_storePath, album.Id, _selectedPaths, _document?.Revision), album.Id, $"Added {_selectedPaths.Count:N0} selected image(s). Existing members were left unchanged.");
    }

    private void RemoveMembers()
    {
        AlbumEntry? album = SelectedAlbum;
        var selected = _memberList.SelectedItems.OfType<AlbumMemberListItem>().ToList();
        if (album is null || selected.Count == 0) { _status.Text = "Select Album members to remove. Source images will not be recycled."; return; }
        Apply(AlbumStore.RemoveMembers(_storePath, album.Id, selected.Select(static item => item.Member.Id).ToList(), null, _document?.Revision), album.Id, $"Removed {selected.Count:N0} member(s) from the Album. Source images were not recycled.");
    }

    private void SetCover()
    {
        AlbumEntry? album = SelectedAlbum;
        AlbumMemberListItem? member = _memberList.SelectedItems.OfType<AlbumMemberListItem>().FirstOrDefault();
        if (album is null || member is null) { _status.Text = "Select one member to use as the cover."; return; }
        Apply(AlbumStore.Update(_storePath, album.Id, _document?.Revision, coverMemberId: member.Member.Id, updateCover: true), album.Id, "Album cover updated.");
    }

    private void DeleteAlbum()
    {
        AlbumEntry? album = SelectedAlbum;
        if (album is null) return;
        if (MessageBox.Show(this, $"Delete Album '{album.Name}'? Source images will not be recycled.", "Delete Album", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;
        Apply(AlbumStore.Delete(_storePath, album.Id, _document?.Revision), null, "Album deleted. Source images were not recycled.");
    }

    private void OpenAlbum()
    {
        AlbumEntry? album = SelectedAlbum;
        if (album is null) return;
        AlbumMutationResult recent = AlbumStore.MarkRecent(_storePath, album.Id, _document?.Revision);
        if (!recent.Ok) { Apply(recent, album.Id, ""); return; }
        AlbumEntry active = recent.Document?.Albums.FirstOrDefault(candidate => candidate.Id == album.Id) ?? album;
        _activateAlbum(active);
        Reload(album.Id, $"Opened {active.Name}. Existing members outside the current WPF catalog remain listed as unavailable; they were not dropped.");
    }

    private sealed record AlbumListItem(AlbumEntry Album)
    {
        public override string ToString() => $"{(Album.Pinned ? "[Pinned] " : "")}{Album.Name}  ({Album.Members.Count:N0})";
    }

    private sealed record AlbumMemberListItem(AlbumMemberEntry Member, string Availability)
    {
        public override string ToString() => $"{Path.GetFileName(Member.ImagePath)}  [{Availability}]";
    }
}
