# Bot review and author merge

## Rollout status

Repository auto-merge is enabled. CodeRabbit installation and a real review/approval
trial are pending. This configuration alone does not install the GitHub App.
Existing main protection remains in force; do not remove required checks or bypass
approval to bootstrap the bot. CI currently checks repository hygiene, not product correctness.

## Activate

1. Sign in as the repository administrator and install the official
   [CodeRabbit GitHub App](https://github.com/apps/coderabbitai) for **only Hungle2910/ai-exam-bank**.
2. Review this configuration PR with an independent human reviewer and merge it.
3. Open a small disposable PR. Confirm review on creation and after a new commit.
   CodeRabbit must request changes for an actionable finding, then review the fix
   and approve the latest commit. Verify GitHub counts its approval.
4. Check an intentionally failing CI change and a conflicting branch separately;
   both must remain unmergeable. Close the disposable PRs without merging faulty code.
5. Record actual CodeRabbit check names and producing App from that run before
   adding any required bot check. Review completion alone is not a clean-review verdict.
6. Before allowing bot-only approval, enforce the independent-human policy below
   with a tested required check or eligible code-owner rules. Until then, every PR
   still needs independent human review by team policy.

## Daily workflow after activation

- Open a focused PR linked to its issue. Keep incomplete work in Draft.
- Bot reviews the diff; GitHub Actions runs checks. Fix actionable defects, add
  appropriate regression tests, then push. The bot reviews new commits automatically.
- Let the bot verify addressed threads. Clicking Resolve is not evidence of a fix.
  Discuss false positives with a reviewer and record the reason.
- Once the latest commit is approved, CI passes, discussions are resolved and
  GitHub reports no conflicts, the author with Write access can Squash and merge
  or enable auto-merge for that PR.
- New changes invalidate old approvals. Review limits or outages mean waiting or
  obtaining a human review under the repository policy, not forcing a bot approval.

`@coderabbitai review` requests another review. Do not use `@coderabbitai approve`
or `@coderabbitai resolve` as substitutes for verification: those are overrides.
Author overrides are disabled; non-author overrides still exist in CodeRabbit.

## Independent human review

IAM/infrastructure, authentication/authorization, migrations, deployment workflows,
CODEOWNERS, merge gates and bot configuration require an independent human reviewer.
Gia Hưng reviews or requests a qualified member. For Gia Hưng's own PR, another
member with Write access reviews. GitHub usernames for the other members must be
confirmed before enabling mandatory ownership rules that would block the sole owner.

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
- [GitHub auto-merge](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/automatically-merging-a-pull-request)
