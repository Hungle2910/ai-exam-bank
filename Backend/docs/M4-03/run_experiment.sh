#!/usr/bin/env bash
# Prints logical reads per table for each representative statement. Usage: bash run_experiment.sh [label]
sqlcmd -S '.\SQLEXPRESS' -E -C -I -d AiExamBankM4Perf -i "$(dirname "$0")/index_experiment.sql" 2>&1 \
 | grep -E "^CASE|^Table '(Questions|QuestionRevisions)'" \
 | sed -E "s/, physical reads.*//; s/Scan count ([0-9]+), logical reads ([0-9]+)/scans \1, reads \2/" \
 | awk '/^CASE/ {if (line) print line; line=$0; next} {sub(/^Table /,""); line=line" | "$0} END {print line}'
