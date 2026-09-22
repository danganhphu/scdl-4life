<#
.SYNOPSIS
    Installs scdl on Windows.

.DESCRIPTION
    Downloads the release archive for this machine, checks it against the
    published SHA256SUMS.txt, unpacks it and puts it on PATH.

    Deliberately written for Windows PowerShell 5.1, unlike every other script
    here: whoever runs this has installed nothing yet, and a fresh Windows only
    has 5.1. So no ternaries, no null-coalescing, and TLS 1.2 is forced on,
    because 5.1 does not negotiate it by default and GitHub refuses anything
    older.

.PARAMETER Version
    Release to install, such as 0.2.0. Defaults to the latest.

.PARAMETER InstallDir
    Where to unpack. Defaults to %LOCALAPPDATA%\Programs\scdl.

.PARAMETER NoPathUpdate
    Leave PATH alone.

.EXAMPLE
    irm https://raw.githubusercontent.com/danganhphu/scdl-4life/main/install.ps1 | iex

.EXAMPLE
    ./install.ps1 -Version 0.2.0 -InstallDir D:\tools\scdl
#>
param(
    [string]$Version = $env:SCDL_VERSION,
    [string]$InstallDir = $env:SCDL_INSTALL_DIR,
    [switch]$NoPathUpdate
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repository = 'danganhphu/scdl-4life'

function Write-Step([string]$Message)
{
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# PROCESSOR_ARCHITECTURE reports the architecture of the *process* under WOW64,
# so a 32-bit host reading it would see x86. PROCESSOR_ARCHITEW6432 is set only
# in that case and carries the real one.
function Get-Runtime
{
    $architecture = $env:PROCESSOR_ARCHITEW6432

    if (-not $architecture)
    {
        $architecture = $env:PROCESSOR_ARCHITECTURE
    }

    if ($architecture -eq 'AMD64')
    {
        return 'win-x64'
    }

    throw "No scdl build for $architecture. Only win-x64 is published; build from source instead: https://github.com/$repository"
}

function Get-LatestVersion
{
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repository/releases/latest" -UseBasicParsing

    return $release.tag_name -replace '^v', ''
}

function Assert-Checksum([string]$Archive, [string]$SumsFile)
{
    $name = Split-Path $Archive -Leaf
    $expected = $null

    foreach ($line in Get-Content $SumsFile)
    {
        if ($line -match "^([0-9a-fA-F]{64})\s+\*?$([regex]::Escape($name))$")
        {
            $expected = $Matches[1]
        }
    }

    if (-not $expected)
    {
        throw "SHA256SUMS.txt has no entry for $name."
    }

    $actual = (Get-FileHash -Path $Archive -Algorithm SHA256).Hash

    if ($actual -ne $expected.ToUpperInvariant())
    {
        throw "Checksum mismatch for $name. Expected $expected, got $actual. Do not run it."
    }
}

function Add-ToUserPath([string]$Directory)
{
    $current = [Environment]::GetEnvironmentVariable('Path', 'User')

    if (-not $current)
    {
        $current = ''
    }

    $entries = $current -split ';' | Where-Object { $_ }

    if ($entries -contains $Directory)
    {
        return $false
    }

    [Environment]::SetEnvironmentVariable('Path', (($entries + $Directory) -join ';'), 'User')

    # The process copy is separate from the stored one, so the current shell
    # would not see it otherwise.
    $env:Path = "$env:Path;$Directory"

    return $true
}

$runtime = Get-Runtime

if (-not $Version)
{
    Write-Step 'Finding the latest release'
    $Version = Get-LatestVersion
}

if (-not $InstallDir)
{
    $InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\scdl'
}

$archiveName = "scdl-$Version-$runtime.zip"
$base = "https://github.com/$repository/releases/download/v$Version"
$staging = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())

New-Item -ItemType Directory -Path $staging -Force | Out-Null

try
{
    $archive = Join-Path $staging $archiveName
    $sums = Join-Path $staging 'SHA256SUMS.txt'

    Write-Step "Downloading scdl $Version for $runtime"
    Invoke-WebRequest -Uri "$base/$archiveName" -OutFile $archive -UseBasicParsing
    Invoke-WebRequest -Uri "$base/SHA256SUMS.txt" -OutFile $sums -UseBasicParsing

    Write-Step 'Verifying the checksum'
    Assert-Checksum -Archive $archive -SumsFile $sums

    Write-Step "Unpacking into $InstallDir"

    if (Test-Path $InstallDir)
    {
        Remove-Item -Path $InstallDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

    # Expand-Archive on 5.1 has no -Force that overwrites reliably, which is why
    # the directory is cleared above rather than merged into.
    Expand-Archive -Path $archive -DestinationPath $InstallDir

    if (-not $NoPathUpdate)
    {
        if (Add-ToUserPath -Directory $InstallDir)
        {
            Write-Step 'Added it to your PATH. Open a new terminal for other apps to see it.'
        }
    }

    Write-Host ''
    Write-Host "  scdl $Version is at $InstallDir\scdl.exe" -ForegroundColor Green
    Write-Host '  Try: scdl formats "https://soundcloud.com/<user>/<track>"'
    Write-Host ''
}
finally
{
    Remove-Item -Path $staging -Recurse -Force -ErrorAction SilentlyContinue
}
