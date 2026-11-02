# Human review and merge

Auto-merge is disabled. Pull requests into `dev` require an independent human approval, current `Repository quality` and `Application build and smoke` checks, and resolved conversations. @Lancelot-sys25 is the primary reviewer and merge coordinator. @Hungle2910 remains the repository admin and backup reviewer, especially for PRs authored by @Lancelot-sys25. Authors cannot approve their own PRs.

After the current `dev` commit has passed integration checks, promote it through a `dev → main` PR. Record the exact tested SHA and evidence in the PR body. An independent reviewer checks the release evidence before merging. Branch protection applies to admins on both branches.

Reviewers inspect behavior, authorization, data integrity, failure handling, tests and deployment evidence relevant to the change. A successful CI check does not prove product correctness or live AWS readiness. No AI review bot is required by the merge policy.
