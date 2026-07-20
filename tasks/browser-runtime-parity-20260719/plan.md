# Plan

1. Record exact Browser/WPF process, checkout, revision, build, and Favorite store provenance.
2. Make Grid zoom own Ctrl/Cmd wheel and `+` / `-` / `0`, keep surrounding UI scale fixed, and prefer the visible last-selected image as anchor.
3. Immediately flush visible thumbnail warmup with a bounded high-priority resend window while preserving nearby dedupe and direct `<img>` caching.
4. Retain bounded demand for Favorite/Unrated/Enhanced sparse paging until the match buffer or catalog tail is reached, then prove stale contexts cancel.
5. Resolve Browser shared stores to the main checkout and make shared Favorite state exact after one-time non-destructive local migration.
6. Exercise WPF modal Favorite through the actual `Button.ClickEvent`, then verify UI, disk, and reload.
7. Add WPF key-settings discovery, editing, validation, persistence, default reset, and real input-path tests without advertising unimplemented commands.
8. Profile and improve WPF 100,000-image startup/memory/responsiveness under the same fixture and preserve tail reachability plus virtualization.
9. Run focused and full unit tests, typecheck/build/lint, isolated production Playwright/DOM checks, cross-runtime TEMP-store checks, and WPF product checks.
10. Fast-forward the normal root, rebuild/restart only through project launchers, and confirm port 3000 plus the normal WPF process use the adopted revision.
11. Update contract/recap/SQLite/GitHub with evidence; leave the next product milestone for user choice.
