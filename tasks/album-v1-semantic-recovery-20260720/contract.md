# Album v1 contract

## Durable document

- Path: shared project cache `albums.json`, resolved by the same Browser/WPF
  project-root resolver as Favorites, Seen, Recent, and Search History.
- Root: `version: 1`, monotonic non-negative `revision`, `albums`,
  `recentAlbumIds`, with unknown root fields preserved.
- Album: opaque `id`, non-empty `name`, `pinned`, optional `coverMemberId`,
  `createdAtUtc`, `updatedAtUtc`, monotonic `revision`, `members`; unknown fields
  preserved.
- Member: opaque `id`, canonical absolute `imagePath`, `addedAtUtc`; unknown
  fields preserved. Metadata remains owned by the active catalog/index and is
  not duplicated into Album storage.
- Membership identity is canonical path identity (Windows case-insensitive).
  Adding an existing identity is idempotent. A member id stays stable across
  catalog reloads and temporary disappearance.

Missing storage means a new empty v1 document. Malformed JSON, unsupported future
versions, invalid required values, and duplicate ids are protected errors, never
silently replaced by empty state. Unknown fields survive every supported write.

## Mutation and recovery

- Browser and WPF use the same target-scoped create-new `<target>.lock` protocol.
- Under the lock, every operation reloads the latest on-disk document, validates
  optional `expectedRevision`, mutates one operation, writes a same-directory
  temporary file, flushes, and atomically publishes it.
- Document revision increments once per successful non-idempotent operation;
  affected Album revision increments once. A stale expected revision returns a
  conflict without mutation. Failed validation/publish leaves the prior durable
  document readable.
- Full-document snapshot PUT is not an API operation.

## Operations

- create album; rename album; delete album;
- pin/unpin; set/clear cover; mark recent;
- add one or many members; remove one or many members;
- PhotoViewer-owned source Recycle cleanup removes the recycled canonical member
  identity from all Albums only after the source operation succeeds.

Deleting an Album never recycles source files. Removing membership never recycles
source files. A source Recycle action is separate and explicit. If post-Recycle
membership cleanup fails, source success is not reversed and the cleanup failure
is surfaced/retryable.

## Member availability

Runtime status is derived, not persisted as destructive cleanup:

- `current`: canonical path is present in the active catalog;
- `outside`: file exists and has a supported image type but is outside it;
- `missing`: canonical path no longer exists; keep it as a tombstone;
- `moved`: not guessed from filename. It is established only by explicit
  user-assisted relink or a future durable file-identity match, then performed as
  an operation preserving member id.

Album source/session ordering is the Album member order. Browser may serve an
outside member only through a guarded Album source session/token that validates
membership; WPF may load the same existing canonical member locally. If a safe
session cannot be established, Browser exposes `outside` as unavailable rather
than falling back to an unrestricted raw-path URL. Missing members remain visible
but unavailable in both clients.

## Browser/WPF interaction semantics

- Library and picker expose create/rename/delete/pin/cover/recent/member counts.
- Picker supports one/bulk add and create-then-add.
- Opening an Album creates an explicit Gallery/Modal source; it never overwrites
  Search results. Modal navigation and Filmstrip use the active source order.
- In-Album remove and source Recycle are separate controls and labels.
- Modal close restores focus/reveal through the current source contract.
- Album dialogs use the shared dialog stack/focus trap/opener restoration and
  suppress page/modal/grid shortcuts while topmost.
- `addToAlbum` is configurable. Migration allocates a key only after considering
  all stored/default bindings, including the existing filmstrip migration; `B`
  is preferred but never steals an occupied key.

## Scale boundary

Storage mutation is proportional to the Album document, never the active catalog.
Catalog membership lookup uses canonical-path sets/maps. UI materializes only the
active Album and remains virtualized. Acceptance includes 100,000 catalog items,
large Album bulk mutation, Browser/WPF interleaving, stale-revision conflict, and
failure non-mutation.
