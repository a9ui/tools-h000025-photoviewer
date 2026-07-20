# PhotoViewer

PhotoViewer is a local-first Windows image viewer for large illustration and
photo folders. It provides a Next.js browser interface and a native .NET 8 WPF
interface over the same local-first product model: fast scanning, virtualized
browsing, search, Favorite and Seen state, Albums, Recycle Bin operations, and
explicit optional enhancement jobs.

> [!IMPORTANT]
> Publishing this source repository does **not** make PhotoViewer a hosted
> service. The Browser runtime binds to `127.0.0.1`, the APIs can act on local
> files selected by the operator, and LAN or Internet exposure is unsupported.

## Product surfaces

- **Browser:** Next.js and React, served on IPv4 loopback only.
- **WPF:** native .NET 8 Windows UI that shares supported local state with the
  Browser implementation.
- **WinForms:** preserved as a frozen historical surface. New product features
  are not added there.

## Safety invariants

- Source images are not rewritten by normal viewing or metadata extraction.
- Source deletion uses the Windows Recycle Bin; there is no hard-delete
  fallback.
- Ordinary browsing, search, preview, and modal navigation never enqueue an
  enhancement job or start a worker.
- Browser file APIs require an active viewer session and the shared loopback
  Host/Origin guard.
- User cache and state are migrated non-destructively; malformed or newer shared
  files are not silently replaced.

The integrated product contract is in
[`docs/photoviewer-authoritative-spec.md`](docs/photoviewer-authoritative-spec.md).

## Requirements

- Windows 10 or later
- Node.js 22 recommended (`>=20.9.0` supported by the package metadata)
- pnpm 11
- .NET 8 SDK for the WPF application

Optional enhancement backends such as ComfyUI and Real-ESRGAN ncnn-vulkan are
external local tools. They are not downloaded or started during ordinary
browsing.

## Browser setup

```powershell
corepack pnpm install --frozen-lockfile
corepack pnpm dev
```

The development command binds to `127.0.0.1`. For a production build and local
launcher:

```powershell
corepack pnpm build
node .\scripts\prod_launcher.js
node .\scripts\prod_launcher.js --port 3100
```

An explicit busy port fails without killing or replacing its listener. The
launcher may select another loopback port only when no explicit port was
requested.

## WPF setup

```powershell
dotnet build .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj -c Release
.\start_wpf.bat
```

WPF normal browsing does not require the Browser server. Only explicit managed
Enhancement actions may call the loopback Browser engine.

## Verification

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1

dotnet build `
  .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj `
  -c Release `
  --nologo
```

Focused WPF gates include:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-ui-regression-guard.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p0.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p1a.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-p1b.ps1
```

Tests must use disposable fixtures under the system temporary directory. Do not
use personal image libraries, shared user state, or the normal user-owned port
3000 as test fixtures.

## Repository publication

The intended public repository name is **`H000025-PhotoViewer`**. Repository
visibility changes follow
[`docs/public-repository-policy.md`](docs/public-repository-policy.md) and
[`docs/publication-runbook.md`](docs/publication-runbook.md). The repository
must remain private until the source-security, Git-history privacy, license,
CI, and GitHub-settings gates are all complete.

Public source does not authorize Vercel deployment, Cloudflare Tunnel exposure,
reverse proxying, or binding PhotoViewer to a non-loopback interface.

## Contributing and security

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a pull request. Report
security vulnerabilities through GitHub private vulnerability reporting as
described in [`SECURITY.md`](SECURITY.md); do not post exploit details, private
images, credentials, or unredacted absolute paths in a public issue.

## License

A public-source license has not yet been selected. Until a root `LICENSE` file
is deliberately added by the repository owner, no permission to copy, modify,
or redistribute this project is granted beyond rights provided by applicable
law. See [`docs/license-decision.md`](docs/license-decision.md).
