#!/usr/bin/env bash
# Thin wrapper over build.ps1. PowerShell is pinned in .config/dotnet-tools.json
# so every script behaves the same on every machine, whether or not a system
# pwsh is installed.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
dotnet tool restore
exec dotnet tool run pwsh ./build.ps1 -Task Restore "$@"
