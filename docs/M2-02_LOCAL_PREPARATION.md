# M2-02 Local Preparation and Verification

This work prepares M2-02 for review without using AWS. It does not provision infrastructure, open an SSM session, install SQL Server on EC2 or create a database migration. All examples, scripts and UI messages are in English. The design baseline is [M2-01](M2-01_deploy_network.md).

## Prepared implementation

| Area | Included | Still requires integration or live evidence |
| --- | --- | --- |
| Terraform | Bootstrap state bucket; staging VPC, subnets, IGW, NAT, SG rules, S3 endpoint, private bucket, EC2 definitions, retained SQL data volume and log groups | Account/AMI/AZ verification, approved instance profiles, actual cost quote, plan/apply and inventory |
| IAM/KMS | Inputs for three existing profiles; local runtime/boundary/key/custodian/session/state/PassRole JSON renderer, resource-policy review draft and offline regression tests | Admin review, real identifiers, full execution-policy validation, actual profiles/keys and effective-permission/live checks |
| Hosts | Reviewed-package installer, read-only host check, secret-fetch and initial SQL configuration scripts; API unit and Caddy template | Tested package manifest/checksums, mounted data volume, internal SQL certificate, service/log/backup configuration and SSM proof |
| Health API | Protected endpoint returning the UNKNOWN contract | Real authentication, explicit permission grant, real probes and deployment history |
| Health UI | Protected panel and explicitly enabled loopback Development preview | Integration with the shared frontend/login system; no AWS proof is implied |
| SQL data | Persistence-neutral DeploymentRecord entity and SQL Server mapping draft | Shared DbContext, EF migration, clean-database/TLS and constraint verification |

The API's pending authentication scheme authenticates nobody. Client-supplied role/permission headers or bearer strings are ignored. Protected routes return 401 until the shared authentication implementation replaces the pending default. The policy requires an authenticated principal with the exact `permission=InfrastructureHealth.Read` claim; this is an integration contract, not a completed identity system. Do not introduce a hard-coded login, token or role bypass.

## Run the local preview

Use the .NET SDK version in `global.json` for the normal repository workflow. From the repository root in PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:M2__EnableLocalPreview = 'true'
dotnet run --project src/Api/AiExamBank.Api.csproj --no-launch-profile --urls http://127.0.0.1:5080
```

Open `http://127.0.0.1:5080/dev/infrastructure-health`. The banner states that no AWS/database check has occurred. Every component starts UNKNOWN; a missing deployment record remains empty. The preview is opt-in, only mapped in Development and limited to loopback clients. It does not unlock the protected `/api/v1/admin/infrastructure/health` or `/admin/infrastructure` routes. In Staging/Production, the preview routes do not exist even if the flag is accidentally enabled.

Clear the preview environment variables after use:

```powershell
Remove-Item Env:M2__EnableLocalPreview -ErrorAction SilentlyContinue
Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue
```

## Check Terraform without AWS

Terraform 1.10 or later is required. The AWS provider is constrained to the 6.10 patch series and resolved in committed lockfiles. Initialization downloads provider packages from HashiCorp; it does not authorize resource creation. No AWS credentials are needed for these commands:

```powershell
terraform fmt -check -recursive infrastructure
terraform -chdir=infrastructure/bootstrap init -backend=false -input=false
terraform -chdir=infrastructure/bootstrap validate
terraform -chdir=infrastructure/environments/staging init -backend=false -input=false
terraform -chdir=infrastructure/environments/staging validate
terraform -chdir=infrastructure/environments/staging test
terraform -chdir=infrastructure/modules/private-bucket init -backend=false -input=false
terraform -chdir=infrastructure/modules/private-bucket test
```

The `.tftest.hcl` files use a mocked AWS provider and `command = plan`. They do not execute a real AWS plan, contact AWS APIs or create resources. They verify private host settings, IMDSv2, data-volume encryption, gateway type, bucket protections and the disabled-deployment guard. Passing mock tests is not deployment evidence. Never run an ordinary `terraform plan`, `apply`, import, refresh or backend migration while AWS work is deferred.

`deployment_authorized` defaults to false and intentionally rejects real plans unless explicitly changed. This guard is additional protection, not a substitute for account policy. The example account/AMI/profile values are nonfunctional examples. `backend.local.hcl`, real tfvars, saved plans and state are excluded from Git.

## Check the API and UI contract

```powershell
dotnet build AiExamBank.slnx --configuration Release --warnaserror
dotnet test AiExamBank.slnx --configuration Release --no-build --no-restore
python tests/smoke/test_api_health.py
python tests/smoke/test_infrastructure_health.py
```

The new HTTP checks start loopback API processes and verify 401 on protected routes, rejection of fabricated identity headers, preview isolation from Production, opt-in Development behavior and a safe UNKNOWN response. Authenticated allow/deny integration remains pending; these tests do not claim successful real login or a role-based 403 case.

## DeploymentRecord mapping boundary

[DeploymentRecord.sql](../infrastructure/database/DeploymentRecord.sql) is a SQL Server mapping draft, not an EF migration. Merge it into the shared migration stream after the persistence adapter exists. The entity lives with the health preparation code and is not persisted by the snapshot handler. Environment/DeploymentId is unique, so retrying one deployment is distinguishable from redeploying the same artifact in a new operation. No identity User table or second database is introduced.

## Future deployment checklist

Do not execute this checklist until AWS deployment is authorized:

1. Verify account/Region, Singapore AMI/AZ, profile names, account permissions and a current price quote.
2. Review bucket names, state permissions and backend bootstrap/migration. Protect bootstrap and DB data from accidental destruction.
3. Supply real environment configuration outside Git. Review a real plan before applying it.
4. Validate resource inventory, private boundaries, SSM registration/session access and restricted runtime permissions.
5. Review/install packages, mount the retained SQL disk, configure trusted TLS and restricted database users; integrate the shared migration stream.
6. Configure domain/certificate and the entry proxy, trusted forwarded headers, shared authentication and the explicit health permission. Keep local preview disabled.
7. Verify health/UI permissions, migration/config reproducibility, secret redaction and a no-drift re-plan.
8. Record timestamped, redacted evidence. Leave issue acceptance unchecked until its actual requirements pass.

## AWS security handoff incorporated locally

The later AWS-specific decisions in `M3-01_ban2.docx` have been consolidated without importing its FE/BE or authentication claims. [Security preparation](../infrastructure/security/README.md) provides a standard-library-only generator and synthetic permission checks. It uses two separate app/DB CMKs, pre-created profiles, explicit cross-secret denies, separately scoped operator/DBA sessions and `/aieb/staging/ssm-sessions` logging. The naming mismatch with the earlier `/ssm` Terraform log group is corrected. The M2-01 cost worksheet now includes the two keys.

The referenced policy ZIP was unavailable. Generated documents are M2-authored review templates, not a claim that the missing ZIP was received or that another owner approved them. The resource execution policy is deliberately labeled `.review.json`: account-specific dependent-resource authorization still needs review before deployment, especially EC2 launch/tag conditions. The default staging role does not create IAM roles, key policies or secret values. Bootstrap uses its own administrator workflow.

```powershell
python infrastructure/security/render_policies.py --inputs infrastructure/security/policy-inputs.example.json --output local-data/m2-policy-review
python -m unittest discover -s tests/repository -p test_m2_policy_templates.py -v
```

This renders example JSON locally, without network/AWS access. The output directory is ignored by Git. Actual profile/key/account IDs, session-owner mapping, approved resource policy and live negative tests remain prerequisites for AWS evidence. No FE/BE code was modified for this security handoff.

## Evidence record

Record the tool versions, source revision and actual results of local checks in the PR. No AWS deployment, SSM proof, clean SQL migration, live probe, backup restore or application login is claimed by this preparation.

Local preparation verification on 2026-10-17:

- Terraform 1.10.5 / AWS provider 6.10.0: staging/bootstrap validation passed; two staging mock tests and one bucket-protection mock test passed. No real AWS plan/apply or backend initialization was run.
- .NET build: zero warnings/errors; all 12 existing module/architecture tests passed. Both the existing liveness/error smoke check and the new infrastructure-health HTTP check passed.
- The installed SDK is 10.0.302, whereas the repository pins 10.0.400. Compilation and HTTP checks used an isolated source copy with SDK 10.0.302 and runtime 10.0.10. The repository SDK pin was not changed; validation with the pinned SDK is still required in CI or after installing it locally.
- Shell scripts passed `bash -n`; the panel script passed `node --check`. These are syntax checks, not Ubuntu/SQL runtime tests.
- Repository hygiene and local documentation links were checked including newly added files.

Security follow-up on 2026-10-17: incorporated the AWS-only handoff, rendered 18 example JSON documents, and verified three staging Terraform mock runs (including the configured-domain URL), bucket protection and the Python policy/repository suite. These synthetic tests are not AWS IAM validation. No new application/authentication code was changed in this follow-up. The generated resource API policy remains a draft for actual account/dependent-resource review.
