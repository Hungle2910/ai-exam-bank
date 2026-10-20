# Infrastructure implementation boundary

No AWS resources are created by this repository bootstrap. M2-01 selects one IaC tool and reviews Region/network/egress/hosting/costs; M2-02 implements VPC/EC2/SSM/S3 baseline. Owner SDK integration and IAM review remain with module owners/M3.

See `docs/AWS_STRATEGY.md`. Do not run fictitious apply/deploy commands or commit keys/state/real tfvars. Planned CI/deploy workflows are tracked under M2-04/05.
