# Design

## User Experience

PhotoViewer should feel like a fast local desktop tool running in the browser.
The first screen should appear quickly, controls should stay responsive during
scan and thumbnail work, and modal navigation should not feel tied to background
queues.

## Screens

- Folder selection and scan progress
- Main image grid with date/group sections
- Sidebar for folders, filters, tags, favorites, and status
- Right preview / bottom preview surfaces
- Full image modal with keyboard and edge navigation
- Settings and optional enhancement queue panels

## Visual Direction

Use a dense, quiet, work-focused interface. Prefer stable toolbars, clear
status text, small controls, and predictable spacing over marketing-style
presentation. Avoid adding visual decoration that makes rendering heavier or
slows scanning and browsing workflows.

## Interaction Rules

- Opening, expanding, or previewing an image must not trigger heavy enhancement
  work.
- Visible thumbnails are prioritized over offscreen work.
- Controls remain usable while scan and cache work runs.
- Background work reports progress and should be cancellable or bounded where
  practical.
- Keyboard and pointer navigation in the modal should remain consistent with
  the current app behavior.

## Responsive

The main target is a Windows desktop viewport. Smaller widths should preserve
core browsing and modal use without overlapping text or controls, but mobile is
not the primary optimization target.

## Performance Design Principles

- Render only what the user can inspect or soon inspect.
- Cache local work, but keep cache invalidation predictable.
- Prefer explicit user action over automatic heavy work.
- Measure before changing, then keep improvements backed by repeatable numbers.
