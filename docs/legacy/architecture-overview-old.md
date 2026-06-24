# Architecture Overview

This repository is USER's personal AI-coder template. USER supplies intent and review, while AI coders execute the work and maintain the documentation. Read this file before touching code to understand how every guiding document fits together.

## Document Roles

1. `docs/requirements.md` — Business WHAT. Record high-level goals, target users, user stories, constraints, KPIs, and acceptance criteria exactly as USER describes them.
2. `docs/spec.md` — Engineering HOW. Translate every requirement into architecture, components, data models, external interfaces, validation strategy, and open questions.
3. `AGENTS.md` — Agent rulebook. Explains how AI coders behave inside this repo (tooling, commands, safety boundaries, expectations for autonomy).
4. `.agent/PLANS.md` — ExecPlan manual. Defines the structure, tone, and maintenance rules for `.agent/execplans/*.md` documents.
5. `docs/first_prompt.md` — Kickoff script. Text USER references when instructing a new agent; it enforces the reading order and ExecPlan ritual.

## Recommended Workflow

1. USER writes or amends `docs/requirements.md` and `docs/spec.md`. One file may be blank early on; when that happens, the agent copies USER's intent from the populated file into the other so both stay in sync.
2. AI coders read this overview and `AGENTS.md` to absorb expectations.
3. Follow `docs/first_prompt.md`, which forces this order: requirements → spec → `.agent/PLANS.md` → new ExecPlan under `.agent/execplans/`.
4. Execute the ExecPlan milestone by milestone. Update requirements/spec and the ExecPlan whenever USER shifts priorities.

## Versioning Requirements & Specs

* Both files are living documents; update them immediately after USER clarifies scope.
* For major changes (agent judgment or explicit USER instruction), archive the previous state before editing:
  1. Generate a timestamp in Japan time (UTC+9) using `YYYYMMDDHHMM`.
  2. Create `docs/versions/<timestamp>/` (make `docs/versions/` if needed).
  3. Copy the current `requirements.md` and `spec.md` into that folder.
  4. Continue editing the originals in `docs/`.
* Minor cleanup (typos, formatting) does not require an archive.

## Responsibilities

* **USER** defines intent, priorities, and acceptance. USER does not write code.
* **AI coders** turn the documents into ExecPlans, code changes, tests, and documentation updates. All assumptions must be recorded inside the relevant ExecPlan.

## Shared Skill Layer

This template also includes a shared-skill layer under `docs/skills/`.
These files are reusable implementation playbooks distilled from completed
tasks. Any AI coder should read `docs/skills/README.md` and relevant
`*.SKILL.md` files before starting complex work.

When a project yields reusable lessons, coders should update or add a skill
file so the next AI (regardless of provider) can apply the same proven method
without rediscovering it from scratch.

Keep this overview synchronized whenever the document flow changes so that both USER and future AI coders can on-board rapidly.
