# WPF persistent prompt metadata index (2026-07-19)

## Objective

Keep the Browser-authoritative product contract and WPF full-catalog
virtualization while replacing the repeated 100,000-source-file prompt metadata
pass with a safe WPF-owned persistent index. The first catalog frame and any
visible thumbnail always remain higher priority than background metadata.

## Required invariants

- Publish the complete catalog before prompt/dimension metadata finishes.
- Keep exact 100,000-image / 100-folder reachability and bounded Grid/List
  realization.
- Share Browser/WPF Favorite, Seen, Recent, and passive Enhancement state exactly
  as before; the metadata index never owns those stores.
- Never mutate source images and never delete user state/cache as recovery.
- Validate source identity before every index hit.
- Detect header, truncation, and payload corruption; fail safe to source reads.
- Preserve a future-version index rather than downgrading it.
- Commit only a complete current-generation snapshot with same-directory temp,
  writer exclusion, durable flush, and atomic replacement.
- Cancellation, supersession, close, or catalog mutation keeps the last complete
  index intact.
- Show monotonic background progress and the final hit/refresh result in the
  Viewer, not only on the Landing scan panel.

## Verification

- Focused cold, warm restart, partial stale, corrupt checksum, future version,
  and background cancellation smoke under TEMP.
- Exact 100,000 / 100-folder cold and warm stress with catalog, virtualization,
  tail reachability, zoom anchor, dispatcher, and external WM_NULL gates.
- WPF product aggregate and cross-runtime shared-state regression.
- Normal launcher provenance/adoption without touching Browser port 3000.
