# AWS deployment, security and operations strategy

**Status:** Planned. Resource names/Region/instance sizes/service versions/budget chưa được chốt; IaC và live evidence nằm ở M2-01…M2-10 và owner integrations.

## Network and access

Public HTTPS entry → private .NET app/worker → private DB. VPC/public-private subnets/routes/IGW có traffic matrix. Runtime EC2 managed qua SSM Session Manager; IAM role/SSM Agent/connectivity là prerequisites. NAT hoặc supported VPC endpoints được chọn theo egress và chi phí, không bỏ route rồi public backend để né lỗi.

SG ưu tiên reference security groups và least access. NACL dùng default hoặc rule có requirement cụ thể; review stateless return paths. Flow Logs giúp kiểm network denies; Reachability Analyzer SHOULD cho một path phân tích có ý nghĩa. Một-AZ demo không claim HA.

## Service ownership and purpose

| Service | Purpose | Provision / app owner |
|---|---|---|
| VPC/routes/IGW/EC2/SG/NACL/SSM | Private hosting và controlled administration | Hữu Phước; Minh Phúc review |
| S3 | Import/corpus/artifacts/backups | Hữu Phước; Thiên Phúc corpus; Gia Bảo artifacts |
| Bedrock + KB + vector store | Retrieval/citations và AI draft generation | Hữu Phước infra; Thiên Phúc RAG; Gia Hưng orchestrates |
| IAM / secret delivery | Least privilege/runtime config | Minh Phúc policies; Hữu Phước bindings |
| CloudWatch | Host/app/provider metrics/logs/alarms | Hữu Phước infra; Gia Bảo signal catalog; owners instrument |
| SNS | Actionable incident notifications | Gia Bảo routing; Hữu Phước IaC |
| Lex V2 | Optional intent/slot collection | Thiên Phúc; .NET validates and fulfills |
| SageMaker | Optional difficulty evaluation/advisory | Gia Bảo; Hữu Phước provision support |
| VPN | Stretch real legacy/hybrid demo | Hữu Phước, chỉ khi use case được chốt |

## IAM and configuration

- Separate deploy/CI/runtime/KB/model roles theo requirement; không grant admin để chữa AccessDenied.
- Runtime EC2 dùng instance role và SDK credential chain, không commit long-lived keys.
- Chọn một secret/config mechanism thích hợp; private bucket/public access block, encryption và access-limited backups.
- CI-to-AWS OIDC là preferred design khi workflow triển khai; trust restriction theo repository/ref/environment. Phải review và test trước ghi deployed.
- Không expose credentials, AWS internal config, source content ngoài scope qua health endpoints/logs.
- Supported service Regions/model access/quota được proof trong tuần 1–4, không tự đoán availability.

## Infrastructure as Code and delivery

Chọn một IaC tool, protected state và reviewable plan. Separate dev/staging configuration; secrets không ở example files. CI planned flow: build → tests/security checks → artifact → IaC validate/plan → environment-controlled deploy → migration strategy → smoke → deployment record. Repository hygiene CI đã có; application build/test/deploy CI vẫn thuộc M2-04/05.

Migrations phải backward-compatible khi rollback code; pre-deploy backup; DB restore có quy trình riêng. Một owner khác phải thử clean deploy/rollback/restore theo runbook tại tuần 9. Console-only exceptions có inventory và remediation issue.

## Observability and recovery

Correlation ID nối API→job→provider→revision→review→exam. Job metrics: queued/running/stale/failed/retries/duration; domain metrics do owners emit. Infra metrics: host/service/disk/DB readiness/network denies. Dimensions bounded, logs redacted và retention hữu hạn theo environment.

SNS chỉ báo sự cố actionable; subscription confirmation và **delivery evidence** bắt buộc. Drills: worker crash, timeout sau DB write, provider throttling/access-denied, stale lease, restore và deployment failure. Mỗi drill có expected state/UI message/runbook action.

## Cost and lifecycle

Cost estimate phải theo Region/cấu hình đã chọn: EC2/storage/public IPv4, NAT hours/GB hoặc endpoints, S3/backups, KB vector-store baseline/embeddings/model tokens, CloudWatch volume/retention/cardinality, Lex requests và ML training/endpoint uptime. Không đưa USD estimate khi chưa có config.

NAT billing không dừng chỉ vì EC2 đã stop. Real-time ML endpoints cần xóa khi không dùng; model/config/artifacts có lifecycle riêng. Giới hạn token/batch/attempts; source/artifact retention có owner. Resource tags: Project/Environment/Owner/ManagedBy/CostCenter. Teardown cần bảo toàn backup/evidence và lịch bàn giao; không tự xóa môi trường người dùng cần giữ.

## Well-Architected evidence

Operational excellence: IaC/runbooks/versioned deployments. Security: private boundaries/IAM/RBAC/audit. Reliability: durable recovery/restore. Performance: measured representative queries/jobs. Cost: budget/lifecycle/retention. Sustainability: demo-sized resources và cleanup. Đây là baseline design review, không AWS certification hoặc production assurance.

## Official references

- [AWS Well-Architected Framework](https://docs.aws.amazon.com/wellarchitected/latest/framework/welcome.html)
- [Bedrock retrieval/generation/citations và Guardrails scope](https://docs.aws.amazon.com/bedrock/latest/userguide/kb-test-retrieve-generate.html)
- [Systems Manager private service connectivity](https://docs.aws.amazon.com/systems-manager/latest/userguide/setup-create-vpc.html)
- [VPC/NAT pricing](https://aws.amazon.com/vpc/pricing/)
- [SageMaker endpoint deletion](https://docs.aws.amazon.com/sagemaker/latest/dg/realtime-endpoints-delete-resources.html)
- [GitHub OIDC with AWS](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws)
