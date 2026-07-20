# Album v1 semantic recovery / Browser-WPF parity

## Objective

Claude branch に残る Album MVP を一次証拠として監査し、古い viewer、Delete、
ImageContext、Modal、Grid、Sidebar、性能実装を current local `main` へ戻さず、
Browser と WPF が同じ durable store と operation semantics を使う Album v1 を
設計、実装、検証、文書化する。

Durable state は GitHub、Tools SQLite、project-local docs に残す。Agmsg、外部相談、
deployment、WinForms、新 branch、新 worktree、user cache/state の削除は行わない。

## Hard boundaries

- 実装先は既存 local `main`。Claude branch は read-only の一次証拠として扱う。
- Claude commit の丸ごと cherry-pick と `b584744` の採用は禁止。
- Claude worktree の未追跡 `local-native/` は変更、移動、削除しない。
- Browser runtime verifier は port 3001 以降の隔離 port を使う。
- GitHub Actions は非 gate。local verifier と runtime provenance を gate にする。
- PR #322 は GitHub 操作について **MERGE FROZEN**。push、merge、close は行わない。
  これは内容を除外・無視する意味ではない。PR head と tree 同一の `8914935` が
  Album 実装の authoritative baseline であり、#322 の Browser/WPF 更新をすべて含む。
- Album commit 後の local main は PR head より新しいため、PR head が同じ descendant
  へ明示的に更新されるまで merge 候補と報告しない。
- 古い `origin/main` は provenance 比較にだけ使い、実装 baseline には使わない。

## Deliverables

1. Claude commits の file/hunk 単位 ADOPT/PARTIAL/REJECT 監査。
2. versioned shared Album document、revision、shared create-new lock、latest-on-disk
   mutation、atomic publish、malformed/future refusal、unknown-field preservation。
3. full-snapshot PUT を持たない create/rename/delete/add/remove/bulk operation API。
4. current/outside/missing/moved member と source Recycle / membership remove の分離。
5. Browser/WPF の Album library、picker、source navigation、filmstrip、focus、shortcut。
6. focused/full/isolated runtime/100k/cross-runtime concurrency evidence。
7. current truth、authoritative spec、WPF spec、requirements ledger、SQLite の同期。

## Completion boundary

Local green checkpoint は local `main` へ commit してよい。ただし #322 の push、merge、
close はユーザーの明示承認まで禁止する。最終報告は SHA、changed files、tests、
runtime evidence、未完了事項を列挙し、tree/provenance gate 未成立時は「完成」や
「merge可能」と表現しない。
