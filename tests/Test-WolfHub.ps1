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
Write-Output ("PASS: JSON, {0} profiles, IDs, settings, paths and PowerShell syntax." -f $profiles.Count)
