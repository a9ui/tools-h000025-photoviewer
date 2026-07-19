# Fable review prompt — H000025 PhotoViewer current Browser/WPF recovery

あなたはH000025 PhotoViewerの独立した製品・UI・実装監査役です。今回はread-only reviewだけを行ってください。コード編集、commit、push、PR merge、Issue更新、deployment、実ユーザーstate/cacheの変更は禁止です。一般論ではなく、実ファイル・実装・テスト・実測値に基づいて判定してください。

## 最重要の製品意図

- 非公開、Windowsローカル専用の大量画像PhotoViewer。
- Browser版を現時点の機能・意味・安全境界の正本とし、WPF版はその再現に加えてBrowserの性能限界を超える。
- 10万枚規模でも全件へ到達でき、silent truncateせず、初回表示・scroll・thumbnail・Modal・Favorite操作が軽いことを最優先する。
- FavoriteはLv0〜5 exact。Favorites ONでLv1〜5独立ON/OFF、level未選択はAll。Unseen dotsは表示だけの独立設定。
- Delete成功後は隣の1枚だけへ移動し、二重移動しない。
- 通常閲覧・preview・modal navigationはAI enhancementをenqueue/startしない。
- 既存Favorite/Seen/Recent/settings/cacheを破壊、全消去、勝手なmigration上書きしない。

## 読む順序

Active recovery worktree:

`C:\Users\a9ui\.codex\worktrees\browser-runtime-parity-0719\H000025_PhotoViewer`

必ず次を先に読んでください。

1. `AGENTS.md`
2. `docs/browser-feature-contract.md`
3. `docs/wpf-product-spec.md`
4. `docs/product-quality-review-framework.md`
5. `tasks/browser-runtime-parity-20260719/task.md`
6. `tasks/browser-runtime-parity-20260719/plan.md`
7. `tasks/browser-runtime-parity-20260719/github-issue.md`
8. current `git diff` and `git status`

重点実装:

- `src/components/ImageGrid.tsx`
- `src/components/ImageGrid.test.tsx`
- `src/store/ImageContext.tsx`
- `src/store/ImageContext.test.tsx`
- `src/lib/thumbnailWarmupBatcher.ts`
- `src/lib/sharedProjectRoot.ts`
- `src/app/api/favorites/route.ts`
- `e2e/viewer-grid-zoom.spec.ts`
- `local-native/PhotoViewer.Wpf/MainWindow.xaml`
- `local-native/PhotoViewer.Wpf/MainWindow.xaml.cs`
- `local-native/PhotoViewer.Wpf/App.xaml.cs`
- WPF key-binding settings/model/persistence/test files added by the current diff, if any
- WPF performance code touched by the current diff, if any

## Version/provenance facts

- GitHub `origin/main` is `626b7dd` (PR #315 era).
- Normal local `main` is `3efea54`, exactly 153 commits ahead of `origin/main` after a fresh fetch.
- Existing local lineage was pushed without changing remote main as draft PR #319:
  `https://github.com/a9ui/tools-h000025-photoviewer/pull/319`
- Current recovery is tracked in Issue #320:
  `https://github.com/a9ui/tools-h000025-photoviewer/issues/320`
- Current recovery branch is `codex/browser-runtime-parity-20260719`; it is intentionally uncommitted while tests and performance work continue.
- Port 3000 currently serves the normal root at source revision `3efea54`, but it is the pre-recovery build. Do not treat the active diff as already adopted on port 3000.
- The WPF process observed during diagnosis was launched from `worktrees/wpf-ultimate-0718`. Its DLL build time was 2026-07-18 13:02 JST, while commit `3efea54` was created at 13:18 JST. Therefore the user-visible old WPF behavior was real stale-binary adoption, not proof that the current source lacks every feature.
- There are many local branches, but branch existence does not compose a runtime. Only the checked-out tree used for build is relevant.

## Current recovery changes already implemented and locally green

### Browser Grid zoom

- While Grid is active, Ctrl/Cmd+wheel and Ctrl/Cmd `+` / `-` / `0` change thumbnail size rather than Browser page zoom.
- Sidebar/header/text/right-panel scale remains fixed.
- A visible last-selected image is the first zoom anchor; otherwise pointer card, then viewport center.
- Slider, wheel, and keyboard use the same anchor correction.
- Plain/Alt wheel remains native scroll; text editing, Modal, Settings, Confirm do not capture gallery keyboard zoom.

Evidence:

- focused unit suite: 52/52 PASS at the last integrated run.
- Playwright TEMP 96-image isolated production test: full e2e 4/4 PASS.
- New zoom test repeated 3 times: 3/3 PASS.
- selected card viewport drift after Ctrl+=: <= 1px.
- Sidebar rect, font-size, innerWidth, devicePixelRatio: unchanged.
- integrated Browser suite after Favorite sparse paging and exact sharing: 55 files passed, 2 skipped; 459 tests passed, 2 skipped.
- TypeScript typecheck, ESLint, and optimized Next.js production build: PASS.
- isolated Playwright on port 43132: 4/4 PASS; normal user port 3000 was not reused.
- retired Quick Search/date-preset/Favorite-threshold/sidebar-width regression guard: 18 files PASS.

### Browser thumbnail priority

- Visible warmup is flushed immediately instead of waiting the fixed 40ms batch delay.
- Visible/focused resend window is bounded at 900ms.
- Nearby/background dedupe remains 3500ms.
- Visible paths preempt nearby paths.
- The old Blob/object-URL thumbnail cache proposal was intentionally not adopted because it can retain thousands of blobs and raise 100k-scale JS memory.
- Warm cached thumbnail API observed around 9–14ms after first request; current server queue was idle at concurrency 12.

### Browser/WPF Favorite exact sharing

- WPF and Browser default to the normal checkout `.cache/favorites.json`, including Browser launched from a linked worktree via `.git/commondir` resolution.
- Shared JSON becomes the exact Browser/WPF source after one-time non-destructive import of Browser local-only legacy keys.
- Pending dirty/journal paths remain local exact overlays and use the existing locked three-way PUT.
- Browser focus/visible recovery refreshes shared Favorites without constant polling.
- WPF Lv decrease/clear must no longer be hidden by stale higher Browser localStorage values.
- WPF Modal +/- buttons now have names and the smoke fires the real WPF `Button.ClickEvent`, rather than directly calling the business method.

Evidence:

- ImageContext: 74/74 PASS.
- shared-root helper + Favorite route: 12/12 PASS.
- three-way/journal focused safety: 6/6 PASS.
- WPF real modal ClickEvent smoke: final UI level 4, disk level 4, separate-window reload level 4.
- Browser/WPF TEMP cross-runtime test: 10 iterations, Favorite 20 + Seen 20, valid JSON, no lock/tmp residue, real user cache untouched.

Important review risk: one-time local-only import cannot perfectly distinguish a true old Browser-only key from a WPF-cleared key that is absent from shared state before the marker exists. Assess whether the current rule is the safest non-destructive migration and identify a better deterministic rule only if it preserves real local-only history.

### Favorite-only sparse result completion

- Favorite / Unrated / Enhanced filters retain a bounded match-demand target after the user reaches the result tail.
- If an appended sparse batch moves the old scroll position away from the new tail, loading continues until the buffered match demand is satisfied or the source catalog ends.
- The demand resets on filter/query-equivalent context or catalog index-token changes, and clears when the filter is disabled.
- Normal unfiltered browsing still does not force all 100k metadata into memory.

Evidence:

- sparse paging fixture suite: 21/21 PASS.
- first page with zero Favorite, Unrated, or Enhanced matches: covered.
- sparse continuation after the old scroll position is no longer near the extended tail: covered.
- final match at source index 599: reached.
- stale filter/index context reset and filter-off cancellation: covered.
- Modal/Delete/navigation regression suite: 24/24 PASS.
- focused ESLint and TypeScript typecheck: PASS.

## Work still in progress — do not report as complete

### WPF lightweight behavior

Fresh recovery-worktree Release, TEMP 100,000 images / 100 folders baseline:

- catalog/filtered: 100,000 / 100,000; silent truncate 0;
- Grid realized 15; List realized 9;
- tail 99,999 Grid/List/Modal: PASS;
- catalog ready: 5,209ms;
- full background metadata completion: 37,192ms;
- scan: 2,127ms;
- materialize: 1,910ms;
- catalog prepare: 536ms;
- other publish: 1,166ms;
- tail visible thumbnail: 166ms;
- working set: 146,616,320 -> 326,606,848 bytes;
- external maximum unresponsive streak: 390ms (gate 750ms);
- zoom: 49–69ms, drift 0;
- total verifier process including fixture/cleanup: 84,920ms.

Two same-condition after runs are now available in `tasks/browser-runtime-parity-20260719/wpf-performance-results.md`:

- catalog ready: 3,762 ms and 4,040 ms (22.4–27.8% faster than baseline);
- full background completion: 30,756 ms and 29,196 ms (17.3–21.5% faster);
- background metadata: 26,617 ms and 24,697 ms (15.6–21.7% faster);
- maximum dispatcher gap: 425 ms and 470 ms (5.2–14.3% lower);
- working set after: 319,459,328 and 319,303,680 bytes (about 7.2 MB lower);
- tail thumbnail: 169 ms and 161 ms versus 166 ms baseline; no material regression;
- exact 100,000 results, Grid/List realization, tail 99,999 access, no Enhancement enqueue, and cleanup all remained PASS in both runs.

This is replicated improvement but is not automatically “lightweight” or commercially complete. Judge user-facing cost and remaining architectural limits, not just PASS.

### WPF configurable key settings

The user has confirmed that the WPF app has no discoverable key-settings surface. A bounded implementation is now in progress. Audit only what is actually present in the final diff.

Required behavior:

- show the current bindings for implemented operations rather than advertising non-functional commands;
- cover the core implemented viewer operations, including previous/next, close/back to list, Favorite levels, delete, selection commands, and zoom where those operations already exist;
- allow edit, save, restart/reload persistence, and reset to defaults;
- reject or clearly resolve duplicate/invalid bindings before save;
- preserve the existing default shortcuts so current muscle memory does not regress;
- route the real WPF input path through saved settings, proven by an automated smoke or focused test;
- do not delete or overwrite unrelated Favorite/Seen/Recent/cache state.

## Questions Fable must answer

1. Is the Browser Grid zoom ownership and selected-image anchor behavior aligned with the product intent? Identify any input/focus/mobile edge cases with file/line evidence.
2. Does immediate visible thumbnail flush improve first paint without introducing duplicate I/O, starvation, or unbounded memory? Identify concrete queue risks.
3. Is the exact Favorite migration/refresh model race-safe for Browser/WPF concurrent writes, decrease, clear, crash journal, malformed state, and focus refresh? Separate blockers from acceptable migration tradeoffs.
4. Does the Favorite-only completion design reach every match without reintroducing the original heavy Browser behavior? Provide a better algorithm only if it is implementable and safer.
5. From the WPF baseline and final before/after evidence, what still prevents the WPF app from feeling lightweight at 10万枚? Rank the top three costs by user impact.
6. Does the WPF key-settings implementation expose only real commands, preserve defaults, reject conflicts, persist safely, and drive the real KeyDown/command path? Identify missing discoverability, accessibility, or recovery behavior.
7. Is version control/runtime adoption now auditable with draft baseline PR #319, focused Issue #320, stacked recovery PR, Browser `/api/runtime`, and WPF launcher provenance? What exact remaining guard would prevent another stale worktree executable from misleading the user?
8. Which required tests are still missing? Point to a precise scenario, expected result, and likely test file.
9. Can this recovery be called ready for normal launcher adoption? Answer `YES`, `NO`, or `YES WITH NON-BLOCKING FOLLOWUPS` and justify.

## Required output format

Use this exact structure:

1. **Verdict** — one of `YES`, `NO`, `YES WITH NON-BLOCKING FOLLOWUPS`.
2. **Confirmed current state** — Browser, Favorite sharing, Favorite-only completion, WPF Modal Favorite, WPF performance, WPF key settings, version management. Mark each `confirmed`, `in progress`, or `unproven`.
3. **Blocking findings (P0/P1)** — each finding must include file + line/range, reproduction or reasoning chain, user impact, and smallest safe fix.
4. **Non-blocking improvements (P2)** — prioritized, with measurable acceptance criteria.
5. **Performance judgment** — compare the supplied WPF baseline to final before/after if available; do not invent numbers.
6. **Test gaps** — exact scenario and where to add it.
7. **Adoption checklist** — what must be green before normal `start_viewer.bat` and `start_wpf.bat` replace the stale processes.

Do not praise the amount of work. Look for regressions, false confidence, hidden caps, race conditions, unbounded memory/I/O, stale runtime adoption, and test gaps.
