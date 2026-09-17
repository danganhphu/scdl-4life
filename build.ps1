#Requires -Version 7.0
<#
.SYNOPSIS
    Build, test and publish scdl.

.DESCRIPTION
    Native AOT needs the MSVC linker. The .NET SDK finds it through vswhere by
    asking for the component id Microsoft.VisualStudio.Component.VC.Tools.x86.x64,
    which Visual Studio 2026 (v18) does not report even when the toolchain is
    installed. This script imports the VC environment itself so the publish
    works regardless, and falls back to a trimmed self-contained build when no
    native toolchain exists at all.

.PARAMETER Task
    Build (default), Test, Publish, or All.

.PARAMETER NoAot
    Skip Native AOT and publish trimmed self-contained instead.

.EXAMPLE
    ./build.ps1 All
#>
[CmdletBinding()]
param(
    [ValidateSet('Build', 'Test', 'Publish', 'All')]
    [string]$Task = 'Build',

    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [switch]$NoAot
)

$ErrorActionPreference = 'Stop'

$RepoRoot = $PSScriptRoot
$Solution = Join-Path $RepoRoot 'scdl-4life.slnx'
$CliProject = Join-Path $RepoRoot 'src/Scdl.Cli/Scdl.Cli.csproj'
$ArtifactsRoot = Join-Path $RepoRoot 'artifacts'

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

<#
    Locates a script that sets up the MSVC build environment, and returns the
    command line that invokes it.

    Two wrinkles are handled here. The SDK asks vswhere for the component id
    Microsoft.VisualStudio.Component.VC.Tools.x86.x64, which Visual Studio 2026
    does not report even with the toolchain installed, so that query is only the
    first guess. And Visual Studio 2026 ships the per architecture vcvars64.bat
    without the older vcvarsall.bat, so both layouts are probed.
#>
function Find-VcEnvironmentCommand([string]$TargetRuntime) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'

    if (-not (Test-Path $vswhere)) {
        return $null
    }

    # The vcvarsall argument and the per architecture script name do not agree:
    # x64 is passed as "x64" but its script is vcvars64.bat, not vcvarsx64.bat.
    $architecture, $scriptName = switch -Wildcard ($TargetRuntime) {
        '*-arm64' { 'arm64', 'vcvarsarm64.bat' }
        '*-x86' { 'x86', 'vcvars32.bat' }
        default { 'x64', 'vcvars64.bat' }
    }

    $candidates = @()
    $candidates += & $vswhere -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -latest -format value -property installationPath 2>$null
    $candidates += & $vswhere -products * -all -prerelease -format value -property installationPath 2>$null

    foreach ($path in ($candidates | Where-Object { $_ } | Select-Object -Unique)) {
        $buildDirectory = Join-Path $path 'VC/Auxiliary/Build'

        # Visual Studio 2026 layout: one script per architecture.
        $perArchitecture = Join-Path $buildDirectory $scriptName

        if (Test-Path $perArchitecture) {
            return @{ Script = $perArchitecture; Arguments = '' }
        }

        # Visual Studio 2022 and earlier: one script taking the architecture.
        $vcvarsAll = Join-Path $buildDirectory 'vcvarsall.bat'

        if (Test-Path $vcvarsAll) {
            return @{ Script = $vcvarsAll; Arguments = $architecture }
        }
    }

    return $null
}

<#
    Runs the vcvars script in a child cmd and copies the resulting environment
    into this session, which is the only reliable way to inherit it.
#>
function Import-VcEnvironment([hashtable]$Command) {
    Write-Step "Importing MSVC environment from $(Split-Path -Leaf $Command.Script)"

    $output = & cmd.exe /c "call `"$($Command.Script)`" $($Command.Arguments) > nul 2>&1 && set"

    if ($LASTEXITCODE -ne 0) {
        throw "$(Split-Path -Leaf $Command.Script) failed with exit code $LASTEXITCODE."
    }

    foreach ($line in $output) {
        if ($line -match '^([^=]+)=(.*)$') {
            Set-Item -Path "env:$($Matches[1])" -Value $Matches[2]
        }
    }
}

function Invoke-Build {
    Write-Step "Building $Configuration"
    dotnet build $Solution -c $Configuration

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Test {
    Write-Step 'Running tests'

    # TUnit runs on Microsoft.Testing.Platform. Invoking the produced host
    # directly sidesteps the SDK's VSTest bridge entirely.
    $testHost = Get-ChildItem -Path (Join-Path $RepoRoot 'tests') -Recurse -Filter 'Scdl.Core.Tests.exe' |
        Where-Object { $_.FullName -like "*$Configuration*" } |
        Select-Object -First 1

    if (-not $testHost) {
        throw "Test host not found. Run './build.ps1 Build' first."
    }

    & $testHost.FullName

    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Publish {
    $useAot = -not $NoAot
    $vcEnvironment = if ($useAot) { Find-VcEnvironmentCommand -TargetRuntime $Runtime } else { $null }

    if ($useAot -and -not $vcEnvironment) {
        Write-Warning 'No MSVC toolchain found; falling back to a trimmed self-contained publish.'
        Write-Warning 'Install the "Desktop development with C++" workload to get a Native AOT binary.'
        $useAot = $false
    }

    if ($useAot) {
        Import-VcEnvironment -Command $vcEnvironment
    }

    $output = Join-Path $ArtifactsRoot $(if ($useAot) { 'aot' } else { 'trimmed' })

    Write-Step "Publishing $(if ($useAot) { 'Native AOT' } else { 'trimmed self-contained' }) to $output"

    $arguments = @(
        'publish', $CliProject
        '-c', $Configuration
        '-r', $Runtime
        '-o', $output
    )

    if (-not $useAot) {
        $arguments += @(
            '-p:PublishAot=false'
            '-p:PublishTrimmed=true'
            '-p:PublishSingleFile=true'
            '-p:SelfContained=true'
        )
    }

    dotnet @arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE."
    }

    $binary = Get-ChildItem -Path $output -Filter 'scdl.exe' -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($binary) {
        Write-Host ''
        Write-Host ("  {0}  ({1:N1} MB)" -f $binary.FullName, ($binary.Length / 1MB)) -ForegroundColor Green
    }
}

switch ($Task) {
    'Build' { Invoke-Build }
    'Test' { Invoke-Build; Invoke-Test }
    'Publish' { Invoke-Build; Invoke-Publish }
    'All' { Invoke-Build; Invoke-Test; Invoke-Publish }
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
