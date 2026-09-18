#Requires -Version 7.0
<#
.SYNOPSIS
    Runs the CLI from source. Everything after the script name reaches scdl.

.EXAMPLE
    ./run.ps1 formats "https://soundcloud.com/artist/track"
#>
& "$PSScriptRoot/build.ps1" -Task Run @args
