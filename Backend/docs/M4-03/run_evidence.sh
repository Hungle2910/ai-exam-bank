#!/usr/bin/env bash
# Usage (from Backend/): bash docs/M4-03/run_evidence.sh  > docs/M4-03/evidence_<label>.txt
# Needs: a throwaway database named AiExamBankM4Perf with all migrations applied and query_evidence_seed.sql loaded.
set -eo pipefail
SERVER='.\SQLEXPRESS'; DB=AiExamBankM4Perf
# AUTO_CLOSE (on by default on Express) would purge the plan cache when the test closes its connections.
sqlcmd -S "$SERVER" -E -C -Q "ALTER DATABASE $DB SET AUTO_CLOSE OFF" > /dev/null
sqlcmd -S "$SERVER" -E -C -Q "DBCC FREEPROCCACHE" > /dev/null
export AIEXAMBANK_PERF_CONNECTION="Server=$SERVER;Database=$DB;Integrated Security=true;TrustServerCertificate=True"
dotnet test tests/Persistence.Tests --filter "FullyQualifiedName~QuestionQueryEvidenceRunner" 2>&1 | grep -E "Failed|Passed!|Message|Exception" >&2
sqlcmd -S "$SERVER" -E -C -I -d "$DB" -i docs/M4-03/query_evidence.sql -W -s "|"
