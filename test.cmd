@echo off
REM Thin wrapper over build.ps1. See restore.cmd for why pwsh comes from the
REM tool manifest rather than the PATH.
setlocal
cd /d "%~dp0"
dotnet tool restore || exit /b 1
dotnet tool run pwsh ./build.ps1 -Task Test %*
exit /b %ERRORLEVEL%
