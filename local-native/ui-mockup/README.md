# PhotoViewer WPF UI Mockup

This folder contains the original portable shell mockup and rendered WPF
previews for the `surface:wpf` native lane. It is historical visual material,
not a product-behavior specification.

> NON-NORMATIVE: the HTML/screenshots still show retired shell ideas such as
> Quick Search, Today/date presets, threshold favorite chips, and old sidebar
> content. Do not copy those controls back into Browser or WPF. Current behavior
> is defined by `../../docs/browser-feature-contract.md`, then
> `../../docs/wpf-product-spec.md`. New screenshots must come from the real WPF
> `--shot` path and be labeled with the integrated commit.

- `photoviewer-ui-mockup.html`: self-contained historical shell reference, no
  server or external assets.
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
