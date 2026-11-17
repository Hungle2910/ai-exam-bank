# Exam selection SQL profile

Refs #31. The selection worker reads approved candidates and persists the result under one SQL Server `SERIALIZABLE` transaction. The EF approved reader shares the same scoped context. The transaction protects the approval, revision and option reads until the result write commits, so the worker can pin the candidates it just read. Callers without that transaction still re-read each selected revision. Finalization independently rechecks approval.

## Local measurement

Measured `ExamGenerationStore.ExecuteAsync`, including its SQL reads and writes, using EF command interception and a stopwatch. The isolated SQL Server database contained 120 approved revisions and one draft under one school, department and topic. Each row below is the median of three measured requests after one warmup. Every run checked approved-only IDs, total points, no duplicates and the exact missing count.

| Requested questions | Commands before | Commands after | Median before (ms) | Median after (ms) |
|---:|---:|---:|---:|---:|
| 10 | 25 | 5 | 23.3 | 15.8 |
| 40 | 85 | 5 | 58.3 | 22.0 |
| 120 | 247 | 7 | 140.1 | 31.5 |
| 121, one missing | 247 | 7 | 149.2 | 30.8 |

The command count explains the bottleneck in this fixture. Production latency, concurrent generation and finalization still need measurement against the agreed staging workload after M1-06 and M5-06 integrate.
