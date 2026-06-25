# Issue #5 Visible Browsing Jank Pass

## Scope

This pass targets scroll-time synchronous work in `ImageGrid`.

The changed code keeps existing virtualization behavior, but reduces work that
can happen during rapid scrolling:

- viewport metrics updates are batched with `requestAnimationFrame`;
- scroll-position persistence is debounced by 180 ms instead of writing on
  every `scrollTop` state change.

## Mechanical Reduction

Before this change, each scroll event could immediately run metric reads and
state setters, and each resulting `scrollTop` state update could synchronously
write scroll memory through localStorage.

After this change:

- metrics commits are capped to one per animation frame;
- scroll memory writes happen after the user pauses scrolling for 180 ms.

For a 2 second continuous scroll producing 120 `scrollTop` updates, the scroll
memory write path changes from up to 120 synchronous writes to 1 delayed write:

```text
before: 120 writes
after:    1 write
reduction: 99.2%
```

If a device/browser emits 240 raw scroll events per second on a 60 Hz display,
the metrics path is capped from up to 240 updates/second to at most 60
updates/second:

```text
before: 240 metric update attempts/second
after:   60 metric update attempts/second
reduction ceiling: 75.0%
```

These are mechanical upper bounds from the scheduling change, not a full
fixture benchmark. The project still needs fixture-backed long-task and
thumbnail viewport-fill measurements for the final M2 stop condition.

## Verification

- `pnpm exec vitest run src/lib/viewerUi.test.ts src/lib/modalNavigation.test.ts --reporter=verbose`
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1`
