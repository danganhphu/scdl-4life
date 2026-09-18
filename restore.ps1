#Requires -Version 7.0
<#
.SYNOPSIS
    Convenience wrapper. Identical to: ./build.ps1 -Task Restore
#>
& "$PSScriptRoot/build.ps1" -Task Restore @args
