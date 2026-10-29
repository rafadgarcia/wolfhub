function Read-WolfHubJson {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required JSON file not found: $Path" }
    try { return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json) }
    catch { throw "Invalid JSON in '$Path': $($_.Exception.Message)" }
}

function Get-WolfHubSettings {
    param([Parameter(Mandatory)][string]$Root)
    $defaults = [ordered]@{ theme = 'miku'; showArt = $true; animations = $false; confirmInstalls = $true; logDirectory = '' }
    $path = Join-Path $Root 'config\settings.json'
    if (Test-Path -LiteralPath $path) {
        $loaded = Read-WolfHubJson -Path $path
        foreach ($key in @('theme','showArt','animations','confirmInstalls','logDirectory')) {
            if ($null -ne $loaded.$key) { $defaults[$key] = $loaded.$key }
        }
    }
    if ($defaults.theme -ne 'miku') { $defaults.theme = 'miku' }
    if ([string]::IsNullOrWhiteSpace([string]$defaults.logDirectory)) {
        $base = if ($env:LOCALAPPDATA) { $env:LOCALAPPDATA } else { [Environment]::GetFolderPath('LocalApplicationData') }
        $defaults.logDirectory = Join-Path $base 'WolfHub\logs'
    }
    # User preferences belong in local app data; keep the repository defaults portable.
    $userPath = Join-Path (Join-Path $defaults.logDirectory '..') 'settings.json'
    if (Test-Path -LiteralPath $userPath -PathType Leaf) {
        $loaded = Read-WolfHubJson -Path $userPath
        foreach ($key in @('theme','showArt','animations','confirmInstalls','logDirectory')) {
            if ($null -ne $loaded.$key) { $defaults[$key] = $loaded.$key }
        }
    }
    [pscustomobject]$defaults
}

function Save-WolfHubSettings {
    param([Parameter(Mandatory)]$Settings, [Parameter(Mandatory)][string]$Root)
    $parent = Split-Path -Parent $Settings.logDirectory
    $path = Join-Path $parent 'settings.json'
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    $Settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding UTF8
}
