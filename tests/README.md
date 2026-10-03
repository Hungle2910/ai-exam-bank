# Test strategy

Planned behavior tests cover domain constraints/approved-only selection, immutable revisions/snapshots, resource authorization/review concurrency, import/generation idempotency, worker crash/recovery, source citations scope and E2E live-AWS evidence.

Foundation owners choose tools consistent with actual backend/frontend versions. Tests and reports are not present at bootstrap; do not claim CI green or coverage percentages. Test implementation is owned by each feature owner, with cross-module cases coordinated by M1/M5.
