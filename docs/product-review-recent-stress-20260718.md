# Recent three-owner repeat evidence

Date: 2026-07-18 JST

`scripts/verify-cross-runtime-recent.ps1` uses a fresh temp root and no Browser
server. It races 20 Browser API-route writes, 20 real WPF writer writes, and 20
independent protocol-compatible writes against one `recent-folders.json`.

Acceptance requires valid JSON, version-1 schema plus unknown-field retention,
all three owner latest markers in `recentFolderSets`, no lock or temporary-file
residue, and no source/user cache touch. The history is intentionally capped at
12 distinct folder sets. Since `lastFolderSet` is one schema slot rather than a
per-owner map, the defined result is last successful lock holder wins; each
owner's durable latest marker is instead proven in the additive history.
