# M2-01 – Deployment/network design and import contract

*AI Exam Bank – Intelligent Exam Management & AI-Assisted Exam Generation Platform on AWS*

| Task | Owner | Milestone | Status |
| --- | --- | --- | --- |
| M2-01 Deployment/network design and import contract | Huu Phuoc (M2) – AWS/DevOps, import, infrastructure health | W01 Foundation and Contracts | Draft proposal – pending team confirmation |

> *How to read this document: items are ordered strictly according to issue M2-01. Items marked "No action required" are descriptive task requirements. Empty cells = require AWS permissions, team sign-off, or collaboration with M1/M3/M4/M5. All pre-filled content represents proposals, not confirmed implementations.*

## 1. Goal

**No action required.** Just the objective: design deployment/network and import contract.

## 2. Context

**No action required.** Just project background (.NET modular monolith + durable workers on AWS).

## 3. Scope (IN SCOPE)

**No action required.** Scope defined in issue: import/health API contracts; import + health wireframes; ERD ImportBatch/Row/DeploymentRecord. These are addressed in sections 5 and 7.

## 4. Out of scope

**No action required.** Implementation notes: do not add Kubernetes/microservices/AWS services just to increase component count; AI does not approve/publish; Lex/SageMaker/VPN do not block core MVP.

## 5. Implementation checklist

### 5.1 ☐ Finalize Region, budget, frontend hosting/HTTPS entry, DB hosting, and egress NAT/endpoints

| Item | Proposed Default | Confirmation Required | Notes |
| --- | --- | --- | --- |
| AWS Region | ap-southeast-1 (Singapore) | | Close to Vietnam, reasonable latency for demo, supports required services. Need to verify Bedrock model/KB availability in this region. |
| Max demo budget | | | Team confirmation. Required before provisioning infrastructure. |
| Frontend hosting / HTTPS entry | Public HTTPS entry (443) to reverse proxy or ALB, routing to private app | | Small demos can use a reverse proxy on a public EC2 entry if accepted by team; ALB adds cost. |
| Backend hosting | API and worker in private subnets, no public app ports | | Managed via SSM, no inbound SSH. |
| DB hosting | Private relational DB. DB on EC2 (cost-effective) or RDS (if budget permits) | | Requires M1/M3 to confirm DB engine and security baseline. |
| Outbound egress | Compare NAT Gateway vs. VPC endpoints; prioritize cost-effective routing for Bedrock, S3, SSM, and CloudWatch | | NAT is easy to configure but billed hourly + data transfer. Endpoints are billed per service and region. |
| Cost baseline | | | To be filled after budget and actual region pricing are set (see section 7.2). |

### 5.2 ☐ Design VPC/CIDR/routes/SG, select IaC tool, and state management

#### Proposed Network Diagram

```text
Internet User
  -> HTTPS entry (443) ............ public subnet
  -> .NET API + worker ............ private app subnet
  -> Private relational DB ........ private DB subnet
  -> Private S3 bucket, Bedrock ... via IAM role
  -> CloudWatch Logs/Metrics, SNS alerts

Admin access
  -> AWS SSM Session Manager
  -> No default inbound SSH
```

#### VPC, CIDR, subnets, route tables

| Component | Proposal | Confirmation Required | Notes |
| --- | --- | --- | --- |
| VPC CIDR | 10.20.0.0/16 | | Modify if it conflicts with campus network or future VPN. |
| Public subnet | 10.20.1.0/24 – HTTPS entry | | Route to Internet Gateway. |
| Private app subnet | 10.20.11.0/24 – API and worker | | No public IP. Outbound via NAT or endpoints. |
| Private DB subnet | 10.20.21.0/24 – database | | Accepts traffic only from app security group. |
| Public route table | 0.0.0.0/0 -> Internet Gateway | | |
| Private route table | 0.0.0.0/0 -> NAT Gateway or service endpoints per ADR | | Depends on egress decision in 5.1. |

#### Security groups (detailed traffic table in section 8.1)

| Security Group | Inbound Allowed | Outbound |
| --- | --- | --- |
| sg-entry | 443 from Internet | App port to sg-app |
| sg-app | App port from sg-entry | DB port to sg-db; 443 to S3/Bedrock/SSM/CloudWatch |
| sg-db | DB port from sg-app | No special outbound required |

#### IaC and State Management (Draft ADR – team confirmation required)

| Item | Proposal | Confirmation Required |
| --- | --- | --- |
| IaC tool | Terraform | |
| Rationale | Popular, easy to review plan, fits VPC/EC2/S3/IAM/SSM, repeatable to detect drift. | |
| State backend | | |
| State locking | | |
| Required outputs | VPC ID, subnet IDs, SG IDs, instance IDs, bucket name, health endpoint, deployment version. | |
| Apply principles | All infrastructure changes require plan, review, apply with redacted logs; no manual console edits without documentation. | |
| Repeatability | Re-running plan after apply produces no unexpected changes. | |

### 5.3 ☐ Collaborate with M4/M5 to define CSV contract, ImportBatch/Row, and upload/preview/result wireframes

> *This section requires sign-off from M4 (question schema) and M5 (job retry/idempotency). The "M4/M5 Confirmation" column is left blank.*

#### Proposed CSV Schema

| Column | Mandatory | Type | Validation | Notes | M4/M5 Confirmation |
| --- | --- | --- | --- | --- | --- |
| subject | Yes | text | Non-empty | Subject name | |
| topic | Yes | text | Non-empty | Topic name | |
| difficulty | Yes | text/number | Within rubric set by team | easy/medium/hard or 1/2/3 | |
| question_text | Yes | text | Non-empty, within length limits | Question body | |
| option_a | Yes | text | Non-empty | | |
| option_b | Yes | text | Non-empty | | |
| option_c | Yes | text | Non-empty | | |
| option_d | Yes | text | Non-empty | | |
| correct_option | Yes | A/B/C/D | Must match one option | MVP: Single-answer MCQ | |
| source | Recommended | text | URL or description | Provenance and review | |

#### Data Model (ImportBatch / ImportRow / DeploymentRecord)

| Entity | Proposed Fields | Notes |
| --- | --- | --- |
| ImportBatch | id, file_name, uploaded_by, status, total_rows, valid_rows, invalid_rows, created_at, committed_at, idempotency_key | Represents a single CSV upload session. |
| ImportRow | id, batch_id, row_number, raw_values, normalized_values, validation_status, error_code, error_message, question_revision_id | Row-level validation results. |
| DeploymentRecord | id, environment, source_version, deployed_by, deployed_at, health_status, artifact_version, notes | Tracks deployment health and evidence. |

Wireframes: see section 7.1 (upload, preview, commit result, health panel).

## 6. Dependencies

**No action required.** Dependent on M1-01 owned by M1; M2 tracks status. Issue note: M1-01 is under final review, team provides AWS access.

| Dependency | Owner | Status | Link / Date Confirmed |
| --- | --- | --- | --- |
| M1-01 | M1 – Gia Hung | | |

## 7. Required deliverables

### 7.1 Backend / frontend / DB-data

#### Import / Health API Contracts (Endpoints are proposed; M1's ADR determines final paths)

| Method | Endpoint | Purpose | Main Response | Security |
| --- | --- | --- | --- | --- |
| POST | /imports/upload | Upload CSV, create batch and validate | batchId, status, row summary | Teacher or Admin import permission |
| GET | /imports/{batchId}/preview | View row-level validation results | valid rows, invalid rows, errors | Owner or Admin |
| POST | /imports/{batchId}/commit | Commit valid rows to DRAFT questions | committed count, skipped count, row results | Requires idempotency key |
| GET | /imports/{batchId}/result | View post-commit results | batch status, row mapping | Owner or Admin |
| GET | /admin/infrastructure/health | Infrastructure health and deployment records | status UNKNOWN/UP/DOWN, timestamp, deployment version | Admin only |

#### General Import Rules (Proposed)

- Committed questions remain in DRAFT status and require human review before publishing.
- Repeated commits with the same idempotency key do not duplicate questions (M5 confirmation required).
- Row-level errors do not fail the entire batch: valid rows commit successfully, failed rows record error_code and error_message.
- Enforce server-side authorization (RBAC/resource permissions), never trust client-side validation.

#### ERD ImportBatch / ImportRow / DeploymentRecord

```text
ImportBatch 1 ---- n ImportRow
ImportRow   n ---- 0..1 QuestionRevision   (via question_revision_id, owned by M4)
ImportBatch n ---- 1 User                  (uploaded_by, owned by M3)
DeploymentRecord: independent, no foreign key to ImportBatch
```

| Relationship | Keys | Notes |
| --- | --- | --- |
| ImportBatch – ImportRow | ImportRow.batch_id -> ImportBatch.id | One batch contains multiple rows; unique constraint (batch_id, row_number). |
| ImportRow – QuestionRevision | ImportRow.question_revision_id (nullable) | Populated post-commit only. Requires M4 confirmation of table name. |
| ImportBatch – User | ImportBatch.uploaded_by | Requires M3 confirmation of user table. |
| ImportBatch.idempotency_key | Unique | Requires M5 confirmation of key generation and TTL. |

#### Wireframes: Upload / Preview / Result / Health Panel

```text
[1] UPLOAD CSV
+--------------------------------------------------+
| Select CSV file  [ Browse... ]                   |
| Required schema: subject, topic, difficulty, ... |
| Limits: .csv only, max [__] MB, [__] rows        |
|                                     [ Upload ]   |
+--------------------------------------------------+

[2] PREVIEW
+--------------------------------------------------+
| Total: __ | Valid: __ | Errors: __               |
| Row | Error Column | Error Code | Message        |
| ... | ............ | .......... | .......        |
|                                [ Cancel ] [ Commit Valid Rows ] |
+--------------------------------------------------+

[3] COMMIT RESULT
+--------------------------------------------------+
| DRAFT Questions Created: __ | Skipped Rows: __   |
| Batch Result Link           | [ Safe Retry ]     |
+--------------------------------------------------+

[4] HEALTH PANEL (Admin)
+--------------------------------------------------+
| Environment | Deployment Version | Last Deployed   |
| App: UP/DOWN/UNKNOWN | Database: ... | S3: ...   |
| Bedrock Access: ...  | Log Correlation ID        |
+--------------------------------------------------+
```

### 7.2 AWS / integration

#### Network Inventory (Draft)

| Resource | Estimated Quantity | Public? | Notes |
| --- | --- | --- | --- |
| VPC | 1 | | 10.20.0.0/16 |
| Subnets (Public / Private app / Private DB) | 1 / 1 / 1 | Public subnet only | Second AZ can be added if RDS is used. |
| Internet Gateway | 1 | Yes | |
| NAT Gateway or VPC endpoints | | | Pending egress decision. |
| EC2 (Entry, app/worker, DB if self-hosted) | | | Pending hosting decision. |
| S3 bucket | 1 (private) | No | Block public access enabled, encrypted. |
| CloudWatch Logs/Metrics, SNS | | No | Retention limits apply. |

#### IAM Inventory (Draft, adhering to least privilege; M3 review required)

| Role | Attached To | Proposed Required Permissions | Confirmation Required |
| --- | --- | --- | --- |
| app-runtime-role | EC2 running API and worker | Read/write project S3 bucket; invoke required Bedrock models/KBs; write CloudWatch Logs; SSM core | |
| entry-role | EC2/ALB entry | SSM core, write CloudWatch Logs | |
| deploy-role / user | Terraform operator | Minimum permissions to create VPC, EC2, S3, IAM per plan | |
| db-role (if DB on EC2) | EC2 database | SSM core, write logs, backup to S3 | |

#### Cost Estimation per Region (To be filled after pricing and budget are set)

| Item | Unit Cost | Monthly Quantity | Total Cost | Notes |
| --- | --- | --- | --- | --- |
| EC2 (Entry, app, DB) | | | | |
| Storage (EBS/S3) | | | | |
| Public IPv4 | | | | |
| NAT Gateway or VPC endpoints | | | | Compare both options |
| CloudWatch logs/metrics | | | | |
| Bedrock KB + token estimate | | | | Requires M4/M5 invocation estimates |
| Total | | | | |

#### SSM Strategy

- All EC2 instances are assigned an instance profile with SSM core permissions; management access is handled via Session Manager.
- Inbound port 22 is not opened; key pairs are not used for routine operations.
- Private subnet instances reach SSM via NAT or VPC endpoints (`ssm`, `ssmmessages`, `ec2messages`) based on egress decision.
- Session logging (CloudWatch or S3) will be enabled when budget allows; currently unconfirmed.
- Secrets retrieved via SSM Parameter Store or Secrets Manager; no hard-coding, excluded from documentation.

### 7.3 Security

| Requirement | Proposed Implementation | Confirmation |
| --- | --- | --- |
| Private Backend/DB | API, worker, and DB in private subnets, no public IPs, SGs accept traffic only from upstream SGs | |
| Inbound SSH disabled | No port 22 rules; use SSM Session Manager | |
| Upload limits and validation | Accept `.csv` only, validate MIME type and extension, enforce file size and row count caps (thresholds to be finalized by team) | |
| Secrets management | No hard-coded secrets; credentials excluded from docs; evidence must be redacted | |
| Import authorization | Server-side RBAC: Teacher/Admin for upload; Owner/Admin for batch view; Health endpoint restricted to Admin | M3 |

## 8. Validation

### 8.1 ☐ Review routes/SGs using traffic matrix; Valid/Invalid CSV fixtures

#### Traffic Matrix

| Source | Destination | Port | Direction | Allowed | Notes | Review Result |
| --- | --- | --- | --- | --- | --- | --- |
| Internet | HTTPS entry | 443 | Inbound | Yes | Only entry exposes port 443 | |
| Internet | API app | App port | Inbound | No | API is private | |
| Internet | DB | DB port | Inbound | No | DB is private | |
| sg-entry | sg-app | App port | Inbound | Yes | Restricted to entry SG | |
| sg-app | sg-db | DB port | Inbound | Yes | Restricted to app SG | |
| sg-app | S3/Bedrock/SSM/CloudWatch | 443 | Outbound | Yes | Via NAT or endpoints | |
| Admin | EC2 | 22 | Inbound | No | Use SSM instead of SSH | |

#### Valid CSV Fixture (`valid_import.csv` – Draft, M4 schema confirmation pending)

```csv
subject,topic,difficulty,question_text,option_a,option_b,option_c,option_d,correct_option,source
Math,Algebra,easy,"What is 2 + 3?",4,5,6,7,B,Grade 6 Textbook
Computer Science,Networking,medium,"Which protocol is used for secure web browsing?",HTTP,FTP,HTTPS,SMTP,C,Computer Networking Coursebook
Physics,Mechanics,hard,"What is the SI unit of force?",Joule,Newton,Watt,Pascal,B,Grade 10 Textbook
```

#### Invalid CSV Fixture (`invalid_import.csv`)

```csv
subject,topic,difficulty,question_text,option_a,option_b,option_c,option_d,correct_option,source
Math,Algebra,easy,"What is 2 + 3?",4,5,6,7,B,Grade 6 Textbook
Math,Algebra,easy,"What is 3 + 3?",5,6,,8,B,
Math,Algebra,easy,"What is 4 + 4?",6,7,8,9,E,
Math,Algebra,extreme,"What is 5 + 5?",9,10,11,12,B,
Math,Algebra,easy,,1,2,3,4,A,
```

| Row | Error Type | Proposed Error Code | Expected Result |
| --- | --- | --- | --- |
| 2 | None | | VALID |
| 3 | Missing option_c | MISSING_REQUIRED_FIELD | INVALID, specify missing option_c |
| 4 | correct_option = E | INVALID_CORRECT_OPTION | INVALID |
| 5 | difficulty outside rubric | INVALID_DIFFICULTY | INVALID |
| 6 | Missing question_text | MISSING_REQUIRED_FIELD | INVALID |

> *Expected Preview: Total 5 rows, 1 valid, 4 invalid. Error code naming requires team consensus.*

### 8.2 ☐ API/UI/failure/permission cases pass; results linked below

| Case | Category | Expected Result | Actual Result | Link |
| --- | --- | --- | --- | --- |
| Upload valid CSV | API | 200 OK, batch created, valid row count matches | | |
| Upload CSV with row errors | API | Preview correctly lists row, column, and error code | | |
| Upload non-CSV file | Failure | Rejected with clear error response | | |
| Upload exceeding size limit | Failure | Rejected, no batch created | | |
| Commit twice with same idempotency key | API | Second attempt does not duplicate questions | | |
| Commit missing idempotency key | Failure | Rejected | | |
| Unauthorized user attempts upload | Permission | 403 Forbidden | | |
| User accesses another user's batch | Permission | 403 Forbidden or 404 Not Found | | |
| Non-admin calls health endpoint | Permission | 403 Forbidden | | |
| Health panel when a component is DOWN | UI | Displays accurate status without leaking secrets | | |

### 8.3 ☐ Live AWS evidence for mandatory AWS requirements (mocks are not valid deployment proof)

> *Requires AWS access and live infrastructure. Left blank until results are available.*

| Evidence | Status | Path / Notes |
| --- | --- | --- |
| Terraform plan/apply output (redacted) | | |
| SSM Session Manager access logs/screenshots | | |
| Health endpoint returning deployment version | | |
| Confirmation that DB and backend are inaccessible from the Internet | | |
| Confirmation of outbound traffic to S3/Bedrock/CloudWatch | | |

## 9. Documentation / evidence

### 9.1 ☐ Network diagram, cost sheet, IaC/deploy ADR

| Evidence | Required Content | Status | Path / Notes |
| --- | --- | --- | --- |
| Network diagram | Public entry, private app, private DB, S3, Bedrock, CloudWatch, SSM | Text diagram in 5.2; image pending | |
| Cost sheet | Region, EC2, storage, IPv4, NAT/endpoints, logs, Bedrock KB, token estimate | Template in 7.2; figures pending | |
| IaC/deploy ADR | Rationale for IaC, state handling, plan/apply, outputs, rollback | Draft in 5.2; pending team review | |
| Routes/SG review | Allowed and blocked traffic matrix | Draft in 8.1 | |
| CSV fixtures (valid/invalid) | At least one valid CSV and one CSV with row-level errors | Draft in 8.1 | |
| Secret handling | No hard-coded secrets, credentials excluded from docs, redacted evidence | Principles defined in 7.3 | |
| AWS proof | SSM access, redacted plan/apply output, health endpoint evidence | | |

### 9.2 ☐ Updated OpenAPI/contracts/runbooks/config/ADRs, redacted evidence, release/source/job versions

| Documentation | Updated? | Release / Source / Job Version | Path |
| --- | --- | --- | --- |
| OpenAPI specs for import and health | Draft in 7.1 | | |
| Deployment and SSM runbooks | | | |
| Sample configuration (secret-free) | | | |
| ADRs for region / egress / IaC | | | |

## 10. Acceptance Criteria

**No action required.** Tick only when PR is complete and verified with evidence; currently blank.

- [ ] Deployment architecture includes secure HTTPS access and clear AWS outbound routing; import contracts define inputs/outputs.
- [ ] Scope, validation, authorization, and domain invariants hold; no hard-coded secrets or unresolved blockers remain.
- [ ] PR is peer-reviewed, related checks pass, migrations/configs are reproducible, and documentation/evidence are linked.
- [ ] Actual hours and checklists are updated; issue and Project status reflect verified progress.

## 11. Risk / Blocker

Identified risks: account permissions/quotas, unconfirmed budget; avoid provisioning infrastructure before cost analysis.

#### Open Items Pending Confirmation (from draft)

- Start date and actual deadline for W01.
- AWS account and deployment permissions.
- Official target region; maximum demo budget.
- DB engine and hosting strategy; NAT Gateway vs. VPC endpoints.
- IaC state backend and locking mechanism.
- CSV schema sign-off with M4; job idempotency mechanism with M5.

| Actual Blocker | Specific Condition | Upstream Owner | Next Action |
| --- | --- | --- | --- |
| | | | |
| | | | |

> *A risk does not mean a feature is broken. Log items in Actual Blockers only after official confirmation.*

## 12. PR / evidence / work log

| Metric | Value |
| --- | --- |
| PR | |
| Tests/API/UI/AWS Evidence | |
| Actual Hours | |
| Reviewer (per docs/TEAM.md) | |

#### Work log

| Date | Work Performed | Hours | Link |
| --- | --- | --- | --- |
| 10/07/2026 | M2-01 - Deployment/network design and import contracts | 4 | --- |
| | | | |
| | | | |
| | | | |
| | | | |