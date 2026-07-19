# Security Policy

PhotoViewer is a local-first application with no supported public deployment.
Do not commit personal media, generated caches, local state databases,
credentials, API keys, or machine-specific secrets to this repository.

Run the application only through its normal launchers and require the HTTP
runtime to bind to `127.0.0.1`. Stop the runtime if its provenance check reports
a non-loopback bind. PhotoViewer APIs can read or act on explicitly selected
local files and are not designed for LAN or Internet exposure.

## Reporting a vulnerability

Please use **Security > Report a vulnerability** in this GitHub repository so
the report is handled privately. Do not include credentials, private images, or
other personal data in an issue or public pull request.

There are no published releases. Security fixes target the current active
development revision and are validated through local Browser and WPF gates.
