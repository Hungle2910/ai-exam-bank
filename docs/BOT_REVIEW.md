# Bot review and manual author merge

## Rollout status

Repository auto-merge is disabled. CodeRabbit is installed only on
Hungle2910/ai-exam-bank. It has requested changes on PR #57; clean re-review,
approval of the latest commit and conflict handling still need explicit verification.
Protection on `dev` and `main` remains in force; do not remove required checks or bypass
approval to bootstrap the bot. CI has repository hygiene and an API liveness baseline, not product correctness.

## Verification gates

1. Confirm the [CodeRabbit GitHub App installation](https://github.com/settings/installations)
   remains limited to **Hungle2910/ai-exam-bank**.
2. Open a small disposable PR into `dev`. Confirm review on creation and after a new commit.
   CodeRabbit must request changes for an actionable finding, then review the fix
   and approve the latest commit. Verify GitHub counts its approval.
3. Check an intentionally failing CI change and a conflicting branch separately;
   both must remain unmergeable. Close the disposable PRs without merging faulty code.
4. Record actual CodeRabbit check names and producing App from that run before
   adding any required bot check. Review completion alone is not a clean-review verdict.
5. Before allowing bot-only approval, enforce the independent-human policy below
   with a tested required check or eligible code-owner rules. Until then, every PR
   still needs independent human review by team policy.

## Daily workflow after activation

- Open a focused task PR into `dev` linked to its issue. Keep incomplete work in Draft.
- Bot reviews the diff; GitHub Actions runs checks. Fix actionable defects, add
  appropriate regression tests, then push. The bot reviews new commits automatically.
- Let the bot verify addressed threads. Clicking Resolve is not evidence of a fix.
  Discuss false positives with a reviewer and record the reason.
- Once the latest commit is approved, CI passes, discussions are resolved and
  GitHub reports no conflicts, the author with Write access manually clicks
  Create a merge commit. Auto-merge is disabled for this repository.
- After integration testing on `dev`, open a separate `dev → main` PR with the
  tested commit SHA, release scope and evidence. An independent reviewer approves
  it before the maintainer manually creates the merge commit.
- New changes invalidate old approvals. Review limits or outages mean waiting or
  obtaining a human review under the repository policy, not forcing a bot approval.

`@coderabbitai review` requests another review. Do not use `@coderabbitai approve`
or `@coderabbitai resolve` as substitutes for verification: those are overrides.
Author overrides are disabled; non-author overrides still exist in CodeRabbit.

## Independent human review

IAM/infrastructure, authentication/authorization, migrations, deployment workflows,
CODEOWNERS, merge gates and bot configuration require an independent human reviewer.
Gia Hưng reviews or requests a qualified member. For Gia Hưng's own PR, another
member with Write access reviews. The five confirmed accounts are mapped in
CODEOWNERS, with a peer for each module. Test routing before requiring code-owner approval.

A generic one-approval rule does not distinguish bot approval from human approval.
This document is a team policy until a dedicated required check or code-owner rule
has been implemented and tested; it is not represented as automatic enforcement.
Never let a PR lower its own approval requirements. Do not add sensitive workflows
that execute untrusted PR code with secrets or a write token.

## Conflicts and limits

GitHub detects conflicts and blocks merging. Resolve conflicts locally or use
CodeRabbit's conflict assistance if available in the installed plan; inspect the
result and rerun CI. No bot guarantees that all bugs are found. Add application
build, unit/integration/E2E tests and IaC checks when those components exist.

## References

- [CodeRabbit approval workflow](https://docs.coderabbit.ai/pr-reviews/request-changes-workflow)
- [Automatic review controls](https://docs.coderabbit.ai/configuration/auto-review)
- [GitHub branch protection](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
