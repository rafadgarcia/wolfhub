[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:WolfHubRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

try {
    . (Join-Path $script:WolfHubRoot 'src\core\bootstrap.ps1')
    Start-WolfHub
}
catch {
    Write-Host ("WolfHub could not start: {0}" -f $_.Exception.Message) -ForegroundColor Red
    exit 1
}
