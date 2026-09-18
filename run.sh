#!/usr/bin/env bash
# Runs the CLI from source. Everything after the script name reaches scdl:
#   ./run.sh formats "https://soundcloud.com/artist/track"
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
dotnet tool restore
exec dotnet tool run pwsh ./build.ps1 -Task Run "$@"
