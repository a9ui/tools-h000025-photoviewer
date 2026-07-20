# SQLite / GitHub reflection proposal

Date: 2026-07-19 JST

This file is a proposal only. This milestone intentionally did not mutate
SQLite or GitHub and did not push the branch.

## SQLite proposal

Create a new milestone-sized `improvement_items` record rather than rewriting
the earlier local-only record `id = 41` for the shared-history milestone.

Proposed durable facts:

- title: `WPF gallery zoom / geometry anchor semantic adoption`
- status: `branch_ready_pending_main_adoption`
- required baseline: `492988ba2390172c3331b72762f5cecd907a3605`
- implementation commit: `4a22b61`
- observed current main at closeout:
  `4c81cca3efc80363568d0d9af35297ff3285b48c`
- verification: focused PASS, current aggregate 53/53 with reload soak 24/24,
  exact 100,000 / 100 folders PASS, shared Favorite/Seen/Recent/Search History
  PASS
- exclusions: normal launcher/runtime not run, no push/deploy/user-state write
- next gate: semantic main promotion plus normal launcher/runtime acceptance

Do not mark the record `implemented`, `adopted`, or `closed` until the next task
has promoted the implementation to local main and recorded normal-runtime
evidence.

## GitHub proposal

The PM should select the live H000025 WPF issue/milestone from current GitHub
state rather than guessing an issue number from branch history. After local-main
promotion, add one concise evidence comment containing:

- promoted main commit and source implementation commit `4a22b61`;
- the focused, aggregate 53/53, reload soak 24/24, and exact-100k results;
- Search History and other shared-state preservation evidence;
- normal launcher provenance/responding evidence from the promotion task;
- explicit note that GitHub Actions were not the gate.

If promotion is deferred, reflect this as `branch ready / pending main
adoption`; do not present the capability as shipped or deployed. No PR, push,
deployment, or GitHub Actions run is proposed by this closeout itself.
