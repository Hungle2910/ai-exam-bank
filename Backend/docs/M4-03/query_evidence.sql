-- Reads the cost of the statements the question bank ran, from SQL Server's own statistics.
-- Order: (1) dbcc freeproccache on the THROWAWAY database's instance, (2) run the QuestionQueryEvidenceRunner test
-- (see README.md), (3) run this file:
--   sqlcmd -S .\SQLEXPRESS -E -C -I -d AiExamBankM4Perf -i query_evidence.sql -W -s "|"
-- Logical reads are 8 KB pages touched; they do not depend on cache warmth, so they compare runs fairly.
SET NOCOUNT ON;
SELECT
    LEFT(REPLACE(REPLACE(REPLACE(t.text, CHAR(13), ' '), CHAR(10), ' '), '  ', ' '), 150) AS statement_start,
    s.execution_count                                           AS runs,
    s.last_logical_reads                                        AS logical_reads,
    s.last_elapsed_time / 1000                                  AS ms,
    s.last_rows                                                 AS rows_returned,
    CASE WHEN CAST(p.query_plan AS nvarchar(max)) LIKE '%Index Scan%' OR CAST(p.query_plan AS nvarchar(max)) LIKE '%Table Scan%'
         THEN 'scan' ELSE 'seek' END                            AS plan_shape
FROM sys.dm_exec_query_stats s
CROSS APPLY sys.dm_exec_sql_text(s.sql_handle) t
CROSS APPLY sys.dm_exec_query_plan(s.plan_handle) p
WHERE t.text LIKE '%[[]Question%' AND t.text NOT LIKE '%dm_exec%' AND t.text NOT LIKE '%__EFMigrations%'
ORDER BY s.last_logical_reads DESC;
