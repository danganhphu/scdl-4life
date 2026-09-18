@echo off
REM Runs the CLI from source. Everything after the script name reaches scdl:
REM   run.cmd formats "https://soundcloud.com/artist/track"
setlocal
cd /d "%~dp0"
dotnet tool restore || exit /b 1
dotnet tool run pwsh ./build.ps1 -Task Run %*
exit /b %ERRORLEVEL%
