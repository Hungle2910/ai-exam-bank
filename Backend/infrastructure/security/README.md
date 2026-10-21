# Local IAM and KMS Policy Preparation

This folder implements M2's infrastructure inputs from the final AWS-specific decisions in `M3-01_ban2.docx`, section 7.8. It does not import the document's FE/BE or authentication claims. The document repeats older subsections; this preparation selects the later decisions: two separate symmetric customer-managed KMS keys, admin-provisioned IAM profiles/keys, explicit cross-secret denies and the `/aieb/staging/ssm-sessions` log group.

The referenced `M2-01_policy_templates.zip` was not present in the workspace. These templates are independently prepared M2 implementation drafts, not the missing ZIP or an assertion that M3 has reviewed the generated JSON. No AWS SDK, AWS CLI, credentials, remote state or network is used by the generator/tests.

## Render the review bundle

From the repository root:

```powershell
python infrastructure/security/render_policies.py --inputs infrastructure/security/policy-inputs.example.json --output local-data/m2-policy-review
python -m unittest discover -s tests/repository -p test_m2_policy_templates.py -v
```

The example inputs use a dummy account, keys, roles and instances. They are not deployment configuration. Generated files remain under ignored `local-data/`; use a new output directory for each review because existing JSON is never overwritten. Use an untracked input copy for actual account details later. Never put secret values in this configuration.

The generated files cover:

| Documents | Purpose |
| --- | --- |
| `ec2-trust.json` | Trust only EC2 for the three host roles. |
| `entry/app/db-runtime.json` | Own S3/logs/parameter access and explicit cross-secret/state/IAM denies. |
| `entry/app/db-boundary.json` | Cap the attached managed SSM core and runtime policies. Boundaries do not grant permissions themselves. |
| `app/db-key-policy.json` | Separate key administrator, matching runtime decrypt and matching custodian encrypt. |
| `app/db-secret-writer.json` | Exact parameter writes with own-key encryption and no routine read-back. |
| `terraform-trust.json` | One explicit temporary operator role. This is not an OIDC federation policy. |
| `terraform-backend-passrole.json` | Staging state/lock, exact PassRole and no IAM mutation or secret-value access. |
| `bootstrap-backend.json` | Separate bootstrap state-object access; no permission to create the state bucket itself. |
| `terraform-resources.review.json` | Conservative resource API policy draft for review against Terraform. |
| `operator/dba-sessions.json` | Separate entry/app and DB shell access with scoped session ownership. |
| `session-preferences.json` | Standard shell session log streaming preferences, with timeouts. |

## Decisions applied

Account admin provisions `aieb-staging-entry-role/profile`, `aieb-staging-app-role/profile` and `aieb-staging-db-role/profile`, attaches `AmazonSSMManagedInstanceCore`, corresponding runtime policy and boundary. Terraform staging only references existing profile names. It does not create roles, key policies, secrets or Session Manager preferences. Bootstrap has a separate authorized administrator workflow; the staging execution role cannot bootstrap the account by itself.

App and DB read only their exact parameter. Explicit Deny statements constrain `GetParameter`/`GetParameters` from the broad SSM managed policy, and deny path/history reads. Boundaries restrict effective identity permissions; account resource policies and future attached policies still require review. Agent features that need unrelated parameters can be affected and need a deliberate exception, not removal of the separation controls.

Use two symmetric CMKs, one each for app and DB. Runtime decrypt and custodian encrypt require the exact `PARAMETER_ARN` encryption context and SSM `ViaService` in Singapore. Key policies name the exact roles, and the administrator must exist before creating the key. Administrators are privileged and can modify grants/policies; runtime isolation is not a claim that key administrators cannot access data. Preserve an admin recovery procedure and validate key-policy lockout protection before creation. Standard SecureString requires Encrypt/Decrypt rather than advanced-tier data-key operations; parameter Type/Tier/KeyId must also be checked at provisioning time.

The session-owner prefixes must be reviewed for the actual federated role-session naming mechanism; an IAM username, role name and assumed-role session identity are not interchangeable. Do not replace the ownership scope with `session/*` to fix a failed test. Use an explicitly selected account-owned `SSM-SessionManagerRunShell` document and named host IDs. The templates do not grant SSH, forwarding or Run Command. Standard shell/sudo access can expose host credentials and remains privileged.

`cloudWatchEncryptionEnabled=false` leaves the default CloudWatch Logs encryption at rest and does not require a third customer-managed log key. It does not disable transport encryption or CloudWatch's standard at-rest encryption. If policy requires a customer-managed log key, have the admin configure it and its service/runtime permissions before setting this preference true. The two secret keys are not implicitly reused for logging. Review session preferences with the account admin; they are not installed by Terraform staging.

## Resource execution policy review boundary

The resource policy uses exact IAM/S3/Logs names and regional/account EC2 ARNs. Create/mutation tag conditions intentionally fail closed when an EC2 dependent resource lacks the required context. It is **not approved for attachment or proven sufficient to deploy**. In particular, RunInstances authorizes images, volumes, interfaces, subnet and SG separately; pin the approved AMI and refine conditions/action resources using the actual launch specification. Other multi-resource network actions also need verification. Preserve identity/Region/project restrictions when refining them.

Use actual IAM action names: S3 encryption configuration uses `s3:PutEncryptionConfiguration`; public access block uses `s3:PutBucketPublicAccessBlock`. API operation names in a prose document are not necessarily IAM action names. The generator corrects those two names from the prose allowlist.

No key-policy mutation, role/profile mutation or secret-value permission is delegated to Terraform. If encrypted EBS needs key/grant permissions in the target account, restrict them separately to its approved EBS key and EC2 service; never reuse or grant decrypt on the two secret keys. KMS permissions are not globally denied solely to compensate for an incomplete execution policy.

## What local checks establish

Unit checks cover emitted JSON, synthetic identity-policy union with managed SSM parameter reads, exact KMS context/service checks, role/data-prefix separation, state/lock deletion behavior, PassRole scope, session separation and custodian read-back denial. The matcher covers only the emitted condition subset. It is not AWS IAM simulation and does not model SCPs, resource-policy unions, permission-boundary intersection, KMS grants or all Session Manager authorization semantics.

Before an authorized apply, obtain verified account/role/profile/key identifiers, approve the full resource execution policy and bootstrap operator, inspect effective IAM/boundary/key/bucket/endpoint policies, validate session-owner mapping and the shell document, and run AWS policy validation/simulation plus live allow/deny checks. These are future AWS checks, not completed evidence. No live FE/BE work is required to prepare this bundle.

References: [SSM managed core policy](https://docs.aws.amazon.com/aws-managed-policy/latest/reference/AmazonSSMManagedInstanceCore.html), [SecureString KMS encryption](https://docs.aws.amazon.com/systems-manager/latest/userguide/secure-string-parameter-kms-encryption.html), [Session Manager examples](https://docs.aws.amazon.com/systems-manager/latest/userguide/getting-started-restrict-access-examples.html).
