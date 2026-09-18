#Requires -Version 7.0
<#
.SYNOPSIS
    The build engine for scdl. Every other script in the root is a thin wrapper
    over this one.

.DESCRIPTION
    Native AOT needs the MSVC linker. The .NET SDK finds it through vswhere by
    asking for the component id Microsoft.VisualStudio.Component.VC.Tools.x86.x64,
    which Visual Studio 2026 (v18) does not report even when the toolchain is
    installed, and v18 ships vcvars64.bat without the older vcvarsall.bat. This
    script probes both layouts and imports the environment itself, falling back
    to a trimmed self-contained publish when no native toolchain exists at all.

.PARAMETER Task
    Restore, Build (default), Test, Coverage, Publish, Run, or All.

.PARAMETER NoAot
    Skip Native AOT and publish trimmed self-contained instead.

.EXAMPLE
    ./build.ps1 All

.EXAMPLE
    ./build.ps1 Run -- formats "https://soundcloud.com/artist/track"
#>
[CmdletBinding()]
param(
    # Task is the only positional parameter, on purpose. Without the explicit
    # positions below, Configuration and Runtime were positional too, so
    # `./build.ps1 Run -- formats "<url>"` bound "formats" to Configuration and
    # the URL to Runtime, leaving RemainingArguments empty and the CLI with no
    # command at all. The documented invocation silently did nothing useful.
    [Parameter(Position = 0)]
    [ValidateSet('Restore', 'Build', 'Test', 'Coverage', 'Publish', 'Run', 'All')]
    [string]$Task = 'Build',

    [Parameter()]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$Runtime = 'win-x64',

    [Parameter()]
    [switch]$NoAot,

    # Everything after the task is forwarded to the CLI by the Run task.
    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArguments
)

$ErrorActionPreference = 'Stop'

$RepoRoot = $PSScriptRoot
$Solution = Join-Path $RepoRoot 'scdl-4life.slnx'
$CliProject = Join-Path $RepoRoot 'src/Scdl.Cli/Scdl.Cli.csproj'
$ArtifactsRoot = Join-Path $RepoRoot 'artifacts'
$CoverageRoot = Join-Path $ArtifactsRoot 'coverage'

function Write-Step([string]$Message)
{
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-ExitCode([string]$What)
{
    if ($LASTEXITCODE -ne 0)
    {
        throw "$What failed with exit code $LASTEXITCODE."
    }
}

<#
    Locates a script that sets up the MSVC build environment, and returns the
    command line that invokes it. Two layouts exist: Visual Studio 2026 ships
    one script per architecture (vcvars64.bat), earlier versions ship a single
    vcvarsall.bat that takes the architecture as an argument.
#>
function Find-VcEnvironmentCommand([string]$TargetRuntime)
{
    # Linux and macOS link Native AOT with clang and never look for vswhere, and
    # ${env:ProgramFiles(x86)} is null there - Join-Path would throw on it.
    if (-not $IsWindows)
    {
        return $null
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'

    if (-not (Test-Path $vswhere))
    {
        return $null
    }

    # The vcvarsall argument and the per architecture script name do not agree:
    # x64 is passed as "x64" but its script is vcvars64.bat, not vcvarsx64.bat.
    $architecture, $scriptName = switch -Wildcard ($TargetRuntime)
    {
        '*-arm64' {
            'arm64', 'vcvarsarm64.bat'
        }
        '*-x86' {
            'x86', 'vcvars32.bat'
        }
        default {
            'x64', 'vcvars64.bat'
        }
    }

    $candidates = @()
    $candidates += & $vswhere -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
        -latest -format value -property installationPath 2> $null
    $candidates += & $vswhere -products * -all -prerelease -format value -property installationPath 2> $null

    foreach ($path in ($candidates | Where-Object { $_ } | Select-Object -Unique))
    {
        $buildDirectory = Join-Path $path 'VC/Auxiliary/Build'

        $perArchitecture = Join-Path $buildDirectory $scriptName

        if (Test-Path $perArchitecture)
        {
            return @{ Script = $perArchitecture; Arguments = '' }
        }

        $vcvarsAll = Join-Path $buildDirectory 'vcvarsall.bat'

        if (Test-Path $vcvarsAll)
        {
            return @{ Script = $vcvarsAll; Arguments = $architecture }
        }
    }

    return $null
}

<#
    Runs the vcvars script in a child cmd and copies the resulting environment
    into this session, which is the only reliable way to inherit it.
#>
function Import-VcEnvironment([hashtable]$Command)
{
    $scriptName = Split-Path -Leaf $Command.Script

    Write-Step "Importing MSVC environment from $scriptName"

    $output = & cmd.exe /c "call `"$( $Command.Script )`" $( $Command.Arguments ) > nul 2>&1 && set"

    Assert-ExitCode $scriptName

    foreach ($line in $output)
    {
        if ($line -match '^([^=]+)=(.*)$')
        {
            Set-Item -Path "env:$( $Matches[1] )" -Value $Matches[2]
        }
    }
}

function Invoke-Restore
{
    Write-Step 'Restoring tools and packages'

    dotnet tool restore
    Assert-ExitCode 'Tool restore'

    dotnet restore $Solution
    Assert-ExitCode 'Restore'
}

function Invoke-Build
{
    Write-Step "Building $Configuration"

    dotnet build $Solution -c $Configuration
    Assert-ExitCode 'Build'
}

function Invoke-Test
{
    Write-Step 'Running tests'

    # The "test" section in global.json selects the Microsoft.Testing.Platform
    # runner, so plain `dotnet test` drives TUnit. MTP mode wants the solution
    # behind --solution; passing it positionally is a VSTest-mode habit and
    # fails with "should be via '--solution'".
    dotnet test --solution $Solution -c $Configuration --no-build
    Assert-ExitCode 'Tests'
}

function Invoke-Coverage
{
    Write-Step 'Collecting coverage'

    New-Item -ItemType Directory -Force -Path $CoverageRoot | Out-Null

    $raw = Join-Path $CoverageRoot 'coverage.cobertura.xml'
    $reportDirectory = Join-Path $CoverageRoot 'report'

    # dotnet-coverage wraps the whole test run rather than relying on a
    # collector the test platform has to know about, which keeps this working
    # regardless of what the test SDK does with data collectors.
    dotnet tool run dotnet-coverage collect `
        --output $raw `
        --output-format cobertura `
        "dotnet test --solution $Solution -c $Configuration --no-build"

    Assert-ExitCode 'Coverage collection'

    dotnet tool run reportgenerator `
        "-reports:$raw" `
        "-targetdir:$reportDirectory" `
        '-reporttypes:HtmlInline;TextSummary'

    Assert-ExitCode 'Coverage report'

    $summary = Join-Path $reportDirectory 'Summary.txt'

    if (Test-Path $summary)
    {
        Write-Host ''
        Get-Content $summary | Select-Object -First 20
    }
}

function Invoke-Publish
{
    # Plain if statements rather than if expressions. An if expression assigned
    # inline, and especially one inside a string, is the construct every
    # formatter explodes across half a screen; this shape cannot be made worse.
    $useAot = -not $NoAot

    # Only Windows needs an environment imported before the link step. Elsewhere
    # the SDK drives clang directly, so AOT is attempted as-is.
    if ($useAot -and $IsWindows)
    {
        $vcEnvironment = Find-VcEnvironmentCommand -TargetRuntime $Runtime

        if ($vcEnvironment)
        {
            Import-VcEnvironment -Command $vcEnvironment
        }
        else
        {
            Write-Warning 'No MSVC toolchain found; falling back to a trimmed self-contained publish.'
            Write-Warning 'Install the "Desktop development with C++" workload to get a Native AOT binary.'
            $useAot = $false
        }
    }

    $outputFolder = 'trimmed'
    $publishKind = 'trimmed self-contained'

    if ($useAot)
    {
        $outputFolder = 'aot'
        $publishKind = 'Native AOT'
    }

    $output = Join-Path $ArtifactsRoot $outputFolder

    Write-Step "Publishing $publishKind to $output"

    $arguments = @('publish', $CliProject, '-c', $Configuration, '-r', $Runtime, '-o', $output)

    if (-not $useAot)
    {
        $arguments += @(
            '-p:PublishAot=false'
            '-p:PublishTrimmed=true'
            '-p:PublishSingleFile=true'
            '-p:SelfContained=true'
        )
    }

    dotnet @arguments
    Assert-ExitCode 'Publish'

    # No extension on Linux and macOS, so both names are looked for.
    $binary = Get-ChildItem -Path $output -Include 'scdl', 'scdl.exe' -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($binary)
    {
        Write-Host ''
        Write-Host ("  {0}  ({1:N1} MB)" -f $binary.FullName, ($binary.Length / 1MB)) -ForegroundColor Green
    }
}

function Invoke-Run
{
    Write-Step 'Running scdl from source'

    # The extra -- keeps the CLI's own options from being read by dotnet run.
    dotnet run --project $CliProject -c $Configuration -- @RemainingArguments
}

switch ($Task)
{
    'Restore' {
        Invoke-Restore
    }
    'Build' {
        Invoke-Restore; Invoke-Build
    }
    'Test' {
        Invoke-Restore; Invoke-Build; Invoke-Test
    }
    'Coverage' {
        Invoke-Restore; Invoke-Build; Invoke-Coverage
    }
    'Publish' {
        Invoke-Restore; Invoke-Build; Invoke-Publish
    }
    'Run' {
        Invoke-Run
    }
    'All' {
        Invoke-Restore; Invoke-Build; Invoke-Test; Invoke-Publish
    }
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
