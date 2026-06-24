# Requirements (WHAT)

## Goal statement

Photoviewer Upscale is a separate new version of the completed Photoviewer. It
keeps the existing local-first large-library browsing experience, then adds
explicit, button-triggered AI image enhancement. The original Photoviewer
project remains protected and unchanged.

The first usable milestone must let the user select or open an image, press an
Enhance button, watch a durable local job progress, and view the generated
output without overwriting or re-indexing the source image.

## Personas and scenarios

- USER who browses large local Stable Diffusion or generated-image libraries.
- USER who wants to upscale or improve only selected images, not every image
  they open.
- USER who can use lightweight local enhancement for quick checks, a fast local
  Real-ESRGAN ncnn-vulkan backend for normal GPU upscale jobs, and ComfyUI only
  for advanced custom workflows.

## Functional requirements

1. Preserve the existing scan/search/favorite/delete/viewer workflows inherited
   from Photoviewer unless explicitly changed for this new version.
2. Keep AI enhancement separate from thumbnail, display-rendition, scan, search,
   and modal-open pipelines.
3. Do not automatically enhance images when they are clicked, previewed, or
   opened in expanded modal view.
4. Provide explicit single-image enhancement actions from the right preview and
   expanded modal.
5. Provide an explicit batch enhancement action for selected loaded images.
6. Let the user choose the enhancement model family/preset. The UI must include
   at least anime/illustration-oriented, photo/realistic-oriented, and general
   presets because the best model differs by image type.
7. Let the user choose the enhancement method and amount, including output
   scale, and expose detailed settings such as denoise, sharpen, detail,
   smoothness, optional color controls, and output format.
   Include higher-detail/high-scale presets beyond the first x2 defaults so the
   user can intentionally request stronger enhancement.
8. Store enhancement jobs in a durable local store so refreshes and Next.js
   restarts do not erase queued, running, succeeded, failed, or canceled job
   records.
9. Keep source images immutable. Enhancement output must be written as a
   separate derived file and must never overwrite the source image.
   Running enhancement backends must write through managed temporary files and
   publish final outputs only after successful completion, so cancel/failure
   cannot leave a half-written enhanced version.
10. Treat enhanced outputs as variants of a source image in the first MVP; do not
   automatically mix outputs into the main search index.
11. Show a queue/status UI with queued, running, succeeded, failed, canceled,
   retry, cancel, open output, delete output, and show source affordances.
12. Show progress while enhancement is processing. At minimum the queue must
    include a progress bar, and the expanded modal should show compact progress
    when the current image has an active job.
13. In expanded modal view, provide a top-right UI control and keyboard shortcut
    to switch between the original source image and the enhanced output when an
    enhanced output exists.
    If multiple enhanced outputs exist for the same source image, let the user
    select the desired enhanced version and show enough settings detail to know
    which preset/scale/format/denoise/sharpen produced it.
    When a modal-started enhancement succeeds, switch the modal to that new
    enhanced output automatically.
    The keyboard shortcut for starting modal enhancement must be configurable
    from Settings, defaulting to `A`.
    Starting enhancement from expanded modal view must not automatically open
    the Enhance queue panel; the modal's compact AI control should show current
    progress instead.
14. Import prior Photoviewer browser history non-destructively on first run,
    including favorites, favorite backup, view settings, pinned tabs, filters,
    seen images, scroll memory, recent folder sets, and last opened folder set.
    When browser localStorage is not shared because the new app is running on a
    different localhost port, inherit available H000003 server-side cache state
    such as favorites, settings, cached indexes, and recent scan roots.
15. Keep a simple local adapter. It may use Sharp and a small artificial delay
    to provide fast resize/detail enhancement and prove job state, progress,
    cancellation, output path, and UI flows.
16. Provide a fast Real-ESRGAN ncnn-vulkan adapter as the default real AI
    upscale backend. It should run locally without ComfyUI, default to WebP
    output, support normal scales up to x4, and show source/work/output
    megapixel diagnostics so large jobs are understandable.
17. Provide a ComfyUI adapter for advanced real AI upscale workflows. ComfyUI is
    optional and should not be auto-started during normal viewer launch unless
    explicitly requested through environment configuration. If the launcher does
    start a managed ComfyUI process, it must stop only that process when the
    viewer closes.
18. Keep AI concurrency at one job at a time for the MVP.
19. Keep output serving path-based access guarded by job/output ids; do not
    expose arbitrary absolute local paths as direct API parameters.
    Job completion, failure, and cancellation must be guarded by the active run
    identity so a stale worker cannot overwrite a newer canceled/deleted state.
20. Keep browser localStorage keys separate from the completed Photoviewer after
    first-run import so both apps can be run on localhost without continuing to
    share favorites, view settings, or recent folder sets.
21. Let the enhancement queue/console be hidden and restored from the viewer UI.
22. Do not treat a succeeded enhancement as a terminal state for the source
    image. The user must be able to run enhancement again with different
    settings and keep the new result as another selectable version.
23. Let the user delete an enhanced output without deleting or moving the
    original source image. This must use an enhancement-output-specific action,
    not the source image delete flow.
24. Provide a filter that shows only source images that have at least one
    succeeded enhanced output.

## Non-functional requirements

1. Browsing responsiveness remains more important than background enhancement
   throughput.
2. AI jobs must be cancelable or at least able to record cancel-requested state
   clearly when the current adapter cannot stop instantly.
3. Job failure messages must be visible enough to diagnose missing backend,
   invalid workflow, file IO, or output-copy problems.
4. The MVP should avoid new native dependencies beyond those already present
   unless they are required for the selected real backend.
5. Source files, prompt metadata, favorites, and delete behavior must remain
   stable during enhancement work.

## Constraints and assumptions

1. This project lives at `C:\Users\a9ui\Desktop\Tools\H000024_PhotoviewerUpscale`.
2. `C:\Users\a9ui\Desktop\Tools\H000003_Photoviewer` is the protected completed
   source project and should not be modified for this new-version work.
3. The first implementation should use a JSON durable store under
   `.cache/enhance/` rather than SQLite.
4. The ComfyUI adapter may require user-provided endpoint and workflow
   configuration, but the job UI and simple local adapter should remain usable
   before that backend is configured.

## Acceptance criteria

1. The copied app can install, test, typecheck, build, and launch independently
   from the original Photoviewer.
2. The page title and visible product name identify the new app as Photoviewer
   Upscale.
3. LocalStorage keys for this project use a `pvu_` prefix after one-time
   non-destructive import from legacy `pv_` keys.
4. Pressing Enhance on a single image creates a durable job record and returns
   immediately.
5. The queue UI shows the job progressing without blocking browsing.
6. The enhancement settings UI lets the user choose anime/photo/general preset,
   scale, denoise, sharpen, and output format before creating a job.
   For real AI backends, controls that the selected backend cannot honor should
   be hidden or clearly labeled so changing them does not imply a false effect.
7. The test adapter writes a separate derived output file under
   `.cache/enhance/outputs/`.
8. The output can be viewed from the job UI without adding it to normal search
   results.
9. Expanded modal view can toggle original/enhanced output via the top-right UI
   and the `E` key after a succeeded output exists.
10. Refreshing the browser after job completion still shows the completed job.
11. Canceling a queued or running test-adapter job updates durable job state.
12. No AI job starts from image open, preview open, modal navigation, thumbnail
    warmup, display rendition generation, or scan refresh.
13. The enhancement queue can be hidden without losing durable job state and can
    be shown again from the viewer header.
14. A source image with a completed enhancement can be enhanced again with a
    different setting set; the completed outputs remain selectable in the
    expanded modal by version.
15. Deleting an enhanced output removes only the derived output file and removes
    that version from selectable enhanced outputs; the original source image
    remains indexed and untouched.
16. Enabling the enhanced-only filter limits the image list to source images
    with succeeded enhanced outputs.
17. Settings can change the expanded-modal AI enhancement shortcut, and legacy
    settings without that key are safely completed with the default binding.
