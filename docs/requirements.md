# Requirements

## Goal

PhotoViewer keeps the existing local image browsing functions and improves the
runtime experience toward extreme lightness.

## Required Capabilities

- Browse large local folders of images.
- Scan folders and show progress without locking the interface.
- Display thumbnails with visible-item priority.
- Open a modal/full image view and navigate quickly.
- Preserve favorites, tags, search, date sections, delete, open, settings, and
  preview workflows.
- Keep optional enhancement jobs explicit and isolated from normal browsing.
- Keep all project work coordinated through GitHub, SQLite, Cursor, and the
  ChatGPT Project workflow.

## Performance Targets

Initial targets are measurement targets rather than promises. M1 must establish
baseline numbers and then convert them into concrete thresholds.

- Launch: time to first usable screen.
- Scan: time to first visible results and total scan completion.
- Thumbnail: time to fill the visible viewport and cache hit behavior.
- Modal: previous/next navigation latency after initial image load.
- API: local route response time for scan, search, image, thumbnail, settings,
  favorites, tags, and delete flows.
- Stability: no accidental enhancement/GPU work during normal browsing.

## Completion Criteria

- GitHub repository, milestone, issues, and CI exist.
- Local verification passes or failures are recorded as GitHub issues.
- A repeatable baseline measurement report exists.
- Each optimization PR includes the before/after check used to validate it.
- ChatGPT Project / PRO review can inspect the milestone pack.
