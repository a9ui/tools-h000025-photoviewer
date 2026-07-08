namespace PhotoViewer.Native;

internal sealed record NativeImportWarning(
    string Source,
    string Path,
    string Code,
    string Message,
    string RecoveryAction);
