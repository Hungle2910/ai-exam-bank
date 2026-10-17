# Infrastructure preparation

M2-02 now includes local Terraform definitions, host setup templates and a SQL mapping draft. No AWS resources have been created by this work. Deployment remains disabled by default and IAM profiles are supplied as approved inputs, not created here.

Read [M2-02 local preparation](../docs/M2-02_LOCAL_PREPARATION.md) for offline validation, preview instructions and remaining integration work. [M2-01](../docs/M2-01_deploy_network.md) defines the selected infrastructure design. [Host templates](scripts/README.md) must only be executed during a future authorized deployment. Do not commit credentials, state, saved plans, database backups or real tfvars. Live CI/deployment remains part of M2-04/05.

[IAM/KMS preparation](security/README.md) renders M2-authored policy templates and checks the selected AWS-specific security contracts locally. It does not create IAM roles, attach policies, use AWS credentials or replace account-admin approval and live validation.
