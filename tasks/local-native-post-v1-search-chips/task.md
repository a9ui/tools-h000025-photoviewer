# Local Native Post-v1 #110 Search Chips First Slice

Date: 2026-07-09

## Source Evidence

- GitHub issue #110 requests native search chips and tag-style search UI.
- Browser `SearchBar.tsx` treats comma-separated search terms as committed tags,
  renders chips, supports chip removal, suggestions from `/api/tags`, and
  drag/reorder/keyboard interactions.
- Browser `src/lib/indexer.ts` splits search queries by comma and requires all
  terms to match searchable metadata/text.
- Native #109 already stores prompt metadata in indexed search and adds prompt
  tags to the existing native search field as comma-separated terms.

## Decision

ADOPT for the first bounded slice:

- Keep the native search text field as the canonical query.
- Mirror comma-separated query terms as removable native chips.
- Build a prompt-tag suggestion dropdown from scanned #107 prompt metadata.
- Add the selected suggestion through the existing `AddPromptTagToSearch` path.
- Extend native UI smoke output with `searchChips=true` and
  `searchSuggestion=true`.

DEFER for later #110 or follow-up work:

- Drag/reorder chip behavior.
- Inline token editing and comma/Enter commit behavior inside a custom tag input.
- Rich keyboard dropdown behavior matching the browser search bar.
- Broader advanced search parity beyond prompt-derived suggestions.

REJECT for this slice:

- Browser app changes under `src/**`.
- Script changes under `scripts/**`.
- Automatic enhancement workers or any cache/state deletion.

## Verification Plan

Run:

```powershell
dotnet build .\local-native\PhotoViewer.Native\PhotoViewer.Native.csproj
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -PrepareFixture
powershell -ExecutionPolicy Bypass -File .\scripts\start-local-native.ps1 -HeadlessUiSmoke -Folder .\.cache\native-fixture -Search fixture
git diff --name-only -- src
git diff --name-only -- scripts
git diff --check
corepack pnpm typecheck
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
```
