# ADR 0001 — Repository layout and merge workflow

**Status:** Proposed, pending peer review and merge

**Owner:** M1 / Le Doan Gia Hung

**Date:** 2026-10-09

## Context

Five contributors own distinct vertical slices of one AI Exam Bank product. The team wants a protected integration branch for joint testing before each promotion to `main`. The first executable application baseline must give the modules one predictable home without selecting unreviewed database, frontend or IaC technology.

## Decision

- Use a monorepo with one .NET modular monolith. `src/Api` is the HTTP host; future module code belongs under `src/Modules/<Module>`; the Worker and Web applications are added when their runtime contracts and technology choices are reviewed. `infrastructure` holds IaC and environment configuration once the M2 decision is accepted.
- Target .NET 10 LTS for the initial API baseline and pin the SDK feature band in `global.json`. This decision is revisited if the team has a deployment/runtime constraint.
- Use short task branches from lowercase `dev`, then PR into `dev`. Require up-to-date `Repository quality`, `Application build and smoke` and `CodeRabbit` checks plus resolved review threads; no approving review is required on `dev`. The author verifies CodeRabbit actually reviewed the latest commit, addresses findings and manually creates a merge commit; auto-merge remains disabled. Encourage a peer review for high-risk changes.
- Promote a tested `dev` state to `main` with a second PR whose head is the protected `dev` branch. The promotion workflow checks that the PR's declared tested `dev` SHA equals the current PR head SHA and that Application CI passed on that exact `dev` push commit; a human reviewer verifies the staging evidence itself. Require another independent review and CI before manually creating a merge commit. Preserve ancestry between the two long-lived branches. `main` is the source of tagged releases; the AWS development environment is separate from the Git branch.
- Protect both branches against direct pushes, force pushes and deletion, including for administrators. The earlier uppercase `Dev` branch had no unique commits and was removed; lowercase `dev` was created from the current `main` tip for this workflow.
- CODEOWNERS maps modules to a primary and a peer. Required code-owner review stays off until the paths and reviewer coverage are proven on actual PRs.

## Alternatives considered

- Direct feature PRs into `main`: less coordination, but the team wants a separate tested integration state before release.
- Squash-only merges between long-lived `dev` and `main`: copies changes without preserving ancestry, which makes repeated promotions harder to review. Merge commits keep the branch history connected.
- Separate repositories or services per member: adds deployment and contract overhead before independent scaling is needed.
- Create all target projects and infrastructure now: would claim technology decisions and working artifacts that do not exist.

## Consequences and validation

PRs stay small and integrate into `dev` frequently. A promotion PR may collect multiple reviewed changes, so the team must test the exact `dev` commit being promoted and record the result. Unfinished behavior must stay unexposed or be guarded. CI must build and smoke-test every executable host as it is added. A release must be traceable to a tested commit and deployment evidence. The first PR validates the pinned SDK, API build and HTTP liveness probe; product, database, AWS and frontend readiness require later PRs. The main-source check becomes required after this workflow has landed on `main` and passed a promotion PR.

Related: [architecture](../ARCHITECTURE.md), [team](../TEAM.md), [contribution guide](../../CONTRIBUTING.md), [Project](https://github.com/users/Hungle2910/projects/4).
