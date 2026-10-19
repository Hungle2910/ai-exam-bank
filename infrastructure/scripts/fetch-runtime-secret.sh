#!/usr/bin/env bash
# FUTURE HOST OPERATION: this script calls AWS. Do not run during local preparation.
set -euo pipefail
set +x
umask 077
[[ "$EUID" -eq 0 && "$#" -eq 1 ]] || { echo 'Usage: sudo fetch-runtime-secret.sh app|db' >&2; exit 1; }
case "$1" in
  app) parameter='/aieb/staging/app/connection-string'; filename='app-connection' ;;
  db) parameter='/aieb/staging/db/bootstrap-password'; filename='db-bootstrap-password' ;;
  *) echo 'Select app or db.' >&2; exit 1 ;;
esac
install -d -m 0700 -o root -g root /run/aieb
temp_file="$(mktemp /run/aieb/secret.XXXXXX)"
trap 'rm -f -- "$temp_file"' EXIT
aws ssm get-parameter --region ap-southeast-1 --name "$parameter" --with-decryption --query Parameter.Value --output text > "$temp_file"
[[ -s "$temp_file" ]] || { echo 'The secret is empty.' >&2; exit 1; }
chmod 0600 "$temp_file"
mv -f -- "$temp_file" "/run/aieb/$filename"
echo 'The requested secret was stored in a restricted runtime file. Its value was not printed.'
