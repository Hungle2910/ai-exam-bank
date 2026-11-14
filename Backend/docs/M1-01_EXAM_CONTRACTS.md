# Exam workflow contracts for owner review

Issue #1. Source baseline `dev` at `ebb078e`. The workflow below distinguishes deployed-contract candidates in open PRs from interfaces already on `dev`. Scope follows [ADR 0004](adr/0004-multi-school-ministry-mvp.md) and the [permission matrix](IDENTITY_AND_PERMISSIONS.md).

## Runtime inventory

| Boundary | Implementation on `dev` | Owner |
|---|---|---|
| HTTP and web | `Api/Program.cs` composes auth and routes. Next.js uses the pages in `Frontend/src/components/pages`. Identity mutations use a session cookie and antiforgery. | M1 host, M3 auth |
| SQL schema | `Persistence/AiExamBank.Persistence/AiExamBankDbContext.cs` uses EF Core SQL Server with one migration stream. M3 owns school, department and role assignments. M4 owns taxonomy, school-subject mapping and question revisions. | M1 migration coordination, M3 and M4 mappings |
| Questions | `IQuestionCatalog`, `IQuestionWriter`, and `IApprovedQuestionReader` are public M4 contracts. A question ID plus revision number pins content. | M4 |
| Approval | `ReviewDecisionRequest` defines a decision contract. The state transition, independent reviewer checks and audit integration still need M3's implementation. | M3 |
| Jobs | `Api` and `Worker` compose the M5 file-backed job store. Exams uses the Jobs contracts at the API boundary. | M5 |
| Retrieval | `IKnowledgeRetriever` defines scoped retrieval with citations. The live adapter belongs to M4. | M4 with M2 |

## Exam data and UI handoff

| Record | Relationship and invariant | Evidence |
|---|---|---|
| Blueprint and slots | A Teacher owns a school/department Blueprint. Slots refer to the subject's topics and difficulty and must match count and score totals. The Blueprint version protects updates and archive preserves history. | #86 |
| Exam generation, items and gaps | An exam request refers to a Blueprint version and stores its matrix snapshot. Each selected item pins the approved question revision and its score. A gap records a slot's missing count and score. The request can reference an M5 job. | #83 |
| Draft mappings | An exam and draft job identify a batch. Slot output can link pinned question drafts and citations. The draft write and mapping share one SQL transaction. | #84, #85 |
| Exam event and final revision | The event needs a selected revision pointer. A school exam selection requires an assigned DepartmentHead. The Ministry path requires distinct assigned author and confirmer. The approved-only final snapshot and concurrency contract remain open. | ADR 0004, #26 |

Expected navigation is **Ma trận đề → Tạo đề → Theo dõi kết quả**. PR #86 maps list/detail/create/update under `/api/v1/blueprints` and `POST /api/v1/blueprints/{id}/archive`. PR #83 maps `POST /api/v1/exams/generate` and `GET /api/v1/exams/{id}/selection`. Mutations carry CSRF, and generation also carries `Idempotency-Key` and the Blueprint version. Validation errors use HTTP 400, stale versions use 409 and missing or out-of-scope resources use 404 where the endpoint masks existence. Review links, preview and release routes remain subject to their own contracts.

The selection job reads only approved revisions through M4 and persists selected items and gaps together. A complete set enters `NEEDS_REVIEW`; a shortage produces `PARTIAL`. These describe the exam generation result. M5 owns job execution status, M4 owns question revision state, and M3 owns review decisions. An AI suggestion written through `IQuestionWriter` starts as `DRAFT`; a slot-level `REVIEW_REQUIRED` indicates validated draft output awaiting the future submission flow. Only independently approved revisions are eligible for finalization.

## Deployment and verification

The intended AWS path is browser → HTTPS entry → web/API → SQL Server and worker. The retrieval adapter reaches the authorized corpus and Bedrock with the M2-provided runtime role. See the [AWS strategy](AWS_STRATEGY.md) for hosting, IAM and cost decisions.

Existing Blueprint and selection suites exercise count and score checks, duplicate exclusion, empty-bank gaps, exact-revision reads and scope. The SQL selection fixture explicitly seeds approved revisions. End-to-end approval still requires M3. Before accepting the contracts, M1–M5 review role boundaries, schema ordering, error responses and deployment assumptions together. Sources for current runtime navigation are in the [source map](SOURCE_LAYOUT.md).
