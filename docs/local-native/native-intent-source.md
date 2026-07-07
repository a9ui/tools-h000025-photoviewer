# Local Native Intent Source

Date: 2026-07-08

Canonical thread: `019f3c2e-577f-7421-a499-145ece67eb30`

## Decision

Treat the canonical thread as the product-intent source for the H000025
local-native lane.

The target is not a browser wrapper, webview shell, localhost app, or "same app
but packaged locally." The target is a fast native local PhotoViewer that keeps
the browser PhotoViewer feature set and user workflows while removing normal-use
dependency on Node, Next.js, Chrome/browser runtime, React/DOM rendering, HTTP
API image paths, Blob/Object URL image loading, and local server startup.

## Product Intent From The Canonical Thread

- The old EXE-era viewer comparison is the performance bar.
- Browser/server layering is a real disadvantage for massive image browsing,
  especially startup, large folder scan, thumbnail/grid display, and repeated
  next/previous viewing.
- The browser app should remain preserved as the current product and feature
  baseline.
- The local-native lane must move browser PhotoViewer behavior into native
  local implementation, then continue into acceleration work.
- M1 was only a foothold. It is not the stopping point.
- Browser features must remain available in the native direction: folder
  workflow, scan, grid/list browsing, preview/detail viewer, favorites,
  albums/history/settings migration, search/filtering, delete behavior, and
  enhancement controls where explicitly supported.
- Thumbnails do not disappear in native. What disappears is the browser/HTTP
  thumbnail delivery model. Native still needs local display-size caches,
  decoded image caches, or equivalent fast preview assets.
- Existing state must be carried forward where practical:
  `.cache/favorites.json`, `.cache/settings.json`, `.cache/albums.json`,
  index/folder history candidates, and explicit browser `pvu_*` exports.

## M5/M6 Implication

M5 release-candidate readiness is a review surface, not product completion.
Before the stack is merge-ready, M6 must classify browser-feature/native-parity
coverage row by row and must not count browser, Node, local HTTP server, or
webview evidence as native acceptance by itself.

Browser E2E and browser smoke tests are useful only to prove the preserved
browser baseline still works. Native acceptance must come from native build,
native headless checks, native UI checks, SQLite state evidence, and measured
local performance.
