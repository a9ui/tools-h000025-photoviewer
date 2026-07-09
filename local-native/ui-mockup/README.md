# PhotoViewer WPF UI Mockup

This folder contains the portable design reference and rendered WPF previews for
the `surface:wpf` native lane.

- `photoviewer-ui-mockup.html`: self-contained visual reference, no server or
  external assets.
- `wpf-preview.png`: default grid preview.
- `wpf-list.png`: list preview.
- `wpf-landing.png`: landing/folder-selection preview.
- `wpf-modal-preview.png`: modal preview.
- `wpf-settings.png`: settings overlay preview.
- `wpf-album.png`: album picker preview.
- `wpf-enhance.png`: enhance queue drawer preview.
- `wpf-confirm.png`: confirm delete dialog preview.

The real WPF project lives in `../PhotoViewer.Wpf`.

Current WPF behavior:

- The app can still render these shell states through `--shot --screen <state>`.
- The app can load a real image folder through the native folder picker.
- The app can also produce a real-folder smoke capture with
  `--shot --folder <path>`.
- Browsing and preview do not start enhancement work automatically.
