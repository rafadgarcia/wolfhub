function Write-WolfHubLog {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)][string]$Action,
        [Parameter(Mandatory)][string]$Module,
        [Parameter(Mandatory)][string]$Result,
        [AllowNull()][Nullable[int]]$ExitCode,
        [AllowNull()][string]$Message
    )
    try {
        if (-not (Test-Path -LiteralPath $Directory)) { New-Item -ItemType Directory -Path $Directory -Force | Out-Null }
        $record = [ordered]@{ timestamp = [DateTime]::Now.ToString('o'); action = $Action; module = $Module; result = $Result; exitCode = $ExitCode; message = $Message }
        $file = Join-Path $Directory ('wolfhub-{0}.jsonl' -f (Get-Date -Format 'yyyy-MM-dd'))
        ($record | ConvertTo-Json -Compress) | Add-Content -LiteralPath $file -Encoding UTF8
    } catch { Write-Warning "Could not write WolfHub log: $($_.Exception.Message)" }
}
