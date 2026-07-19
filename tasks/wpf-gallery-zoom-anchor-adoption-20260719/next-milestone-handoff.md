# Next milestone handoff

Date: 2026-07-19 JST

## Milestone

H000025 WPF gallery zoom current-main promotion and normal-runtime acceptance.

## Start contract

1. Create the Goal before any task action.
2. Read the full AGENTS hierarchy and, in order, this task's `recap.md`,
   `reflection-proposal.md`, this handoff, `docs/photoviewer-authoritative-spec.md`,
   `docs/current-implementation-truth.md`, and `docs/wpf-product-spec.md`.
3. Start from the live local `refs/heads/main` at task start. At this closeout it
   is `4c81cca3efc80363568d0d9af35297ff3285b48c`; do not substitute stale
   `origin/main`.
4. Inspect implementation commit `4a22b61` and the documentation commit that
   contains this handoff. Do not blindly cherry-pick overlapping documentation.

## Required work

- Semantically adopt the `4a22b61` WPF code onto current local main while
  preserving the repository-hardening changes in `4c81cca3` and any newer main
  work.
- Preserve Browser/WPF shared Search History async/keyboard/accessibility,
  GUID-isolated stall verification, and shared Favorite/Seen/Recent behavior.
- Reconcile authoritative/spec/truth docs only after the promoted main state is
  known; change `pending adoption` to `implemented` only with evidence.
- Run Release build, focused zoom/anchor, Search History focused/stall,
  current aggregate with reload soak, and normal WPF launcher provenance plus
  one responding exact process check.
- Re-run exact 100,000 / 100 folders if semantic reconciliation changes WPF
  layout, state, or stress-verifier code; otherwise retain this task's exact
  evidence and state why it remains applicable.

## Boundaries

Do not touch Browser port 3000, deployment, WinForms, or unrelated dirty files.
Do not use stale `origin/main`. Do not make GitHub Actions or external
consultation a gate. Keep user state/cache/source unchanged except for the
minimal normal-WPF runtime evidence explicitly authorized by this next
milestone. Do not push unless separately requested.

## Completion route

After promotion and normal-runtime acceptance, reflect the result in live
GitHub and SQLite using `reflection-proposal.md`, write the closeout packet, and
create the following fresh task for displayed Original/Enhanced Enter plus WPF
List Enter-to-Modal parity.
