# M2-01 - Deployment/network design and import contract

*AI Exam Bank - Intelligent Exam Management & AI-Assisted Exam Generation Platform on AWS*

| Task | Owner | Milestone | Status |
| --- | --- | --- | --- |
| M2-01 Deployment/network design and import contract | Huu Phuoc (M2) - AWS/DevOps, import, infrastructure health | W01 Foundation and Contracts | Draft proposal - pending team confirmation |

> How to read this document: items are ordered according to issue M2-01. Items marked "No action required" are descriptive task requirements. Empty cells mean AWS permissions, team sign-off, or collaboration with M1/M3/M4/M5 is required. All pre-filled content is a proposal, not confirmed implementation evidence.

## 1. Goal

**No action required.** This is only the objective: design deployment/network and import contract.

## 2. Context

**No action required.** This is only the project context: .NET modular monolith + durable workers on AWS.

## 3. Scope (IN SCOPE)

**No action required.** Scope is already defined in the issue: import/health API contracts; import + health wireframes; ERD for ImportBatch/Row/DeploymentRecord. These items are addressed in sections 5 and 7.

## 4. Out of scope

**No action required.** Implementation notes: do not add Kubernetes/microservices/AWS services only to increase component count; AI does not approve/publish; Lex/SageMaker/VPN do not block the core MVP.

## 5. Implementation checklist

### 5.1. ☐ Finalize Region, budget, frontend hosting/HTTPS entry, DB hosting, and NAT/endpoints egress

| Item | Proposed default | Confirmation required | Notes |
| --- | --- | --- | --- |
| AWS Region | ap-southeast-1 (Singapore) | | Close to Vietnam, reasonable demo latency, and supports required services. Need to verify Bedrock model/KB availability in this region. |
| Maximum demo budget | | | Team confirmation. Required before building infrastructure. |
| Frontend hosting / HTTPS entry | Public HTTPS entry (443) to reverse proxy or ALB, then route to private app | | A small demo can use a reverse proxy on a public EC2 entry if accepted by the team; ALB adds cost. |
| Backend hosting | API and worker in private subnets, no public application ports | | Managed through SSM, no inbound SSH. |
| DB hosting | Private relational DB. DB on EC2 for lower cost, or RDS if budget allows | | M1/M3 must confirm DB engine and security baseline. If RDS is selected, the DB subnet group must include at least 2 subnets in 2 different Availability Zones. |
| Outbound egress | Compare NAT Gateway and VPC endpoints; prioritize the lower-cost option that still provides access to Bedrock, S3, SSM, and CloudWatch | | NAT is easy to configure but has hourly + data charges. Endpoints are billed per service and region. NAT (default route 0.0.0.0/0) and VPC endpoint (route prefix list or ENI) are different mechanisms; they can be combined and are not two replacement targets for the same route. |
| Cost baseline | | | Fill in after budget and actual regional pricing are available. |

### 5.2. ☐ Design VPC/CIDR/routes/SG, select an IaC tool, and manage state

#### Proposed network diagram

```text
Internet User
  -> HTTPS entry (443) ............ public subnet
  -> .NET API + worker ............ private app subnet
  -> Private relational DB ........ private DB subnet (RDS: 2 subnets in 2 AZs)
  -> Private S3 bucket, Bedrock ... through IAM role
  -> CloudWatch Logs/Metrics, SNS alerts

Admin access
  -> AWS SSM Session Manager
  -> No default inbound SSH
```

#### VPC, CIDR, subnets, route tables

| Component | Proposal | Confirmation required | Notes |
| --- | --- | --- | --- |
| VPC CIDR | 10.20.0.0/16 | | Change if it conflicts with the school network or future VPN. |
| Public subnet | 10.20.1.0/24 - HTTPS entry | | Route to Internet Gateway. |
| Private app subnet | 10.20.11.0/24 - API and worker | | No public IP. Outbound through NAT or endpoints. |
| Private DB subnet | 10.20.21.0/24 - database (AZ-a). If RDS is selected: add 10.20.22.0/24 in AZ-b and place both in one DB subnet group | | Accepts traffic only from the app security group. A self-hosted DB on EC2 needs only 1 subnet; RDS requires at least 2 AZs, even without Multi-AZ enabled. |
| Public route table | 0.0.0.0/0 -> Internet Gateway | | |
| Private route table - default route | 0.0.0.0/0 -> NAT Gateway (only if NAT is selected) | | If NAT is not used, there is no default route to the Internet. Depends on the egress decision in 5.1. |
| Gateway VPC endpoint (S3) | Dedicated route: S3 prefix list -> vpce-id, added to the private route table. Do not use 0.0.0.0/0. | | This route is more specific than 0.0.0.0/0, so S3 traffic goes through the endpoint instead of NAT. |
| Interface VPC endpoints (ssm, ssmmessages, ec2messages, bedrock-runtime, logs...) | No route-table entry. Creates ENIs in private subnets and uses Private DNS. | | Requires an endpoint security group (sg-vpce) that allows 443 from sg-app. |

#### Security groups

| Security group | Allowed inbound | Outbound |
| --- | --- | --- |
| sg-entry | 443 from Internet | App port to sg-app |
| sg-app | App port from sg-entry | DB port to sg-db; 443 to S3/Bedrock/SSM/CloudWatch |
| sg-vpce (if using Interface endpoint) | 443 from sg-app | No special outbound required |
| sg-db | DB port from sg-app | No special outbound required |

#### IaC and state management (draft ADR - team confirmation required)

| Item | Proposal | Confirmation required |
| --- | --- | --- |
| IaC tool | Terraform | |
| Rationale | Popular, easy to review with plan output, suitable for VPC/EC2/S3/IAM/SSM, and repeatable for drift detection. | |
| State backend | | |
| State locking | | |
| Required outputs | VPC ID, subnet IDs, SG IDs, instance IDs, bucket name, health endpoint, deployment version. | |
| Apply principles | Every infrastructure change requires plan, review, apply with redacted logs; no manual console edits without documentation. | |
| Repeatability | Re-running plan after apply produces no unexpected changes. | |

### 5.3. ☐ Collaborate with M4/M5 to define CSV contract, ImportBatch/Row, and upload/preview/result wireframes

This section requires confirmation from M4 (question schema) and M5 (job retry/idempotency). The "M4/M5 confirmation" column is intentionally left blank.

#### Proposed CSV schema

| Column | Mandatory | Type | Validation | Notes | M4/M5 confirmation |
| --- | --- | --- | --- | --- | --- |
| subject | Yes | text | Non-empty | Subject name | |
| topic | Yes | text | Non-empty | Topic name | |
| difficulty | Yes | text/number | Within rubric set by the team | easy/medium/hard or 1/2/3 | |
| question_text | Yes | text | Non-empty, within length limits | Question body | |
| option_a | Yes | text | Non-empty | | |
| option_b | Yes | text | Non-empty | | |
| option_c | Yes | text | Non-empty | | |
| option_d | Yes | text | Non-empty | | |
| correct_option | Yes | A/B/C/D | Must match one option | MVP: single-answer MCQ | |
| source | Recommended | text | URL or source description | Provenance and review | |

#### Data model (ImportBatch / ImportRow / DeploymentRecord)

| Entity | Proposed fields | Notes |
| --- | --- | --- |
| ImportBatch | id, file_name, uploaded_by, status, total_rows, valid_rows, invalid_rows, created_at, committed_at, idempotency_key | One CSV upload session. |
| ImportRow | id, batch_id, row_number, raw_values, normalized_values, validation_status, error_code, error_message, question_revision_id | Row-level validation result. |
| DeploymentRecord | id, environment, source_version, deployed_by, deployed_at, health_status, artifact_version, notes | Supports health and deployment evidence. |

Wireframe: see section 7.1 (upload, preview, commit result, health panel).

## 6. Dependencies

**No action required.** Depends on M1-01 owned by M1; M2 only tracks status. Issue note: M1-01 is being finalized; AWS permissions come from the team.

| Dependency | Owner | Status | Link / confirmation date |
| --- | --- | --- | --- |
| M1-01 | M1 - Gia Hung | | |

## 7. Required deliverables

### 7.1. Backend / frontend / DB-data

#### Import / health API contracts

Endpoints are proposed; M1's ADR determines the final paths.

| Method | Endpoint | Purpose | Main response | Security |
| --- | --- | --- | --- | --- |
| POST | /imports/upload | Upload CSV, create batch, and validate | batchId, status, row summary | Teacher or Admin import permission |
| GET | /imports/{batchId}/preview | View row-level validation results | valid rows, invalid rows, errors | Owner or Admin |
| POST | /imports/{batchId}/commit | Commit valid rows to DRAFT questions | committed count, skipped count, row results | Requires Idempotency-Key header |
| GET | /imports/{batchId}/result | View post-commit results | batch status, row mapping | Owner or Admin |
| GET | /admin/infrastructure/health | Infrastructure health and deployment record | status UNKNOWN/UP/DOWN, timestamp, deployment version | Admin only |

#### Idempotency-Key for POST /imports/{batchId}/commit

This proposal requires M5 confirmation.

| Item | Convention |
| --- | --- |
| Location | HTTP request header Idempotency-Key. Do not place it in body or query string. |
| Format | UUID v4 generated by the client for each commit attempt; UI keeps the same key when Retry is clicked. |
| Storage | Server stores the key in ImportBatch.idempotency_key (unique) together with the commit result. |
| First attempt | 200: creates DRAFT questions, returns committed/skipped count and row results. |
| Retry with same key, same batch | 200: returns the stored result, creates no additional questions. A batch can only be successfully committed once. |
| Missing, empty, or invalid header | 400 IDEMPOTENCY_KEY_REQUIRED: no data changes, batch status stays unchanged. |
| Key already used for another batch | 409 IDEMPOTENCY_KEY_CONFLICT: no data changes. |
| Key retention period | |

#### General import rules

- Questions created by commit remain in DRAFT status and require human review before use.
- A batch can only be committed once. Repeated commits (even with a new or same Idempotency-Key) on an already committed batch return the cached result and do not create duplicate questions (M5 confirmation required).
- Row-level errors do not fail the whole batch: valid rows can still commit, failed rows record error_code and error_message.
- Enforce server-side authorization (RBAC/resource permission), never trust UI-only validation.

#### ERD ImportBatch / ImportRow / DeploymentRecord

```text
ImportBatch 1 ---- n ImportRow
ImportRow   n ---- 0..1 QuestionRevision   (through question_revision_id, owned by M4)
ImportBatch n ---- 1 User                   (uploaded_by, owned by M3)
DeploymentRecord: independent, no foreign key to ImportBatch
```

| Relationship | Key | Notes |
| --- | --- | --- |
| ImportBatch - ImportRow | ImportRow.batch_id -> ImportBatch.id | One batch has many rows; unique (batch_id, row_number). |
| ImportRow - QuestionRevision | ImportRow.question_revision_id (nullable) | Populated only after commit. Requires M4 confirmation of table name. |
| ImportBatch - User | ImportBatch.uploaded_by | Requires M3 confirmation of user table. |
| ImportBatch.idempotency_key | Unique | Value comes from commit Idempotency-Key header. Requires M5 confirmation of key generation and retention period. |

#### Wireframe upload / preview / result / health panel

```text
[1] UPLOAD CSV
+--------------------------------------------------+
| Select CSV file  [ Browse... ]                   |
| Required schema: subject, topic, difficulty, ... |
| Limit: .csv only, max [__] MB, [__] rows         |
|                                    [ Upload ]    |
+--------------------------------------------------+

[2] PREVIEW
+--------------------------------------------------+
| Total: __ | Valid: __ | Errors: __               |
| Row | Error column | Error code | Message        |
| ... | ............ | .......... | .......        |
|                      [ Cancel ]  [ Commit valid rows ]|
+--------------------------------------------------+

[3] COMMIT RESULT
+--------------------------------------------------+
| DRAFT questions created: __ | Skipped rows: __   |
| Batch result link | [ Safe retry ]               |
| Retry uses the same Idempotency-Key              |
+--------------------------------------------------+

[4] HEALTH PANEL (Admin)
+--------------------------------------------------+
| Environment | Deployment version | Last deployed |
| App: UP/DOWN/UNKNOWN | Database: ... | S3: ...    |
| Bedrock access: ...  | Log correlation ID         |
+--------------------------------------------------+
```

### 7.2. AWS / integration

#### Network inventory (draft)

| Resource | Estimated quantity | Public? | Notes |
| --- | --- | --- | --- |
| VPC | 1 | | 10.20.0.0/16 |
| Public / private app / private DB subnets | 1 / 1 / 1 (DB on EC2) or 2 in 2 different AZs (RDS) | Public subnet only | If RDS is used, a second DB subnet in another AZ is required. |
| Internet Gateway | 1 | Yes | |
| NAT Gateway (route 0.0.0.0/0) | | | Pending egress decision. |
| VPC endpoints: Gateway (S3, through route prefix list) and Interface (through ENI + DNS, not through route table) | | | Pending egress decision. Can be used together with NAT. |
| EC2 (entry, app/worker, DB if self-hosted) | | | Pending hosting decision. |
| S3 bucket | 1 (private) | No | Block public access, encrypted. |
| CloudWatch Logs/Metrics, SNS | | No | Retention limits apply. |

#### IAM inventory

Least privilege principle; M3 review required.

| Role | Attached to | Proposed required permissions | Confirmation required |
| --- | --- | --- | --- |
| app-runtime-role | EC2 running API and worker | Read/write only the project S3 bucket; invoke required Bedrock model/KB; write CloudWatch Logs; SSM core | |
| entry-role | EC2/ALB entry | SSM core, write CloudWatch Logs | |
| deploy-role / user | Terraform operator | Minimum permissions to create VPC, EC2, S3, IAM according to plan | |
| db-role (if DB is on EC2) | EC2 database | SSM core, write logs, backup to S3 | |

#### Cost estimate by region

Fill in after pricing and budget are available.

| Item | Unit cost | Monthly quantity | Total cost | Notes |
| --- | --- | --- | --- | --- |
| EC2 (entry, app, DB) | | | | |
| Storage (EBS/S3) | | | | |
| Public IPv4 | | | | |
| NAT Gateway or VPC endpoints | | | | Compare both options |
| CloudWatch logs/metrics | | | | |
| Bedrock KB + token estimate | | | | Requires M4/M5 invocation estimates |
| Total | | | | |

#### SSM plan

- Every EC2 instance has an instance profile with SSM core permissions; administrative access uses Session Manager.
- Do not open inbound 22; do not use key pairs for routine access.
- Instances in private subnets reach SSM through NAT or VPC endpoints (`ssm`, `ssmmessages`, `ec2messages`) based on the egress decision.
- Enable session logging (CloudWatch or S3) when budget allows; not confirmed yet.
- Secrets are retrieved from SSM Parameter Store or Secrets Manager, not hard-coded, and not written into docs.

### 7.3. Security

| Requirement | Proposed implementation | Confirmation |
| --- | --- | --- |
| Private backend/DB | API, worker, and DB are in private subnets, with no public IPs; SGs accept traffic only from upstream SGs | |
| No default inbound SSH | No port 22 rule; use SSM Session Manager | |
| Upload type/size limits | Accept only .csv, validate MIME and extension, enforce file size and row count caps (exact values set by team) | |
| Secret handling | No hard-coded secrets; credentials are not written into docs; evidence must be redacted | |
| Import authorization | Server-side RBAC: Teacher/Admin upload; Owner/Admin view batch; health endpoint is Admin only | M3 |

## 8. Validation

### 8.1. ☐ Review routes/SG using traffic table; CSV fixtures valid/invalid

#### Traffic table

| Source | Destination | Port | Direction | Allowed | Notes | Review result |
| --- | --- | --- | --- | --- | --- | --- |
| Internet | HTTPS entry | 443 | Inbound | Yes | Only entry exposes 443 | |
| Internet | API app | App port | Inbound | No | API is private | |
| Internet | DB | DB port | Inbound | No | DB is private | |
| sg-entry | sg-app | App port | Inbound | Yes | Only from entry SG | |
| sg-app | sg-db | DB port | Inbound | Yes | Only from app SG | |
| sg-app | S3/Bedrock/SSM/CloudWatch | 443 | Outbound | Yes | Through NAT or endpoints | |
| sg-app | sg-vpce (Interface endpoint) | 443 | Outbound | Yes | Only if Interface endpoint is used | |
| Admin | EC2 | 22 | Inbound | No | Use SSM instead of SSH | |

#### Valid CSV fixture (`valid_import.csv`)

Draft, pending M4 schema confirmation.

```csv
subject,topic,difficulty,question_text,option_a,option_b,option_c,option_d,correct_option,source
Math,Algebra,easy,"What is 2 + 3?",4,5,6,7,B,Grade 6 textbook
Computer Science,Computer Networks,medium,"Which protocol is used for secure web browsing?",HTTP,FTP,HTTPS,SMTP,C,Computer Networking textbook
Physics,Mechanics,hard,"What is the SI unit of force?",Joule,Newton,Watt,Pascal,B,Grade 10 textbook
```

#### Invalid CSV fixture (`invalid_import.csv`)

```csv
subject,topic,difficulty,question_text,option_a,option_b,option_c,option_d,correct_option,source
Math,Algebra,easy,"What is 2 + 3?",4,5,6,7,B,Grade 6 textbook
Math,Algebra,easy,"What is 3 + 3?",5,6,,8,B,
Math,Algebra,easy,"What is 4 + 4?",6,7,8,9,E,
Math,Algebra,extreme,"What is 5 + 5?",9,10,11,12,B,
Math,Algebra,easy,,1,2,3,4,A,
```

| Row | Error | Proposed error code | Expected result |
| --- | --- | --- | --- |
| 2 | No error | | VALID |
| 3 | Missing option_c | MISSING_REQUIRED_FIELD | INVALID, specify option_c |
| 4 | correct_option = E | INVALID_CORRECT_OPTION | INVALID |
| 5 | difficulty outside rubric | INVALID_DIFFICULTY | INVALID |
| 6 | Missing question_text | MISSING_REQUIRED_FIELD | INVALID |

Expected preview: total 5 rows, 1 valid, 4 invalid. Error code names require team consensus.

### 8.2. ☐ API/UI/failure/permission cases pass; results linked below

| Case | Type | Expected result | Actual result | Link |
| --- | --- | --- | --- | --- |
| Upload valid CSV | API | 200, batch created, valid_rows matches | | |
| Upload CSV with row-level errors | API | Preview lists correct row, column, and error code | | |
| Upload non-CSV file | Failure | Rejected with clear error | | |
| Upload file exceeding size limit | Failure | Rejected, no batch created | | |
| Retry commit: two attempts with same Idempotency-Key header, same batch | API | Second attempt returns 200, returns first result, creates no duplicate DRAFT | | |
| Commit missing Idempotency-Key header | Failure | 400 IDEMPOTENCY_KEY_REQUIRED, creates no question, batch state unchanged | | |
| Commit with Idempotency-Key already attached to another batch | Failure | 409 IDEMPOTENCY_KEY_CONFLICT, creates no question | | |
| Unauthorized user calls upload | Permission | 403 | | |
| User views another user's batch | Permission | 403 or 404 | | |
| Non-admin calls /admin/infrastructure/health | Permission | 403 | | |
| Health panel when one component is DOWN | UI | Shows accurate status without leaking secrets | | |

### 8.3. ☐ Live AWS evidence for AWS MUST requirements

Mocks are not deployment evidence. Requires AWS permissions and live infrastructure. Leave blank until results are available.

| Evidence | Status | Path / notes |
| --- | --- | --- |
| Terraform plan/apply output (redacted) | | |
| SSM Session Manager access screenshot/log | | |
| Health endpoint returns deployment version | | |
| Confirmation that DB and backend are not accessible from the Internet | | |
| Confirmation that outbound to S3/Bedrock/CloudWatch works | | |

## 9. Documentation / evidence

### 9.1. ☐ Network diagram, cost sheet, IaC/deploy ADR

| Evidence | Required content | Status | Path / notes |
| --- | --- | --- | --- |
| Network diagram | Public entry, private app, private DB, S3, Bedrock, CloudWatch, SSM | Text diagram in 5.2; image pending | |
| Cost sheet | Region, EC2, storage, IPv4, NAT/endpoints, logs, Bedrock KB, token estimate | Template in 7.2; numbers pending | |
| IaC/deploy ADR | Rationale for IaC, state, plan/apply, outputs, rollback | Draft in 5.2; team confirmation required | |
| Routes/SG review | Allowed and blocked traffic table | Draft in 8.1 | |
| CSV fixtures valid/invalid | At least one valid CSV and one CSV with row-level errors | Draft in 8.1 | |
| Secret handling | No hard-coded secrets, no credentials in docs, redacted evidence | Principle defined in 7.3 | |
| AWS proof | SSM access, redacted plan/apply output, health endpoint evidence | | |

### 9.2. ☐ OpenAPI/contracts/runbooks/config/ADR updated, evidence redacted, release/source/job versions available

| Documentation | Updated? | Release / source / job version | Path |
| --- | --- | --- | --- |
| OpenAPI for import and health | Draft in 7.1 | | |
| Deploy and SSM runbook | | | |
| Sample config (secret-free) | | | |
| Region / egress / IaC ADR | | | |

## 10. Acceptance Criteria

**No action required.** Tick only when PR is complete and verified with evidence; currently left blank.

- [ ] Deployment approach has clear HTTPS access and AWS outbound routing; import has input/output contract.
- [ ] Scope/validation/authorization/domain invariants hold; no hard-coded secret or unresolved task blocker remains.
- [ ] PR is peer-reviewed, relevant checks pass, migrations/config are reproducible, and docs/evidence are linked.
- [ ] Actual Hours and checklist are updated; issue and Project status reflect verified progress.

## 11. Risk / Blocker

Risks from the issue: account permission/quota, unconfirmed budget; avoid building infrastructure before cost analysis.

#### Items pending confirmation

- Real W01 start date and calendar deadline.
- AWS account and deployment permissions.
- Official target region; maximum demo budget.
- DB engine and hosting; NAT Gateway or VPC endpoints.
- IaC state backend and state locking.
- CSV schema sign-off with M4; job idempotency sign-off with M5.

Risk does not mean a feature is broken. Log an Actual blocker only after confirmation.

| Actual blocker | Specific condition | Upstream owner | Next action |
| --- | --- | --- | --- |
| | | | |
| | | | |

## 12. PR / evidence / work log

| Item | Value |
| --- | --- |
| PR | PR #67 (docs/M2-01_deploy_network.md) |
| Tests/API/UI/AWS evidence | | |
| Actual hours | | |
| Reviewer (per docs/TEAM.md) | | |

#### Work log

| Date | Work performed | Hours | Link |
| --- | --- | --- | --- |
| | | | |
| | | | |
| | | | |
| | | | |
