# PhotoViewer Start Here

Use this repository as the complete project entry point. No private workspace or
machine-specific documentation is required.

1. Read `README.md` and `SECURITY.md`.
2. Read `AGENTS.md`, `PROJECT.md`, and `DESIGN.md`.
3. Read `docs/photoviewer-authoritative-spec.md` for the integrated Browser/WPF
   product contract, implementation boundaries, and WinForms FROZEN policy.
4. Read `docs/current-implementation-truth.md` as a historical implementation
   ledger, then verify claims against current source, tests, and the exact Git
   revision.
5. Use `docs/browser-feature-contract.md` and `docs/wpf-product-spec.md` for
   detailed surface-specific contracts.
6. For repository publication work, read `docs/public-repository-policy.md`,
   `docs/publication-runbook.md`, and `docs/license-decision.md`.
7. Check current GitHub issues, pull requests, and Actions for the exact branch.
8. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1
```

For WPF changes, also build Release and run the focused verifier for the changed
surface. Documentation marked complete or green is not a substitute for current
source and exact-revision test evidence.
