#!/usr/bin/env bash
# Read-only checks. Run on a future authorized Ubuntu host, not on AWS now.
set -euo pipefail
source /etc/os-release
[[ "$ID" == ubuntu && "$VERSION_ID" == 22.04 ]] || { echo 'Ubuntu 22.04 is required.' >&2; exit 1; }
[[ "$(uname -m)" == x86_64 ]] || { echo 'An x86_64 host is required.' >&2; exit 1; }
printf 'Operating system: Ubuntu %s\nArchitecture: x86_64\n' "$VERSION_ID"
if systemctl is-active --quiet snap.amazon-ssm-agent.amazon-ssm-agent.service || systemctl is-active --quiet amazon-ssm-agent.service; then
  echo 'SSM Agent service is running; AWS registration and permissions are not verified by this check.'
else
  echo 'SSM Agent is not running. Install a reviewed package or inspect the preinstalled snap.' >&2
  exit 1
fi
