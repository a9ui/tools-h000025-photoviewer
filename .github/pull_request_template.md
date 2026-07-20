## Summary

Describe the user-visible result and why this is the smallest safe change on current `main`.

## Affected surfaces

- [ ] Browser
- [ ] WPF
- [ ] Browser and WPF shared state or semantics
- [ ] Repository, CI, documentation, or contributor tooling
- [ ] WinForms critical break/fix only

## Safety and ownership

- [ ] Source images are not rewritten by viewing, indexing, or Enhancement.
- [ ] No hard-delete fallback was added; source deletion remains Windows Recycle Bin only.
- [ ] Ordinary browsing, search, preview, modal navigation, and thumbnail decode still create zero Enhancement jobs and workers.
- [ ] Browser/WPF shared-state ownership, latest-on-disk merge, malformed/newer-version protection, and atomic publication were preserved where applicable.
- [ ] User cache or state is not deleted as a repair or migration strategy.
- [ ] Local APIs remain bound to `127.0.0.1` and retain the Host/Origin/session/path guards where applicable.
- [ ] Process execution uses validated argument arrays and does not introduce shell interpolation.
- [ ] WinForms remains frozen unless this PR is a documented critical break/fix.

## Privacy and security

- [ ] Fixtures are generated and disposable; no personal images or real user state are included.
- [ ] The PR contains no credentials, private scanner output, database files, or unredacted home-directory paths.
- [ ] Security-sensitive changes identify the untrusted input, closest control, sink/invariant, impact, and regression test.
- [ ] Suspected undisclosed vulnerabilities were handled through private vulnerability reporting rather than a public issue.

## Verification performed

List the exact commands and results. Do not write only “all green.”

```text
<commands and concise results>
```

## Browser/WPF parity

Explain whether both surfaces must produce the same result, why any native presentation differs, and which focused tests prove the contract.

## Known limitations or checks not run

State them explicitly, including unavailable services, skipped stress gates, network-dependent audits, or runtime checks.
