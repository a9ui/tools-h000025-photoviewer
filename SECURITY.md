# Security Policy

## Supported versions

PhotoViewer has no published binary releases. Security fixes target the current
active `main` revision and are validated through the Browser and WPF gates in
this repository.

## Product security boundary

PhotoViewer is a local-first application. A public source repository does not
turn the application into a public service.

- The Browser runtime must bind to `127.0.0.1`.
- PhotoViewer APIs are not designed for LAN or Internet exposure.
- Reverse proxies, Cloudflare Tunnel, Vercel deployment, port forwarding, and
  non-loopback binding are unsupported.
- The APIs may read or act on local files explicitly selected by the operator.
- Source deletion is limited to the Windows Recycle Bin; hard-delete fallback is
  prohibited.
- Optional Enhancement work starts only from an explicit action. Ordinary
  browsing must not start workers or enqueue jobs.

Stop the runtime immediately if its provenance check reports a non-loopback
listener or an unexpected source revision.

## Reporting a vulnerability

Use **Security → Report a vulnerability** in this GitHub repository so the
report is handled through private vulnerability reporting. Please include:

- affected revision and surface (Browser or WPF),
- a concise source-to-impact description,
- safe reproduction steps using disposable fixtures,
- the expected and observed result,
- suggested remediation when available.

Do **not** open a public issue for a suspected vulnerability before the
maintainer has assessed it.

## Protect personal data

Do not include any of the following in a report, issue, pull request, test
fixture, log, or screenshot:

- personal images or image metadata,
- credentials, API keys, cookies, or tokens,
- an unredacted user profile or home-directory path,
- `.cache`, local state databases, or shared settings copied from a real user,
- production process dumps or logs containing local paths.

Use generated files under the system temporary directory and replace machine
specific paths with placeholders such as `<USER_HOME>` or `<PROJECT_ROOT>`.

## Public repository operations

Before changing this repository from private to public, maintainers must follow
[`docs/publication-runbook.md`](docs/publication-runbook.md). The gate includes
source review, full Git-history secret scanning, Issues/PR/Actions privacy
review, an explicit license decision, exact-SHA CI, repository rename while
still private, and a final human approval immediately before the visibility
change.

Changing visibility and later reverting it is not a rollback strategy: code,
logs, or metadata may already have been cloned, cached, or forked.
