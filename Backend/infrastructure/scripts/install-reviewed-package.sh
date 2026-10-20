#!/usr/bin/env bash
# Install a locally supplied, checksum-verified .deb. No downloads or AWS calls.
set -euo pipefail
[[ "$EUID" -eq 0 ]] || { echo 'Run as root on an authorized host.' >&2; exit 1; }
[[ "$#" -eq 2 && "$1" == *.deb && -f "$1" && "$2" =~ ^[a-fA-F0-9]{64}$ ]] || {
  echo 'Usage: install-reviewed-package.sh /path/package.deb EXPECTED_SHA256' >&2; exit 1;
}
source /etc/os-release
[[ "$ID" == ubuntu && "$VERSION_ID" == 22.04 && "$(uname -m)" == x86_64 ]] || exit 1
actual="$(sha256sum -- "$1")"
[[ "${actual%% *}" == "${2,,}" ]] || { echo 'Package checksum mismatch.' >&2; exit 1; }
dpkg --install -- "$1"
# Missing dependencies must be resolved using the reviewed package manifest;
# this script deliberately does not run an unpinned apt upgrade/install.
