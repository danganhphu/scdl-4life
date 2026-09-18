#!/usr/bin/env bash
# Thin wrapper over build.ps1. See restore.sh for why pwsh comes from the tool
# manifest rather than the PATH.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
dotnet tool restore
exec dotnet tool run pwsh ./build.ps1 -Task Build "$@"
