# License Decision for Public Source

Status: owner decision required before repository visibility can become public.

This document is a decision aid, not legal advice. The selected license must be
reviewed against the intended reuse model and third-party obligations before a
root `LICENSE` file is committed.

## Current state

There is no root `LICENSE` file. Until one is deliberately added, making the
repository public allows people to read the source but does not grant a general
permission to copy, modify, redistribute, sublicense, or sell it.

`package.json` remains `"private": true` to prevent accidental npm publication.
That flag is separate from the GitHub repository's visibility and should remain
true unless package distribution becomes an explicit milestone.

## Common choices

| Option | Reuse and redistribution | Patent terms | Copyleft | Typical fit |
| --- | --- | --- | --- | --- |
| MIT | Broad permission with copyright/license notice | No express patent grant | None | Small tools where maximum reuse and simple compliance are desired |
| Apache-2.0 | Broad permission with notices | Express patent license and termination terms | None | Public projects that want permissive reuse plus clearer patent language |
| GPL-3.0 | Redistribution and modification allowed under GPL terms | Includes patent-related terms | Strong | Projects that want distributed derivatives to remain under the GPL |
| AGPL-3.0 | GPL-style terms plus network-use source obligations | Includes patent-related terms | Strong, including network use | Network services; usually a poor conceptual fit for PhotoViewer's unsupported remote deployment model unless chosen deliberately |
| Source-available custom terms | Depends entirely on the text | Depends on the text | Custom | Projects that want public reading but restrict commercial use, redistribution, or hosting; requires careful legal drafting |
| No license / all rights reserved | No general reuse permission | None granted | Not applicable | Public review only; limits outside contribution and reuse |

## Product-specific questions

The owner should answer:

1. Should third parties be allowed to redistribute modified PhotoViewer builds?
2. Is commercial use allowed?
3. Should downstream applications be required to publish their source?
4. Is an express patent grant important?
5. Will outside contributions be accepted, and under what inbound license?
6. Should anyone be allowed to host or expose a modified PhotoViewer service,
   even though upstream does not support Internet deployment?
7. Is compatibility with a future package or binary distribution important?

## Third-party inventory

Before choosing and publishing a license:

- inventory production and development npm dependency licenses,
- inventory any NuGet or SDK-distributed dependencies used by WPF,
- review icons, fonts, screenshots, fixtures, and copied reference material,
- separate repository code from optional external ComfyUI, Real-ESRGAN,
  ncnn-vulkan binaries, and model files,
- determine whether `THIRD_PARTY_NOTICES.md` is required,
- remove or replace any asset whose provenance or redistribution permission is
  unclear.

A dependency's license does not automatically become PhotoViewer's license, and
PhotoViewer's license must not claim ownership of external binaries or models.

## Decision record

Complete this section in the same pull request that adds `LICENSE`:

```text
Selected license:
Reason:
Commercial use:
Modification and redistribution:
Copyleft expectation:
Patent position:
Inbound contribution policy:
Third-party notice result:
Reviewed by:
Date:
```

Repository visibility remains private until the decision, root license file,
README update, and third-party review are complete.
