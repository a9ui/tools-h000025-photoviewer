using System.Text.Json;
using System.Windows.Input;

namespace PhotoViewer.Wpf;

[Flags]
internal enum ShortcutContext
{
    None = 0,
    Gallery = 1,
    Modal = 2,
    Viewer = Gallery | Modal,
}

internal enum ViewerKeyAction
{
    PreviousImage,
    NextImage,
    CloseModal,
    FavoriteIncrease,
    FavoriteDecrease,
    FavoriteLevel1,
    FavoriteLevel2,
    FavoriteLevel3,
    FavoriteLevel4,
    FavoriteLevel5,
    RecycleCurrentImage,
    SelectAllResults,
    ClearSelection,
    ReopenLastClosedPreviewTab,
    MovePreviewTabLeft,
    MovePreviewTabRight,
    FlipHorizontal,
    ToggleEnhancedPreview,
    GalleryZoomIn,
    GalleryZoomOut,
    GalleryZoomReset,
    ModalZoomIn,
    ModalZoomOut,
    ModalZoomReset,
    ToggleModalFilmstrip,
}

internal sealed record KeyBindingDefinition(
    ViewerKeyAction Action,
    string StorageName,
    string Label,
    string HelpText,
    ShortcutContext Context,
    KeyChord DefaultChord);

internal readonly record struct KeyChord(Key Key, ModifierKeys Modifiers)
{
    private const ModifierKeys SupportedModifiers =
        ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows;

    public string CanonicalText
    {
        get
        {
            var parts = new List<string>(5);
            if ((Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(KeyToken(Key));
            return string.Join("+", parts);
        }
    }

    public string DisplayText
    {
        get
        {
            var parts = new List<string>(5);
            if ((Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(NormalizeKey(Key) switch
            {
                Key.OemPlus => "+",
                Key.OemMinus => "\u2212",
                _ => KeyToken(Key),
            });
            return string.Join(" + ", parts);
        }
    }

    public bool Matches(Key key, ModifierKeys modifiers)
    {
        if (!TryCreate(key, modifiers, out KeyChord candidate, out _))
            return false;
        return candidate == this;
    }

    public static bool TryCreate(Key key, ModifierKeys modifiers, out KeyChord chord, out string error)
    {
        key = NormalizeKey(key);
        modifiers &= SupportedModifiers;
        if (key == Key.OemPlus)
        {
            // Shift is commonly required to type '+'; it is part of the glyph,
            // not a separate shortcut modifier for this key.
            modifiers &= ~ModifierKeys.Shift;
        }

        if (key is Key.None
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            or Key.System or Key.DeadCharProcessed or Key.ImeProcessed)
        {
            chord = default;
            error = "Press a non-modifier key. Modifier-only shortcuts are not valid.";
            return false;
        }

        if (TryGetReservedReason(key, modifiers, out error))
        {
            chord = default;
            return false;
        }

        chord = new KeyChord(key, modifiers);
        error = "";
        return true;
    }

    private static bool TryGetReservedReason(Key key, ModifierKeys modifiers, out string error)
    {
        if (key == Key.Tab)
        {
            error = "Tab combinations are reserved for keyboard focus navigation.";
            return true;
        }

        if ((modifiers & ModifierKeys.Alt) != 0 && (key is Key.F4 or Key.Space))
        {
            error = "That Alt combination is reserved by Windows.";
            return true;
        }

        if (key == Key.Delete
            && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt))
                == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            error = "Ctrl + Alt + Delete is reserved by Windows.";
            return true;
        }

        if (key == Key.Escape
            && (modifiers & (ModifierKeys.Control | ModifierKeys.Shift))
                == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            error = "Ctrl + Shift + Escape is reserved by Windows.";
            return true;
        }

        if (key == Key.Escape && (modifiers & ModifierKeys.Control) != 0)
        {
            error = "Ctrl + Escape combinations are reserved by Windows.";
            return true;
        }

        if (key == Key.Escape && (modifiers & ModifierKeys.Alt) != 0)
        {
            error = "Alt + Escape combinations are reserved by Windows.";
            return true;
        }

        if ((modifiers & ModifierKeys.Windows) != 0)
        {
            error = "Windows-key combinations are owned by the operating system and cannot be used reliably by this app.";
            return true;
        }

        error = "";
        return false;
    }

    public static bool TryParse(string? value, out KeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            return false;

        string[] tokens = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return false;

        ModifierKeys modifiers = ModifierKeys.None;
        for (int index = 0; index < tokens.Length - 1; index++)
        {
            switch (tokens[index].ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "alt":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "shift":
                    modifiers |= ModifierKeys.Shift;
                    break;
                case "win":
                case "windows":
                case "meta":
                    modifiers |= ModifierKeys.Windows;
                    break;
                default:
                    return false;
            }
        }

        if (!TryParseKeyToken(tokens[^1], out Key key))
            return false;
        return TryCreate(key, modifiers, out chord, out _);
    }

    private static Key NormalizeKey(Key key) => key switch
    {
        Key.Add => Key.OemPlus,
        Key.Subtract => Key.OemMinus,
        Key.NumPad0 => Key.D0,
        Key.NumPad1 => Key.D1,
        Key.NumPad2 => Key.D2,
        Key.NumPad3 => Key.D3,
        Key.NumPad4 => Key.D4,
        Key.NumPad5 => Key.D5,
        Key.NumPad6 => Key.D6,
        Key.NumPad7 => Key.D7,
        Key.NumPad8 => Key.D8,
        Key.NumPad9 => Key.D9,
        _ => key,
    };

    private static string KeyToken(Key key) => NormalizeKey(key) switch
    {
        Key.Left => "Left",
        Key.Right => "Right",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Escape => "Escape",
        Key.Delete => "Delete",
        Key.Back => "Backspace",
        Key.Return => "Enter",
        Key.Space => "Space",
        Key.OemPlus => "Plus",
        Key.OemMinus => "Minus",
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
        _ => NormalizeKey(key).ToString(),
    };

    private static bool TryParseKeyToken(string token, out Key key)
    {
        string normalized = token.Trim();
        key = normalized.ToLowerInvariant() switch
        {
            "left" or "arrowleft" => Key.Left,
            "right" or "arrowright" => Key.Right,
            "up" or "arrowup" => Key.Up,
            "down" or "arrowdown" => Key.Down,
            "esc" or "escape" => Key.Escape,
            "delete" or "del" => Key.Delete,
            "backspace" => Key.Back,
            "enter" or "return" => Key.Return,
            "space" => Key.Space,
            "+" or "plus" or "=" or "oemplus" or "add" => Key.OemPlus,
            "-" or "minus" or "oemminus" or "subtract" => Key.OemMinus,
            "0" => Key.D0,
            "1" => Key.D1,
            "2" => Key.D2,
            "3" => Key.D3,
            "4" => Key.D4,
            "5" => Key.D5,
            "6" => Key.D6,
            "7" => Key.D7,
            "8" => Key.D8,
            "9" => Key.D9,
            _ => Key.None,
        };
        if (key != Key.None)
            return true;

        return Enum.TryParse(normalized, ignoreCase: true, out key) && key != Key.None;
    }
}

internal static class KeyBindingSettings
{
    public static IReadOnlyList<KeyBindingDefinition> Definitions { get; } =
    [
        Def(ViewerKeyAction.PreviousImage, "previousImage", "Previous image", "Navigate to the previous image while the modal is open.", ShortcutContext.Modal, Key.Left),
        Def(ViewerKeyAction.NextImage, "nextImage", "Next image", "Navigate to the next image while the modal is open.", ShortcutContext.Modal, Key.Right),
        Def(ViewerKeyAction.CloseModal, "closeModal", "Back to gallery / close modal", "Close the image modal. App Settings and Recycle confirmation always retain Escape as a fixed rescue key.", ShortcutContext.Modal, Key.Escape),
        Def(ViewerKeyAction.FavoriteIncrease, "favoriteIncrease", "Favorite +1", "Increase the selected image or selected images by one Favorite level.", ShortcutContext.Viewer, Key.F),
        Def(ViewerKeyAction.FavoriteDecrease, "favoriteDecrease", "Favorite −1", "Decrease the selected image or selected images by one Favorite level.", ShortcutContext.Viewer, Key.U),
        Def(ViewerKeyAction.FavoriteLevel1, "favoriteLevel1", "Set Favorite Lv1", "Set the selected image or selected images to exact Favorite level 1.", ShortcutContext.Viewer, Key.D1, ModifierKeys.Control),
        Def(ViewerKeyAction.FavoriteLevel2, "favoriteLevel2", "Set Favorite Lv2", "Set the selected image or selected images to exact Favorite level 2.", ShortcutContext.Viewer, Key.D2, ModifierKeys.Control),
        Def(ViewerKeyAction.FavoriteLevel3, "favoriteLevel3", "Set Favorite Lv3", "Set the selected image or selected images to exact Favorite level 3.", ShortcutContext.Viewer, Key.D3, ModifierKeys.Control),
        Def(ViewerKeyAction.FavoriteLevel4, "favoriteLevel4", "Set Favorite Lv4", "Set the selected image or selected images to exact Favorite level 4.", ShortcutContext.Viewer, Key.D4, ModifierKeys.Control),
        Def(ViewerKeyAction.FavoriteLevel5, "favoriteLevel5", "Set Favorite Lv5", "Set the selected image or selected images to exact Favorite level 5.", ShortcutContext.Viewer, Key.D5, ModifierKeys.Control),
        Def(ViewerKeyAction.RecycleCurrentImage, "recycleSelected", "Move current image to Recycle Bin", "Run the guarded single-image Recycle Bin flow for the primary selection. Confirmation remains in force.", ShortcutContext.Viewer, Key.Delete),
        Def(ViewerKeyAction.SelectAllResults, "selectAllResults", "Select all current results", "Select every image in the current filtered result.", ShortcutContext.Gallery, Key.A, ModifierKeys.Control),
        Def(ViewerKeyAction.ClearSelection, "clearSelection", "Clear image selection", "Clear the current image selection and preview.", ShortcutContext.Gallery, Key.A, ModifierKeys.Control | ModifierKeys.Shift),
        Def(ViewerKeyAction.ReopenLastClosedPreviewTab, "reopenLastClosedPreviewTab", "Reopen last closed preview tab", "Restore the most recently closed preview tab.", ShortcutContext.Viewer, Key.T, ModifierKeys.Control | ModifierKeys.Shift),
        Def(ViewerKeyAction.MovePreviewTabLeft, "movePreviewTabLeft", "Move preview tab left", "Move the focused preview tab one position to the left.", ShortcutContext.Viewer, Key.Left, ModifierKeys.Alt | ModifierKeys.Shift),
        Def(ViewerKeyAction.MovePreviewTabRight, "movePreviewTabRight", "Move preview tab right", "Move the focused preview tab one position to the right.", ShortcutContext.Viewer, Key.Right, ModifierKeys.Alt | ModifierKeys.Shift),
        Def(ViewerKeyAction.FlipHorizontal, "flipHorizontal", "Flip modal image", "Flip the current modal image horizontally. This does not modify the source file.", ShortcutContext.Modal, Key.H),
        Def(ViewerKeyAction.ToggleEnhancedPreview, "toggleEnhancedPreview", "Toggle Original / Enhanced preview", "Switch only between the original and an already-succeeded managed output. This never creates or starts an enhancement job.", ShortcutContext.Modal, Key.E),
        Def(ViewerKeyAction.GalleryZoomIn, "galleryZoomIn", "Gallery zoom in", "Increase Grid card size without changing the sidebar, header, or fonts.", ShortcutContext.Gallery, Key.OemPlus, ModifierKeys.Control),
        Def(ViewerKeyAction.GalleryZoomOut, "galleryZoomOut", "Gallery zoom out", "Decrease Grid card size without changing the sidebar, header, or fonts.", ShortcutContext.Gallery, Key.OemMinus, ModifierKeys.Control),
        Def(ViewerKeyAction.GalleryZoomReset, "galleryZoomReset", "Reset gallery zoom", "Reset Grid card size to 200.", ShortcutContext.Gallery, Key.D0, ModifierKeys.Control),
        Def(ViewerKeyAction.ModalZoomIn, "modalZoomIn", "Modal zoom in", "Increase modal image zoom.", ShortcutContext.Modal, Key.OemPlus),
        Def(ViewerKeyAction.ModalZoomOut, "modalZoomOut", "Modal zoom out", "Decrease modal image zoom.", ShortcutContext.Modal, Key.OemMinus),
        Def(ViewerKeyAction.ModalZoomReset, "modalZoomReset", "Reset modal zoom", "Reset modal zoom, pan, and flip state.", ShortcutContext.Modal, Key.D0),
        Def(ViewerKeyAction.ToggleModalFilmstrip, "toggleModalFilmstrip", "Toggle modal filmstrip", "Pin or unpin the modal thumbnail filmstrip. Bottom-edge hover remains transient.", ShortcutContext.Modal, Key.T),
    ];

    private static readonly Dictionary<ViewerKeyAction, KeyBindingDefinition> ByAction =
        Definitions.ToDictionary(static definition => definition.Action);
    private static readonly Dictionary<string, KeyBindingDefinition> ByStorageName =
        Definitions.ToDictionary(static definition => definition.StorageName, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<ViewerKeyAction, KeyChord> CreateDefaults()
        => Definitions.ToDictionary(static definition => definition.Action, static definition => definition.DefaultChord);

    public static Dictionary<ViewerKeyAction, KeyChord> NormalizePersisted(
        IReadOnlyDictionary<string, JsonElement>? persisted,
        out Dictionary<string, JsonElement>? unknownEntries)
    {
        var normalized = CreateDefaults();
        Dictionary<string, JsonElement>? unknown = null;
        if (persisted is not null)
        {
            foreach ((string name, JsonElement value) in persisted)
            {
                if (!ByStorageName.TryGetValue(name, out KeyBindingDefinition? definition))
                {
                    unknown ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    unknown[name] = value.Clone();
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String
                    && KeyChord.TryParse(value.GetString(), out KeyChord chord)
                    && IsAllowedForAction(definition.Action, chord, out _))
                {
                    normalized[definition.Action] = chord;
                }
            }
        }

        // A hand-edited or future file must never leave active shortcut
        // contexts ambiguous. Fall back atomically to the proven defaults.
        if (FindConflicts(normalized).Count > 0)
            normalized = CreateDefaults();
        unknownEntries = unknown;
        return normalized;
    }

    public static Dictionary<string, JsonElement> ToPersisted(
        IReadOnlyDictionary<ViewerKeyAction, KeyChord> bindings,
        IReadOnlyDictionary<string, JsonElement>? unknownEntries)
    {
        var persisted = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (unknownEntries is not null)
        {
            foreach ((string name, JsonElement value) in unknownEntries)
            {
                if (!ByStorageName.ContainsKey(name))
                    persisted[name] = value.Clone();
            }
        }
        foreach (KeyBindingDefinition definition in Definitions)
        {
            KeyChord chord = bindings.TryGetValue(definition.Action, out KeyChord value)
                ? value
                : definition.DefaultChord;
            persisted[definition.StorageName] = JsonSerializer.SerializeToElement(chord.CanonicalText);
        }
        return persisted;
    }

    public static IReadOnlyDictionary<ViewerKeyAction, IReadOnlyList<ViewerKeyAction>> FindConflicts(
        IReadOnlyDictionary<ViewerKeyAction, KeyChord> bindings)
    {
        var conflicts = new Dictionary<ViewerKeyAction, List<ViewerKeyAction>>();
        for (int firstIndex = 0; firstIndex < Definitions.Count; firstIndex++)
        {
            KeyBindingDefinition first = Definitions[firstIndex];
            KeyChord firstChord = bindings.TryGetValue(first.Action, out KeyChord firstValue)
                ? firstValue
                : first.DefaultChord;
            for (int secondIndex = firstIndex + 1; secondIndex < Definitions.Count; secondIndex++)
            {
                KeyBindingDefinition second = Definitions[secondIndex];
                if ((first.Context & second.Context) == ShortcutContext.None)
                    continue;
                KeyChord secondChord = bindings.TryGetValue(second.Action, out KeyChord secondValue)
                    ? secondValue
                    : second.DefaultChord;
                if (firstChord != secondChord)
                    continue;
                AddConflict(conflicts, first.Action, second.Action);
                AddConflict(conflicts, second.Action, first.Action);
            }
        }
        return conflicts.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<ViewerKeyAction>)pair.Value);
    }

    public static KeyBindingDefinition Definition(ViewerKeyAction action) => ByAction[action];

    public static bool IsAllowedForAction(ViewerKeyAction action, KeyChord chord, out string error)
    {
        if ((chord.Modifiers & ModifierKeys.Windows) != 0)
        {
            error = "Windows-key combinations cannot be assigned because Windows may consume them before PhotoViewer.";
            return false;
        }

        error = "";
        return true;
    }

    private static KeyBindingDefinition Def(
        ViewerKeyAction action,
        string storageName,
        string label,
        string helpText,
        ShortcutContext context,
        Key key,
        ModifierKeys modifiers = ModifierKeys.None)
        => new(action, storageName, label, helpText, context, new KeyChord(key, modifiers));

    private static void AddConflict(
        Dictionary<ViewerKeyAction, List<ViewerKeyAction>> conflicts,
        ViewerKeyAction action,
        ViewerKeyAction other)
    {
        if (!conflicts.TryGetValue(action, out List<ViewerKeyAction>? list))
        {
            list = [];
            conflicts[action] = list;
        }
        list.Add(other);
    }
}
