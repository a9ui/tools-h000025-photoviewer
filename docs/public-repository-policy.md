# Public Repository Operating Policy

Status: proposed policy; repository visibility must remain private until the
final gate in this document is complete.

## 1. Publication model

PhotoViewer uses the following split:

- **Source repository:** intended to become public under the name
  `a9ui/H000025-PhotoViewer`.
- **Application runtime:** local-only and bound to `127.0.0.1`.
- **Deployment:** unsupported and outside the publication milestone.

Public source does not authorize LAN exposure, Internet hosting, Vercel,
Cloudflare Tunnel, reverse proxying, port forwarding, cloud storage, or an
account service. A future remote-access product would require a separate threat
model and authentication/authorization architecture.

## 2. Default decision

The preferred path is to retain the current repository and history, rename it
while still private, and then change visibility only after every gate below is
complete.

Use a new clean-history public repository instead when any of these conditions
is true:

- a credential or other secret exists in history and safe rewrite is not
  approved or cannot be verified,
- personal or internal information is too widely distributed across Git
  history, Issues, pull requests, comments, logs, or artifacts to review
  confidently,
- required private refs, Actions logs, or retained artifacts cannot be audited,
- the owner prefers to keep internal development history private,
- a history rewrite would create disproportionate recovery risk.

A visibility change is never used as an experiment. Making the repository
private again cannot recall data already cloned, cached, indexed, or forked.

## 3. Publication gates

### Gate A — source security

- A repository-wide security review covers Browser, WPF, local APIs, file and
  path handling, image parsing, Enhancement backends, process execution, shared
  state, and GitHub Actions.
- All P0/P1 security findings are fixed and validated on the exact candidate.
- Lower-severity findings are fixed, explicitly accepted with rationale, or
  tracked without overstating readiness.
- `dev`, `start`, and the normal launcher bind explicitly to `127.0.0.1`.
- `/api/:path*` retains the loopback Host/Origin/Fetch-Metadata guard and
  side-effecting routes keep defense in depth.

### Gate B — privacy and secrets

Review all of the following, not only the current working tree:

- every local and remote branch,
- every tag,
- pull-request head and merge refs,
- deleted and unreachable-looking historical files still present in refs,
- commit author names and email addresses,
- Issues, pull requests, reviews, and comments,
- Actions logs and retained artifacts,
- releases, release assets, screenshots, and attachments.

Run at least two complementary secret scanners where possible, including a
full-history scanner such as Gitleaks and a verified-secret scanner such as
TruffleHog. Store raw reports outside the repository and redact any secret value
from summaries.

Classify every candidate as credential, personal information, local path,
internal workflow information, harmless history, or false positive. Rotate or
revoke credentials before removing them from source or history.

A history rewrite requires a separate owner approval that lists affected refs,
force-push scope, collaborator impact, and the recovery bundle.

### Gate C — public-facing repository content

The candidate contains:

- a public-facing `README.md`,
- `SECURITY.md` with private reporting and local-only runtime scope,
- `CONTRIBUTING.md`,
- issue forms and a pull-request template,
- self-contained repository guidance without private workspace dependencies,
- a publication runbook and license decision record.

Public examples and tests use generated temporary fixtures. They do not contain
personal media, real cache/state, private database content, credentials,
unredacted home-directory paths, or screenshots with private metadata.

### Gate D — license and third-party obligations

- The owner selects and commits a root `LICENSE` file.
- The selected license is reflected in the README and package/repository
  metadata.
- Production and development dependency licenses are inventoried.
- `THIRD_PARTY_NOTICES.md` is added when required.
- External binaries, models, ComfyUI, and Real-ESRGAN assets are not claimed as
  repository-owned or relicensed merely because PhotoViewer can call them.

Until this gate is complete, repository publication is NO-GO.

### Gate E — exact-revision CI

The exact publication candidate has successful checks for:

- Browser unit tests, lint, typecheck, and production build,
- the public-surface policy verifier,
- WPF Release build,
- security-focused Browser/WPF tests relevant to the publication changes,
- dependency audit or an explicit, reviewed exception.

The check names required by branch protection are copied from successful GitHub
runs; they are not guessed from workflow YAML.

### Gate F — rename while private

Only after Gates A-E are green:

1. freeze merges and pushes,
2. create a private recovery bundle containing all refs,
3. rename `a9ui/tools-h000025-photoviewer` to
   `a9ui/H000025-PhotoViewer`,
4. update local remotes and repository references,
5. rerun exact-revision verification while visibility is still private.

GitHub redirects many old repository URLs after a rename, but tracked references
must still be updated so the repository does not depend on redirects forever.

### Gate G — final human approval

Immediately before the visibility change, present:

- repository name, visibility, branch, and exact SHA,
- surviving security findings and accepted risks,
- secret/privacy scan result and inaccessible surfaces,
- selected license and third-party status,
- exact successful CI runs and required check names,
- current branch protection and Actions permissions,
- the documented consequences of making history, Issues, PRs, Actions logs, and
  metadata public.

The repository remains private unless the owner explicitly approves this exact
candidate.

## 4. Public GitHub operating controls

After visibility becomes public, apply and verify:

- secret scanning,
- push protection,
- Dependabot vulnerability alerts,
- private vulnerability reporting,
- code scanning where available,
- read-only default `GITHUB_TOKEN` permissions,
- approval requirements for first-time outside contributors where available,
- branch protection requiring pull requests and the exact successful checks,
- administrators included in branch protection,
- force pushes and branch deletion disabled,
- conversation resolution required,
- automatic branch deletion after merge,
- GitHub Actions referenced by immutable full commit SHA.

Do not upload personal logs, caches, screenshots, databases, or scanner output as
Actions artifacts.

## 5. Change management after publication

- Security fixes use private vulnerability reporting and, when needed, a private
  fork or security advisory workflow.
- Public issues must not contain active exploit details before remediation.
- Pull requests from forks receive no repository secrets and run with read-only
  permissions.
- New API routes, parsers, process launchers, file mutations, and shared-state
  writers require focused security tests.
- Any proposal to expose PhotoViewer beyond loopback is a separate milestone and
  is not approved by this policy.

## 6. Decision record

A publication closeout records:

- whether the existing history or a clean public history was selected,
- the exact candidate SHA,
- scanner versions and result summaries,
- privacy redactions and accepted historical exposure,
- license decision,
- CI and GitHub-setting evidence,
- the explicit owner approval,
- confirmation that deployment was not performed.
