[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $root 'src\core\config.ps1')
. (Join-Path $root 'src\core\environment.ps1')
$catalog = Read-WolfHubJson (Join-Path $root 'config\packages.json')
if ($catalog.packages.Count -lt 1) { throw 'Catalog is empty.' }
$ids = @{}; foreach ($package in $catalog.packages) { if ($ids.ContainsKey($package.id)) { throw "Duplicate package id $($package.id)" }; $ids[$package.id]=$true }
$profiles = Get-ChildItem (Join-Path $root 'profiles') -Filter '*.json' -File
foreach ($file in $profiles) {
    $profile = Read-WolfHubJson $file.FullName
    foreach ($id in $profile.packages) { if (-not $ids.ContainsKey($id)) { throw "Unknown package id '$id' in $($file.Name)" } }
}
$modules = Read-WolfHubJson (Join-Path $root 'config\modules.json')
$moduleIds = @{}; foreach ($module in $modules.modules) {
    if ([string]::IsNullOrWhiteSpace($module.id) -or $moduleIds.ContainsKey($module.id)) { throw "Missing or duplicate module id '$($module.id)'" }
    $moduleIds[$module.id] = $true
    if ([string]::IsNullOrWhiteSpace($module.purpose) -or [string]::IsNullOrWhiteSpace($module.status) -or @($module.dependencies).Count -eq 0) { throw "Module '$($module.id)' is missing purpose/status/dependencies." }
}
foreach ($requiredModule in @('toolkit','packages','windows-tweaks','audio-switcher','system-backup')) {
    if (-not $moduleIds.ContainsKey($requiredModule)) { throw "Required module '$requiredModule' is missing." }
}
$features = Read-WolfHubJson (Join-Path $root 'config\features.json')
$featureIds = @{}; foreach ($feature in $features.features) {
    if ([string]::IsNullOrWhiteSpace($feature.id) -or $featureIds.ContainsKey($feature.id)) { throw "Missing or duplicate feature id '$($feature.id)'" }
    $featureIds[$feature.id] = $true
    foreach ($field in @('name','changes','dependencies','permission','reversible','scope')) {
        if ([string]::IsNullOrWhiteSpace([string]$feature.$field)) { throw "Feature '$($feature.id)' is missing '$field'." }
    }
}
foreach ($requiredFeature in @('packages.install','packages.upgrade-all','toolkit.install','drivers.vendor-installer','tweaks.power-plan','tweaks.explorer','tweaks.context-menu','tweaks.flush-dns')) {
    if (-not $featureIds.ContainsKey($requiredFeature)) { throw "Required action metadata '$requiredFeature' is missing." }
}
$hubSource = Get-Content -LiteralPath (Join-Path $root 'hub-app\WolfHubApp.cs') -Raw -Encoding UTF8
if ($hubSource -match 'C:\\Users\\Rafa\\Desktop\\Projetos\\wolfhub') { throw 'A developer-specific absolute workspace path remains in the hub source.' }
$settings = Get-WolfHubSettings -Root $root
if ($settings.theme -ne 'miku') { throw 'Unexpected theme fallback.' }
$resolved = [IO.Path]::GetFullPath((Join-Path $root 'src\core\bootstrap.ps1'))
if (-not (Test-Path -LiteralPath $resolved)) { throw 'Bootstrap path resolution failed.' }
$environment = Get-WolfHubEnvironment
if ($null -eq $environment.WingetPath) { Write-Output 'PASS: WinGet-unavailable state represented without invoking installation.' }
foreach ($file in Get-ChildItem (Join-Path $root 'src'),(Join-Path $root 'tests') -Recurse -Filter '*.ps1' -File) {
    $tokens=$null; $errors=$null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors) | Out-Null
    if ($errors.Count) { throw "Parse failure in $($file.FullName): $($errors[0].Message)" }
}
Write-Output ("PASS: JSON, {0} profiles, {1} modules, {2} action definitions, IDs, settings, paths and PowerShell syntax." -f $profiles.Count,$moduleIds.Count,$featureIds.Count)
