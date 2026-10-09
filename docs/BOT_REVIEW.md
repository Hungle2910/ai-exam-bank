# Bot review and manual author merge

## Current merge gates

Auto-merge is disabled. For a PR into `dev`, GitHub requires the up-to-date
`Repository quality`, `Application build and smoke`, and `CodeRabbit` checks,
plus resolved conversations. No human approval is required on `dev`; the PR
author manually creates the merge commit after verifying the bot actually
reviewed the latest change. A green CodeRabbit status marked **review skipped**
does not establish that a review happened. An in-progress bot check blocks the PR.

`main` remains a separate release gate: promote the tested `dev` head with a
`dev → main` PR, include the exact tested SHA and evidence, and obtain one
independent human approval before manual merge. CODEOWNERS routes optional
peer review on `dev`; required code-owner review is not enabled.

## Author workflow on `dev`

1. Open a focused PR linked to its issue; use Draft while the work is incomplete.
2. Read CodeRabbit's review and CI results on the latest commit. Fix actionable
    findings, add regression tests where needed, and push. Request re-review with
    `@coderabbitai review` if the bot did not review the latest change.
3. Verify a proposed fix before resolving a conversation. Record the reason for
    dismissing a false positive. Invite a qualified peer for risky changes to
    IAM, auth, migrations, deployment or merge controls.
4. Confirm all three required checks pass, conversations are resolved and GitHub
    reports no conflict. Then the author clicks **Create a merge commit**.

Bot review is advisory on code quality and does not replace product testing or
domain review. Do not use `@coderabbitai approve` or `@coderabbitai resolve` as
substitutes for verification. An outage or pending required check means waiting
for the check or repairing its integration, not bypassing branch protection.

## Release workflow on `main`

After integration testing on `dev`, open a separate `dev → main` PR. Include
the tested commit SHA, release scope and test/deployment evidence. The promotion
check rejects a missing or stale SHA and requires a successful Application CI
`push` run for that exact `dev` commit. A different team member with Write access
reviews the evidence and approves. The maintainer manually creates the merge
commit when GitHub reports all requirements satisfied.

## Bot verification and limits

CodeRabbit is installed for this repository. Validate on a small PR that it
reviews both the initial diff and a new commit, identifies an actionable finding,
and clears it after a fix. Also verify that failing CI and merge conflicts remain
unmergeable. A successful status can mean a skipped review, especially on older
PRs whose base branch predates the bot configuration; inspect the review text.

GitHub detects merge conflicts. Resolve them, inspect the result and rerun CI.
No bot guarantees that all bugs are found. Add application tests and deployment
checks alongside the implemented components.

## References

- [CodeRabbit request-changes workflow](https://docs.coderabbit.ai/pr-reviews/request-changes-workflow)
- [CodeRabbit automatic review controls](https://docs.coderabbit.ai/configuration/auto-review)
- [GitHub branch protection](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
