# ADR 0001 — Repository layout and merge workflow

**Status:** Proposed, pending peer review and merge

**Owner:** M1 / Le Doan Gia Hung

**Date:** 2026-10-09

## Context

Five contributors own distinct vertical slices of one AI Exam Bank product. The repository has a protected `main`, two open documentation PRs into `main`, and an M5 documentation branch. The first executable application baseline must give the modules one predictable home without selecting unreviewed database, frontend or IaC technology.

## Decision

- Use a monorepo with one .NET modular monolith. `src/Api` is the HTTP host; future module code belongs under `src/Modules/<Module>`; the Worker and Web applications are added when their runtime contracts and technology choices are reviewed. `infrastructure` holds IaC and environment configuration once the M2 decision is accepted.
- Target .NET 10 LTS for the initial API baseline and pin the SDK feature band in `global.json`. This decision is revisited if the team has a deployment/runtime constraint.
- Use short task branches from `main`, PR into `main`, one independent human approval, required CI and resolved review threads. The PR author manually squash merges after all gates pass. No automated merge.
- `main` is the source of release artifacts. A deployed AWS development environment is independent of a Git `Dev` branch. The old `Dev` branch was removed after confirming it had no unique commits and no open PRs targeting it; its tip `8918530` remains in `main` history.
- CODEOWNERS maps modules to a primary and a peer. Required code-owner review stays off until the paths and reviewer coverage are proven on actual PRs.

## Alternatives considered

- Long-lived `Dev` plus promotion PRs to `main`: two integration lanes and repeated reviews for this five-person team.
- Separate repositories or services per member: adds deployment and contract overhead before independent scaling is needed.
- Create all target projects and infrastructure now: would claim technology decisions and working artifacts that do not exist.

## Consequences and validation

PRs stay small and integrate frequently. Unfinished behavior must stay unexposed or be guarded until it is ready. CI must build and smoke-test every executable host as it is added. A release must be traceable to a tested commit and deployment evidence. The first PR validates the pinned SDK, API build and HTTP liveness probe; product, database, AWS and frontend readiness require later PRs.

Related: [architecture](../ARCHITECTURE.md), [team](../TEAM.md), [contribution guide](../../CONTRIBUTING.md), [Project](https://github.com/users/Hungle2910/projects/4).
