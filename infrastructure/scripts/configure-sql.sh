#!/usr/bin/env bash
# FUTURE HOST OPERATION: initial non-production SQL setup only. Never format disks.
set -euo pipefail
set +x
[[ "$EUID" -eq 0 ]] || { echo 'Run as root on an authorized host.' >&2; exit 1; }
[[ -x /opt/mssql/bin/mssql-conf ]] || { echo 'Install the reviewed SQL Server 2022 package first.' >&2; exit 1; }
mountpoint -q /var/opt/mssql || { echo 'A verified retained data volume must be mounted at /var/opt/mssql.' >&2; exit 1; }
[[ ! -e /var/opt/mssql/data/master.mdf ]] || { echo 'Existing SQL data detected. Use the backup/upgrade runbook, not initial setup.' >&2; exit 1; }
secret=/run/aieb/db-bootstrap-password
[[ -f "$secret" && "$(stat -c %a "$secret")" == 600 && "$(stat -c %u "$secret")" == 0 ]] || {
  echo 'A root-owned mode-0600 bootstrap secret file is required.' >&2; exit 1;
}
[[ -r /etc/aieb/sql/server.crt && -r /etc/aieb/sql/server.key ]] || { echo 'Install the internal SQL TLS certificate and restricted key first.' >&2; exit 1; }
chown mssql:mssql /var/opt/mssql
export ACCEPT_EULA=Y MSSQL_PID=Developer
MSSQL_SA_PASSWORD="$(cat "$secret")"
export MSSQL_SA_PASSWORD
/opt/mssql/bin/mssql-conf -n setup
unset MSSQL_SA_PASSWORD
/opt/mssql/bin/mssql-conf set memory.memorylimitmb 4096
/opt/mssql/bin/mssql-conf set network.tlscert /etc/aieb/sql/server.crt
/opt/mssql/bin/mssql-conf set network.tlskey /etc/aieb/sql/server.key
/opt/mssql/bin/mssql-conf set network.tlsprotocols 1.2
/opt/mssql/bin/mssql-conf set network.forceencryption 1
systemctl restart mssql-server
echo 'Initial SQL configuration completed. Verify TLS and create restricted app/migration logins before use.'
