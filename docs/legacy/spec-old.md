# Specification (HOW)

## Architecture summary

Photoviewer Upscale starts from the completed Photoviewer codebase, but AI
enhancement is a separate subsystem. The viewer still uses `src/lib/indexer.ts`,
`src/store/ImageContext.tsx`, `src/components/ImageGrid.tsx`,
`src/components/ImageModal.tsx`, and `src/lib/thumbnailCache.ts` for browsing.
Enhancement work must not be inserted into those thumbnail/display/scan paths.

The new subsystem lives under `src/lib/enhance/` and `src/app/api/enhance/`.
The API registers and controls jobs. The actual work runs through an in-process
queue for the first local MVP and delegates image processing to an adapter.
The adapter layer supports the simple local Sharp enhancer, the default
Real-ESRGAN ncnn-vulkan fast GPU adapter, and an advanced ComfyUI HTTP adapter
without changing the UI contract.

## Product namespace

- Project id: `H000024`
- Folder: `H000024_PhotoviewerUpscale`
- Package: `h000024-photoviewer-upscale`
- App title: `Photoviewer Upscale`
- Browser/localStorage prefix: `pvu_`
- Enhancement cache root: `.cache/enhance`

The completed source app `H000003_Photoviewer` stays protected. This project may
reuse code from the copy, but it should not write back to the original folder.

## Enhancement data model

Create `src/lib/enhance/types.ts` with these plain TypeScript concepts:

- `EnhancementJobStatus`: `queued`, `running`, `succeeded`, `failed`,
  `canceled`, or `deleted`.
- `EnhancementJob`: durable record with `id`, `sourceId`, `sourcePath`,
  `sourceSignature`, `presetId`, `presetHash`, `adapterId`, `status`,
  `progress`, optional `outputPath`, optional `errorMessage`, timestamps, and
  `cancelRequested`. Running jobs also store `runId`, `workerInstanceId`,
  `lastHeartbeatAt`, and optional backend identifiers such as
  `externalPromptId` or `externalProcessId`.
- `EnhancementPreset`: stable settings such as label, model family
  (`anime`, `photo`, or `general`), model name, scale, output format,
  denoise, sharpen, and adapter options.
  Detail enhancement also stores `detail` and `smoothness` settings. `detail`
  increases edge/detail sharpening strength so output changes are visible
  beyond pixel dimensions without intentionally changing color tone.
  `smoothness` applies controlled pre-sharpen smoothing so strong detail
  settings do not only produce harsh jagged edges.
  Optional color controls are stored separately as `colorBrightness`,
  `colorContrast`, and `colorSaturation`. They default to `0` so enhancement
  does not change color unless the user explicitly adjusts those controls.
- `EnhancedImage`: a derived output linked to exactly one source image.

For MVP, keep built-in presets for anime/illustration, photo/realistic, and
general high-scale work. Include stronger detail/high-scale variants such as
x4 anime/photo detail and x6 general max presets in addition to the first x2
defaults. The `sharpTestAdapter` uses these as placeholders until a real model
backend exists, but each job must store a full preset snapshot so later preset
edits do not change historical job identity.

## Durable job store

Create `src/lib/enhance/jobStore.ts`.

The first implementation stores JSON at `.cache/enhance/jobs.json` and writes
atomically by writing a temporary file and renaming it into place. It exposes
functions to list, get, create, update, mark cancel requested, and retry jobs.
The store must create `.cache/enhance/` when needed.

SQLite is intentionally deferred. The local MVP is single-user and benefits
more from fewer dependencies while the job and UI contract are still changing.

## Queue and adapters

Create `src/lib/enhance/queue.ts` and `src/lib/enhance/adapters/`.

The queue runs at concurrency 1. It atomically claims queued jobs with a
per-run `runId`, invokes the selected adapter, updates progress, and records
succeeded, failed, or canceled status only when the active `runId` still
matches. Cancel requests are cooperative: the store marks `cancelRequested`,
and the adapter periodically checks it. Backend-specific cancel hooks may also
stop only the exact spawned child process or exact ComfyUI prompt for that job.
Queue startup performs one stale-running recovery pass; `GET /api/enhance/jobs`
must remain read-only.

Create an adapter interface in `src/lib/enhance/adapters/index.ts`:

    export interface EnhancementAdapter {
      id: string;
      label: string;
      run(job: EnhancementJob, context: EnhancementAdapterContext): Promise<EnhancementAdapterResult>;
    }

The first adapter is `sharpTestAdapter`. It reads the source image, writes a
separate output under `.cache/enhance/outputs/`, uses Sharp for a lightweight
resize/detail-enhancement operation, and includes a short delay/progress loop
so UI state can be verified without a real AI backend. The local adapter should
use high-quality Lanczos resizing, denoise/median cleanup, optional smoothing,
and stronger parameterized sharpening. It should avoid deliberate saturation,
hue, or brightness shifts. This is still not a true generative super-resolution
model, but the placeholder should make 4x+ jobs visibly different instead of
only increasing canvas size.

The default real AI adapter is `ncnnVulkanAdapter`. It runs
`C:\AI\RealESRGAN-ncnn-vulkan\realesrgan-ncnn-vulkan.exe` directly without
ComfyUI. Normal UI scales are x1.5, x2, x3, and x4. The adapter chooses a native
ncnn scale of x2, x3, or x4, then uses Sharp only when the requested final scale
differs from the native model scale. Anime presets use `realesr-animevideov3`
by default because local measurements showed it is much faster than the older
`realesrgan-x4plus-anime` model on large generated images. Photo and general
presets use `realesrgan-x4plus` until local benchmark data justifies a more
specific default.

The ncnn adapter normalizes each source image into a per-job temporary
`input.png` before invoking the native executable so EXIF orientation, unusual
source formats, and non-ASCII source paths are handled by the app. It writes
engine output into a per-run temporary directory, materializes final output to a
same-directory `.tmp` path, checks for cancel, then renames it into the managed
output path only after success. Temporary files are removed on cancel or
failure. It records diagnostics including
backend, model name, requested scale, native scale, source megapixels, AI work
megapixels, final output megapixels, warning level, ncnn engine time,
postprocess time, and total time. The API rejects normal ncnn jobs above x4.
It also preflights source dimensions and returns a `409` response for very large
jobs: `UPSCALE_REQUIRES_CONFIRMATION` for slow-but-allowed jobs and
`UPSCALE_TOO_LARGE` for blocked jobs. The UI may retry a confirmation-required
job with `confirmLargeJob: true`. The API checks the ncnn executable and
required model files before creating a job and returns
`BACKEND_NOT_AVAILABLE` if the backend is missing. The adapter validates the
materialized output dimensions before publishing the final managed file.

The ComfyUI adapter calls a local ComfyUI HTTP server. It uploads the source
image to `/upload/image`, patches an API-format workflow from
`PVU_COMFY_WORKFLOW_PATH` or `config/comfy-upscale-workflow.json`, submits it to
`/prompt`, polls `/history/{prompt_id}`, downloads the first output image from
`/view`, and copies that output into `.cache/enhance/outputs/`. The default
endpoint is `http://127.0.0.1:8188` and can be changed with `PVU_COMFY_URL`.
The default workflow uses ComfyUI's built-in `LoadImage`,
`UpscaleModelLoader`, `ImageUpscaleWithModel`, and `SaveImage` nodes. Anime
presets patch the workflow to use `RealESRGAN_x4plus_anime_6B.pth`; photo and
general presets use `RealESRGAN_x4plus.pth`. These model files live under
`C:\AI\ComfyUI\models\upscale_models`. The adapter fails with a clear
configuration error if the workflow JSON or required model files are missing.
Because these are native x4 Real-ESRGAN models, requested scales that differ
from the native model output are normalized by the app after the AI pass.

`start_viewer.bat` uses `scripts/prod_launcher.js`. Normal viewer launch does
not auto-start ComfyUI, because the default backend is the direct Real-ESRGAN
ncnn-vulkan adapter and ComfyUI can interfere with other GPU workloads such as
A1111. Set `PVU_COMFY_AUTOSTART=1` to start a managed local ComfyUI process
from `C:\AI\ComfyUI` on `127.0.0.1:8188` for Advanced ComfyUI Workflow. When
the launcher exits, only the ComfyUI process started by that launcher is stopped
with the viewer. If port `8188` is already occupied, the launcher assumes
ComfyUI is user-managed, reuses it, and does not kill it on exit. Override
`PVU_COMFY_ROOT`, `PVU_COMFY_HOST`, `PVU_COMFY_PORT`, and `PVU_COMFY_URL` for a
different local runtime.

## API routes

Add API routes under `src/app/api/enhance/`:

- `POST /api/enhance/jobs`: validate source id against the current index, create
  a queued job, start the queue, and return the job immediately.
  Server-side validation rejects malformed enhancement settings: `scale` must
  be a finite number from 1 to 8, `denoise` and `sharpen` must be finite
  numbers from 0 to 100, and `outputFormat` must be `png`, `webp`, or `jpg`.
- `GET /api/enhance/jobs`: return recent jobs, optionally filtered by source id.
- `GET /api/enhance/jobs/[id]`: return one job.
- `POST /api/enhance/jobs/[id]/cancel`: mark cancel requested.
- `POST /api/enhance/jobs/[id]/retry`: create or reset a job for retry.
- `DELETE /api/enhance/jobs/[id]/output`: delete only the managed enhanced
  output file for that job, then mark the job as deleted. This route must never
  accept a source path or arbitrary output path from the client.
- `GET /api/enhance/output?jobId=...`: stream only the output belonging to that
  job from the managed `.cache/enhance/outputs` root. Do not accept arbitrary
  absolute output paths.

Each route should use Node runtime. API handlers must not block until an AI job
finishes.

## UI integration

Add minimal UI surfaces:

- In `src/components/RightPreviewPanel.tsx`, add Enhance for the active preview
  and Enhance selected for selected images.
- In `src/components/RightPreviewPanel.tsx`, add compact enhancement settings
  controls for method (`Simple local resize`, `Real-ESRGAN fast GPU`, or
  `ComfyUI AI upscale`), model preset, scale, and output format. Store the
  current UI settings in `localStorage["pvu_enhance_settings"]`. Show denoise,
  sharpen, detail, smoothness, and color controls only for methods that actually
  apply those values; for direct real AI backends, show a hint rather than
  pretending unsupported sliders change the model.
- In `src/components/ImageModal.tsx`, add an Enhance button to the existing
  modal control area. It must not bubble into modal close/navigation handlers.
  The modal enhancement keyboard shortcut is stored in Settings as
  `keyBindings.enhanceImage`, defaults to `a`, and must be merged with defaults
  when old settings files do not contain the key.
  Starting enhancement from the modal must not force `view.enhanceQueueOpen` to
  `true`; the modal AI button/progress indicator is the in-modal status surface.
- In `src/components/ImageModal.tsx`, add a top-right original/enhanced toggle
  that is enabled only after a succeeded output exists for the current source.
  The `E` key performs the same toggle. While the current source has a queued or
  running enhancement job, the modal AI control shows compact progress and does
  not enqueue duplicates. A succeeded job must not disable later enhancement;
  the user can enqueue a new job with different current settings after the
  active job finishes.
- In `src/components/ImageModal.tsx`, treat succeeded jobs for the current
  source as a selectable enhanced-version list rather than a single latest
  output. The version selector should show compact labels and expose detailed
  preset/scale/format/denoise/sharpen information so the user can choose which
  enhanced output to compare against the original.
  If the user starts an enhancement from the modal, automatically switch to that
  new enhanced output when the job succeeds.
  Provide a delete-output-only control for the selected enhanced version. It
  calls the enhancement output delete route and must not call the source image
  delete route.
- Add `src/components/EnhanceQueuePanel.tsx` for job list, status, progress,
  cancel, retry, show source, open output, and delete output.
- Add a lightweight hook or context helper for polling `/api/enhance/jobs` while
  jobs are queued or running.
- Add an `Enhanced only` filter to the sidebar. The filter keeps source image
  identity and shows only source images with at least one succeeded enhanced
  output; enhanced output files are not mixed into the main search index.

The queue UI should make it obvious that enhancement is explicit user work.
Opening an image, moving through modal next/previous, and thumbnail warmup must
never create jobs.

## Output identity and serving

Use deterministic output paths derived from source path, source mtime, source
size, preset hash, adapter id, and the job id. A typical layout is:

    .cache/enhance/outputs/<sourceHash>/<basename>__<presetId>__<presetHash>.png

The source image id remains the normalized absolute source path. Enhanced output
is a variant linked to that id. The MVP does not add outputs to the main
`searchIndex`.

Different enhancement settings produce different `presetHash` values and
therefore different output paths. Re-running the same settings also gets a
job-id-prefixed filename so repeated attempts do not overwrite each other. The
UI should preserve those completed jobs as separate selectable versions for the
same source image.

Deleting a completed enhanced version removes only the derived output file under
the managed `.cache/enhance/outputs` root and marks the job `deleted`. The
source image path is never accepted as input to this delete route. Unfinished
jobs cannot delete outputs, and a deleted output must not be revived by a
late-running queue update.

## Legacy browser state import

Runtime storage in this project uses the `pvu_` prefix. On first run, call
`migrateLegacyPhotoviewerLocalStorage()` before reading persisted viewer state.
The migration copies legacy `pv_` keys into the matching `pvu_` keys only when
the destination key is absent. It must not delete or overwrite legacy keys.
The migration should keep checking for missing keys even after the import marker
exists so later-added keys are not skipped forever.

The first migration set includes:

- `favorites`
- `favorites_backup`
- `view`
- `pinned_tabs`
- `perf_enabled`
- `fav_only`
- `unfav_only`
- `scroll_memory`
- `seen_images`
- `recent_dirs`
- `last_dir_set`

Because localStorage is scoped by browser origin, H000024 cannot read H000003
browser state when the two apps run on different localhost ports. Server-side
fallback therefore reads the protected H000003 `.cache` directory in a
read-only way:

- Merge H000003 `.cache/favorites.json` into H000024 favorites, preserving the
  higher favorite level when both apps know the same image.
- Use H000003 `.cache/settings.json` when H000024 has no settings file yet.
- Let the indexer read matching H000003 `index_*.json` and `folders_*.json`
  cache files when H000024 does not yet have its own cache for that folder.
- Expose `/api/legacy-state` to reconstruct recent scan roots and a last-folder
  candidate from H000003 cache metadata. The client merges these into
  `pvu_recent_dirs` and `pvu_last_dir_set` without deleting old state.

## Image loading robustness

`CachedImage` must compare fallback URLs after resolving them to absolute
browser URLs. Comparing an absolute current image URL with a relative fallback
URL can repeatedly assign the same broken fallback and create noisy error loops.
Fallback state must also be kept in React state, not only by mutating the DOM
image `src` in `onError`; otherwise a later render can put the same broken
thumbnail or display URL back and make an already recovered image disappear.

Thumbnail and display cache files under `.cache/thumbs` and `.cache/display`
must be treated as reusable only when they are non-empty and parse as image
metadata. Validation results should be memoized by path, size, and mtime so warm
cache requests stay fast, while a broken existing cache file is deleted and
regenerated instead of being served forever.
Thumbnail and display WebP generation writes to a unique temporary file first,
then renames it into the final cache path with short Windows rename retries.
Pending generation is checked before cache validation so concurrent requests do
not inspect or remove a file while another request is still creating it.
Client-side blob image cache entries are evicted if the browser fails to decode
the cached blob URL, allowing the next render to refetch instead of reusing a
bad object URL.

The source-image delete API is separate from enhanced-output deletion. Source
deletion is allowed only for images currently present in the active index and is
blocked for files inside the project directory.

The scan SSE route should send lightweight keepalive comments during long
preparation work. In multi-folder scans, one missing or unreadable folder should
be reported as skipped when other folders can still be indexed; if every folder
fails, the route should return an error event.

## Enhancement queue visibility

The viewer view settings include `enhanceQueueOpen`. The header exposes a
compact toggle for showing or hiding the queue, and the queue header has a Hide
action. Hiding the queue affects only visibility; durable job records and output
links remain unchanged.

## Validation strategy

Run from `C:\Users\a9ui\Desktop\Tools\H000024_PhotoviewerUpscale`:

1. `CI=true pnpm install`
2. `pnpm test:unit`
3. `pnpm typecheck`
4. `pnpm lint`
5. `pnpm build`

Manual MVP validation:

1. Launch on a port separate from the old app, for example
   `pnpm exec next start -p 3164`.
2. Scan a small QA folder.
3. Click an image and press Enhance.
4. Confirm the job appears quickly and browsing remains responsive.
5. Confirm `.cache/enhance/jobs.json` and an output file appear.
6. Refresh the browser and confirm completed job state remains visible.
7. Create a job with a non-default preset/scale/format and confirm the durable
   job stores those settings and writes the matching output extension.
8. Open the source image in expanded modal after the job succeeds and confirm
   `OR/UP` plus the `E` key switch original/enhanced display.
9. Confirm no job appears merely from opening a modal or navigating images.
