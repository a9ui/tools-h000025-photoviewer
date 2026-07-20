# Album v1 source audit and frozen provenance

## Read-only start snapshot (2026-07-20 JST)

| Evidence | Value |
| --- | --- |
| local `main` | `8914935f3118f5634d22570be7d207fbc7735340` |
| PR #322 head | `8914935f3118f5634d22570be7d207fbc7735340` |
| `origin/main` | `626b7dd5416f3619ae59fc66d47e79acd1a74fd5` |
| PR/local tree | `7ad395735a8043dd93f114e2c225e9e9630802b3` |
| PR merge ref | `e5c335369a50509c81d767fc5dc4368430eee6e9` |
| PR merge candidate tree | `7ad395735a8043dd93f114e2c225e9e9630802b3` |
| Claude branch/head | `claude/photoviewer-local-ui-f62c30` / `c63df41220087cb323faf81f0c18bf187a4b68b3` |
| Album MVP | `d2a99c41418ac88933b84db66a223b3c7e592d99` |
| Shortcut follow-up | `a904d937e43de8cbcaf7b57a0fcb3711d175101a` |
| Overlay guard | `c63df41220087cb323faf81f0c18bf187a4b68b3` |

At the snapshot, `origin/main` is an ancestor of PR head, PR head and local main
have zero commit/tree diff, and the GitHub merge-candidate tree equals PR head
tree. `8914935` is therefore the authoritative implementation baseline and
already includes every Browser/WPF update in #322. The old `origin/main`
`626b7dd` is provenance evidence only, never an implementation base. The first
Album commit on local main makes PR #322 stale.

**MERGE FROZEN is an operational GitHub boundary only:** this lane does not push,
merge, or close #322. It does not freeze, exclude, or ignore #322's content.

Claude worktree is intentionally dirty only by untracked `local-native/`. All
four source commits (`b584744`, Album MVP, shortcut, overlay guard) are reachable
from the Claude branch and not from local main. Do not clean or delete that
worktree/branch until semantic recovery, tests, docs, and runtime evidence are
green and the recovered work is reachable from local main.

## Commit disposition

| Commit | Disposition | Safe meaning to recover | Rejected implementation |
| --- | --- | --- | --- |
| `b584744` | REJECT | none as a unit | old viewer navigation, Delete and performance changes; whole commit forbidden |
| `d2a99c4` | PARTIAL | Album product concept; library/picker information architecture; create/rename/delete/add/remove/pin/cover/recent operations | `.cache/albums.json`, full unguarded rewrites, duplicated image metadata, old `ImageContext`, raw path URLs, old Modal/Grid/Sidebar/page integration |
| `a904d93` | PARTIAL | configurable add-to-album shortcut and settings affordance | fixed `B` default without collision-aware migration |
| `c63df41` | PARTIAL | Album overlays must isolate global shortcuts | selector-only guards tied to old overlays; current dialog stack/focus contract must own isolation |

## Required pre-merge proof (not authorized in this milestone)

Immediately before any future merge candidacy, capture all of the following on
the final adopted head. Any failure keeps #322 frozen.

1. exact local main, PR head, and origin main SHA;
2. `origin/main` is an ancestor of PR head;
3. PR head tree equals the selected local main tree;
4. `git log PR_HEAD..LOCAL_MAIN` is empty;
5. GitHub PR merge-candidate tree equals PR head tree;
6. Claude Album commits, branch, and dirty/untracked worktree remain reachable
   and protected until recovery is proven;
7. Browser/WPF authoritative regression matrix and normal launcher provenance
   are rerun at that exact final head.

No push, merge, or close is permitted without the user's explicit approval even
after every proof is green.
