# Repository Publication Runbook

This runbook changes the source repository from private to public while keeping
PhotoViewer's application runtime local-only. Visibility must not change until
the final approval step.

Target rename:

```text
a9ui/tools-h000025-photoviewer
→ a9ui/H000025-PhotoViewer
```

## 1. Freeze and record the candidate

Stop merges and non-publication pushes. Record:

```powershell
gh repo view a9ui/tools-h000025-photoviewer `
  --json nameWithOwner,visibility,defaultBranchRef,isPrivate,viewerCanAdminister,url

git status --short
git rev-parse HEAD
git remote -v
```

The working tree must be clean and `HEAD` must match the reviewed GitHub
candidate.

## 2. Create a private recovery bundle

Write the bundle outside the repository:

```powershell
$AuditRoot = Join-Path $env:TEMP 'photoviewer-publication-audit'
New-Item -ItemType Directory -Force $AuditRoot | Out-Null

git fetch --all --tags --prune
git bundle create (Join-Path $AuditRoot 'before-public.bundle') --all
git bundle verify (Join-Path $AuditRoot 'before-public.bundle')
```

Do not commit or upload the bundle.

## 3. Fetch pull-request refs for history review

```powershell
git fetch origin `
  '+refs/pull/*/head:refs/remotes/origin/pull/*/head' `
  '+refs/pull/*/merge:refs/remotes/origin/pull/*/merge'
```

Inventory refs and commit identities:

```powershell
git for-each-ref --format='%(refname) %(objectname)' `
  | Set-Content -Encoding utf8 (Join-Path $AuditRoot 'refs.txt')

git log --all --format='%H`t%an`t%ae`t%s' `
  | Set-Content -Encoding utf8 (Join-Path $AuditRoot 'commits.tsv')
```

Review every author email in the inventory. A public repository exposes commit
identity metadata even when the current files are clean.

## 4. Scan secrets and private paths

Use current supported scanner versions. Keep raw reports under `$AuditRoot`.
Example commands:

```powershell
gitleaks git . `
  --redact `
  --log-opts='--all' `
  --report-format json `
  --report-path (Join-Path $AuditRoot 'gitleaks-history.json')

$GitUri = 'file:///' + ((Resolve-Path .).Path -replace '\\', '/')
trufflehog git $GitUri `
  --results=verified,unknown `
  --json `
  | Set-Content -Encoding utf8 (Join-Path $AuditRoot 'trufflehog-history.jsonl')

powershell -ExecutionPolicy Bypass `
  -File .\scripts\verify-public-surface.ps1 `
  -FullTree `
  -OutputPath (Join-Path $AuditRoot 'public-surface.json')
```

Also review GitHub Issues, pull requests, reviews, comments, Actions logs,
retained artifacts, releases, screenshots, and attachments. Repository-local
scanners do not cover all of those surfaces.

For every candidate, record disposition without copying a secret value into the
summary. If a credential is real, rotate or revoke it first. Do not rewrite Git
history without a separate owner-approved plan.

If required refs, logs, artifacts, or private GitHub surfaces cannot be audited,
direct publication is not green. Use the clean-history fallback or leave the
repository private.

## 5. Complete the license gate

Read `docs/license-decision.md`. The owner selects a license and commits the
exact root `LICENSE` file. Then inventory third-party licenses and decide whether
`THIRD_PARTY_NOTICES.md` is required.

No root `LICENSE` means publication remains NO-GO.

## 6. Run exact-candidate verification

First make the publication blockers executable:

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\scripts\verify-public-surface.ps1 `
  -FullTree `
  -RequireLicense `
  -OutputPath (Join-Path $AuditRoot 'final-public-surface.json')
```

Then run the product gates:

```powershell
corepack pnpm install --frozen-lockfile
powershell -ExecutionPolicy Bypass -File .\scripts\verify-project.ps1 -Full

dotnet build `
  .\local-native\PhotoViewer.Wpf\PhotoViewer.Wpf.csproj `
  -c Release `
  --nologo

powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-album-store.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-modal-interaction.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-delete-correctness.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-path-robustness.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-decode-bounds.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-wpf-png-metadata.ps1
```

Do not use the normal user-owned port 3000 or rebuild a shared `.next` directory
behind a live production process. Use isolated ports, temporary fixtures, and an
isolated output directory where required.

Wait for the GitHub Actions checks on the exact SHA. Copy the successful check
names into the repository hardening command; do not infer them from YAML alone.
Run the scheduled dependency-audit workflow manually for this candidate and
record the result.

## 7. Rename while still private

Confirm that `a9ui/H000025-PhotoViewer` is available, then run:

```powershell
gh repo rename `
  -R a9ui/tools-h000025-photoviewer `
  H000025-PhotoViewer
```

Do not pass `--yes` until the displayed old and new repository identities have
been reviewed.

Update the local remote:

```powershell
git remote set-url origin https://github.com/a9ui/H000025-PhotoViewer.git
git remote -v
gh repo view a9ui/H000025-PhotoViewer --json nameWithOwner,visibility,url
```

Update maintained references such as `project.toml`, issue-form links, badges,
and scripts. Historical old-name mentions are allowed only in the publication
policy and migration runbook.

Enforce the rename result:

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\scripts\verify-public-surface.ps1 `
  -FullTree `
  -RequireLicense `
  -ExpectedRepository a9ui/H000025-PhotoViewer `
  -OutputPath (Join-Path $AuditRoot 'renamed-public-surface.json')
```

Rerun Step 6 while the repository remains private.

## 8. Produce the final approval packet

The packet must contain:

- canonical repository name,
- visibility, branch, and exact SHA,
- P0-P3 security findings and accepted risks,
- secret and privacy scan summary,
- inaccessible or deferred GitHub surfaces,
- selected license and third-party status,
- exact successful Actions runs and required check names,
- branch protection and Actions permission plan,
- confirmation that the runtime remains loopback-only,
- confirmation that deployment was not performed.

Explicitly state that Git history, Issues, pull requests, comments, Actions logs,
and retained metadata may become publicly visible.

Stop here until the owner explicitly approves this exact candidate.

## 9. Change visibility

Only after approval:

```powershell
gh repo edit a9ui/H000025-PhotoViewer `
  --visibility public `
  --accept-visibility-change-consequences
```

Immediately verify:

```powershell
gh repo view a9ui/H000025-PhotoViewer `
  --json nameWithOwner,visibility,isPrivate,url
```

## 10. Apply public hardening

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\scripts\harden-github-repository.ps1 `
  -Repository a9ui/H000025-PhotoViewer `
  -RequiredCheck 'Browser verification','WPF Release build' `
  -ForkApprovalPolicy first_time_contributors `
  -ArtifactRetentionDays 30 `
  -Apply
```

Use the actual successful check names if they differ.

Verify secret scanning, push protection, vulnerability alerts, private
vulnerability reporting, CodeQL default setup, read-only workflow permissions,
fork-workflow approval, artifact/log retention, and branch protection.

## 11. Anonymous verification

From a clean temporary directory without using the authenticated checkout:

```powershell
$PublicClone = Join-Path $env:TEMP 'H000025-PhotoViewer-public-check'
Remove-Item $PublicClone -Recurse -Force -ErrorAction SilentlyContinue
git clone https://github.com/a9ui/H000025-PhotoViewer.git $PublicClone
```

Confirm README, LICENSE, SECURITY, issue forms, workflows, and the intended
source files are readable. Confirm no scanner report, recovery bundle, personal
image, user state, or local database was published.

Run the final Actions workflow and record the result. Confirm CodeQL completed
for JavaScript/TypeScript, C#, and Actions, or record exact unsupported-language
or plan limitations instead of silently treating configuration as a clean scan.

## 12. Clean-history fallback

Stop direct publication and use a new clean-history repository when the policy
gates require it. Create the public repository from a reviewed export rather
than mirroring all refs. Keep the internal repository private under a distinct
name selected by the owner. Do not create or publish the fallback repository
without explicit approval.
