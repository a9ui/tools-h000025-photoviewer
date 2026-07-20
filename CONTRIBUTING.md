# Contributing to PhotoViewer

Thank you for helping improve PhotoViewer. The project values small,
source-backed changes that preserve local data, keep the runtime lightweight,
and behave consistently across Browser and WPF where the product contract
requires parity.

## Before starting

1. Read `README.md`, `SECURITY.md`, `PROJECT.md`, and `DESIGN.md`.
2. Read `docs/photoviewer-authoritative-spec.md` for product semantics and
   ownership boundaries.
3. Search existing issues and pull requests before opening a duplicate.
4. For security concerns, use private vulnerability reporting instead of a
   public issue.

## Development setup

```powershell
corepack pnpm install --frozen-lockfile
corepack pnpm dev
```

For WPF:

```powershell
dotnet build .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -c Release
```

Use Windows and disposable fixtures under the system temporary directory for
filesystem, Recycle Bin, metadata, or cross-runtime tests.

## Non-negotiable product rules

- Keep the Browser HTTP runtime on `127.0.0.1`.
- Do not add LAN or Internet deployment as part of an ordinary feature PR.
- Do not add hard-delete fallback. Source deletion must use the Windows Recycle
  Bin and must preserve state when the recycle operation fails.
- Do not rewrite source images during viewing, indexing, or enhancement.
- Do not start Enhancement jobs or workers from browsing, scanning, search,
  preview, modal navigation, or thumbnail decode.
- Preserve Browser/WPF ownership of shared state and latest-on-disk merge
  semantics.
- Do not delete user cache or state as a migration or repair strategy.
- Keep WinForms frozen. Only critical break/fix maintenance belongs there.
- Do not restore removed legacy UI such as Quick Search presets, relative date
  presets, rainbow status presentation, bottom Filmstrip, or circle-only modal
  navigation.

## Privacy-safe fixtures and reports

Never use or commit:

- personal images,
- real `.cache` or shared state,
- local databases,
- credentials or tokens,
- screenshots with private metadata,
- unredacted home-directory paths.

Use placeholders such as `<USER_HOME>` and `<PROJECT_ROOT>`. Test cleanup must
refuse to remove any path outside the disposable temporary root it created.

## Pull requests

Keep a PR focused on one root problem. In the description, include:

- user-visible behavior and affected surfaces,
- exact source and test files changed,
- safety and state-ownership impact,
- commands actually executed,
- known limitations or tests not run.

Security-sensitive changes must trace the attacker-controlled input, closest
control, dangerous sink or broken invariant, and regression test. A document
that says a check is green is not a substitute for the source and test result.

## Required verification

Browser changes normally require:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
```

WPF changes require a Release build and the focused verifier for the affected
surface. Cross-surface changes require both Browser and WPF evidence.

Do not run `pnpm typecheck` and `pnpm build` concurrently because both use
`.next/types`. Do not rebuild the shared `.next` directory behind a live
user-owned production process.

Large 20,000/100,000-item stress gates are required only when the change touches
the relevant virtualization, materialization, metadata-index, or stress-verifier
paths, or when a reviewer identifies a scale-specific risk.

## Accessibility and UI

- Enabled controls need visible hover and keyboard-focus states.
- Icon-only actions need a non-empty accessible name and explanatory tooltip.
- Dialogs must retain an Escape rescue path, focus cycle, and sensible focus
  return.
- Browser and WPF may use native presentation patterns, but must preserve the
  same product result and safety semantics.

## License and third-party code

Do not copy code, assets, models, or generated output unless its license and
attribution obligations are understood and compatible with the repository's
selected license. External ComfyUI, Real-ESRGAN, binaries, and model files are
not automatically part of this repository's license.
