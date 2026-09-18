@echo off
REM Thin wrapper over build.ps1. PowerShell is pinned in
REM .config/dotnet-tools.json so every script behaves the same on every machine.
setlocal
cd /d "%~dp0"
dotnet tool restore || exit /b 1
dotnet tool run pwsh ./build.ps1 -Task Restore %*
exit /b %ERRORLEVEL%
