# Local Native Post-v1 Issue Queue Triage

Date: 2026-07-08

Goal: `Local Native Post-v1 Issue Queue Triage And Parallel Work Selection`

## Source Packet Read

- `AGENTS.md`
- `PROJECT.md`
- `DESIGN.md`
- `project.toml`
- `START_HERE.md`
- `docs/operations-log.md`
- `local-native/README.md`
- `docs/local-native/native-intent-source.md`
- `docs/local-native/m20-verification.md`
- `docs/local-native/m19-verification.md`
- `docs/local-native/api-error-parity-matrix.md`
- `docs/local-native/malformed-import-recovery.md`
- `docs/local-native/state-migration-map.md`
- `docs/local-native/pvu-state-persistence-migration.md`
- `tasks/local-native-post-v1-malformed-import-recovery/task.md`
- `tasks/local-native-post-v1-api-error-parity/task.md`
- `tasks/local-native-post-v1-pvu-state-persistence/task.md`
- `tasks/local-native-m20/task.md`
- `tasks/local-native-m5/browser-regression-matrix.md`

The local worktree initially pointed at detached `c63df41220087cb323faf81f0c18bf187a4b68b3`,
which did not contain the local-native packet. The packet was read from
`origin/main` and the worktree was then switched to
`codex/h25-post-v1-queue-triage` at
`7d9b75f3cd7e0d4c60d6e33ecdd8d54204f6672f`.

## Live State

- GitHub milestone #26 `Local Native Post-v1 Backlog`: open, 20 open / 2
  closed.
- Closed in milestone #26: #115, #116.
- Open in milestone #26: #97-#114, #117, #118.
- Open PRs: none.
- Latest main CI: run #28915773995 passed for `main` at
  `7d9b75f3cd7e0d4c60d6e33ecdd8d54204f6672f`.
- SQLite: job #236 is `Post-v1 #117 continuation`, status `dispatched`.
- Agmsg: trace `h25-117-closeout-20260708` has three `required_reply=none`
  pointers to `cursor_impl`, `claude_ui`, and `grok_consult`.
- Linear: not used.

## Queue Classification

| Issue | State | Parallelization | Decision |
| --- | --- | --- | --- |
| #97 Native enhancement queue management | Open | Serialized | High-risk explicit enhancement UI. Must not run in parallel with #98. Needs explicit-action-only queue smoke and zero passive enqueue proof. |
| #98 Original/enhanced image toggle | Open | Serialized after #97 | Depends on output discovery/management policy from #97. Do not start first. |
| #99 Preview tabs and pinned previews | Open | Serialized design base | Base issue for preview-tab model. #100 depends on it; #101 should not change preview interaction at the same time. |
| #100 Restore recently closed preview tabs | Open | Serialized after #99 | Depends on tab lifecycle semantics from #99. |
| #101 Hover quick preview | Open | Defer until #99 decision | Touches preview behavior and can conflict with tab/pin interactions. |
| #102 Folder bucket range selection | Open | Blocked / needs product UI decision | M12 showed `CheckedListBox` cannot support multi-extended range selection. Needs replacement/custom control semantics before implementation. |
| #103 Bulk favorite actions | Open | Candidate after #117 active row clears | Non-destructive and has existing multi-selection evidence, but still touches `MainForm`/favorite state. Keep behind active #117 unless no competing local-native branch is active. |
| #104 Bulk open actions | Open | Defer / needs behavior decision | Needs policy for mixed folders and large selections. Can follow #103 or a short product decision. |
| #105 Bulk recycle/delete actions | Open | Serialized after #106 | Destructive flow. Requires disposable-copy verification and no hard-delete fallback. |
| #106 Delete confirmation and do-not-ask settings | Open | Serialized before #105 | Should define confirmation/cancel/do-not-ask behavior before bulk delete. |
| #107 Prompt and negative prompt metadata display | Open | Serialized metadata base | Base issue for metadata extraction/display. #108 and #109 depend on the metadata model. |
| #108 Copy PNG info and prompt metadata | Open | Serialized after #107 | Clipboard/copy actions should share #107 fixture and parser evidence. |
| #109 Prompt tag actions | Open | Deferred after #107/#110 decision | Needs tag semantics and search interaction policy. |
| #110 Search chips and tag-style search UI | Open | Deferred / product UI decision | Existing search works. Chip/tag UI should wait for tag semantics or a small UI spec. |
| #111 Compact and poster display modes | Open | Serialized display base | Base issue for display variants. #112/#113 should follow or explicitly split from it. |
| #112 Aspect ratio display controls | Open | Serialized after #111 | Depends on display-mode surface and fixture expectations. |
| #113 Gallery wheel and keyboard zoom | Open | Serialized after #111/#112 | Interacts with thumbnail size/aspect/display controls. |
| #114 Editable keybinding recorder | Open | Deferred late | Should wait until new shortcuts from display/zoom/preview decisions settle. |
| #115 Malformed import recovery UI | Closed | Complete | PR #121 merged; no further parallel work needed unless regression appears. |
| #116 Native browser API and error parity matrix | Closed | Complete | PR #120 merged; matrix-only result adopted; no HTTP compatibility layer. |
| #117 Complete pvu state persistence migration | Open | Reserved / serialized | Parent thread owns continuation via SQLite job #236. Do not compete until parent explicitly hands it back. |
| #118 Native UI polish and screenshot sweep | Open | Safe prep candidate now | Docs/review/screenshot-prep does not compete with #117 implementation. Actual UI fixes should be adopted individually after native evidence. |

## Recommended Parallel Work

Start only #118 preparation from this triage lane. It is the safest
non-conflicting row because it can begin as a Human Surface evidence packet:
desktop screenshot plan, overlap/text/focus checklist, and native smoke
commands. It should not modify `src/**`, start workers, deploy, or touch
H000033.

Do not start #97/#98, #102, #105/#106, or #117 from this lane. Those are either
high-risk, product/UI blocked, destructive, or already owned by another active
thread.

## Advice Classification

- `ADOPT`: Treat #117 as reserved for the parent continuation job #236.
- `ADOPT`: Dispatch #118 as the next independent review/prep lane.
- `PARTIAL_ADOPT`: #103 is a good future implementation candidate, but only
  after the active #117 branch is no longer competing.
- `REJECT`: Broad enhancement queue work, destructive flows, or `src/**`
  browser changes as parallel work from this triage lane.
- `DEFER`: #97/#98, #99-#114 implementation until their dependency decisions
  are made and the active #117 row is not competing.
- `NEEDS_HUMAN`: #102 folder range control semantics; #104 mixed-folder bulk
  open behavior; destructive-flow policy if #106 expands beyond confirmation
  UX.

## Verification For This Report

This triage made no native implementation claim. Required verification is
therefore docs/report hygiene only:

```powershell
git diff --name-only -- src
git diff --check
```
