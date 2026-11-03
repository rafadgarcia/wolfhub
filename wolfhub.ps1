[CmdletBinding()]
param(
    [switch]$Console
)

$ErrorActionPreference = 'Stop'
$script:WolfHubRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $Console) {
    $hubExe = Join-Path $script:WolfHubRoot 'hub-app\dist\WolfHub.exe'
    $buildScript = Join-Path $script:WolfHubRoot 'hub-app\build.ps1'
    $needsBuild = -not (Test-Path -LiteralPath $hubExe -PathType Leaf)
    if (-not $needsBuild) {
        $hubStamp = (Get-Item -LiteralPath $hubExe).LastWriteTimeUtc
        $inputFiles = @((Get-Item -LiteralPath $PSCommandPath))
        foreach ($relative in @('hub-app','audio-app','config','profiles')) {
            $directory = Join-Path $script:WolfHubRoot $relative
            if (Test-Path -LiteralPath $directory -PathType Container) {
                $inputFiles += Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object {
                    $_.FullName -notmatch '[\\/]dist[\\/]' -and $_.FullName -notmatch '[\\/]icons-temp[\\/]'
                }
            }
        }
        $needsBuild = @($inputFiles | Where-Object { $_.LastWriteTimeUtc -gt $hubStamp }).Count -gt 0
    }
    if ($needsBuild -and (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
        & $buildScript | Out-Null
    }
    if (Test-Path -LiteralPath $hubExe) {
        Start-Process -FilePath $hubExe
        return
    }
}

try {
    . (Join-Path $script:WolfHubRoot 'src\core\bootstrap.ps1')
    Start-WolfHub
}
catch {
    Write-Host ("WolfHub could not start: {0}" -f $_.Exception.Message) -ForegroundColor Red
    exit 1
}
