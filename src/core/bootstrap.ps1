$corePath = Split-Path -Parent $MyInvocation.MyCommand.Path
$script:WolfHubRoot = Split-Path -Parent (Split-Path -Parent $corePath)
. (Join-Path $corePath 'environment.ps1')
. (Join-Path $corePath 'config.ps1')
. (Join-Path $corePath 'logger.ps1')
. (Join-Path $script:WolfHubRoot 'src\ui\renderer.ps1')
. (Join-Path $script:WolfHubRoot 'src\ui\menu.ps1')
. (Join-Path $script:WolfHubRoot 'src\modules\packages\installer.ps1')

function Start-WolfHub {
    $environment = Get-WolfHubEnvironment
    if (-not $environment.IsWindows) { throw 'WolfHub requires Windows.' }
    $script:WolfHubSettings = Get-WolfHubSettings -Root $script:WolfHubRoot
    $script:WolfHubCatalog = Read-WolfHubJson -Path (Join-Path $script:WolfHubRoot 'config\packages.json')
    $script:WolfHubProfiles = @{}
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $script:WolfHubRoot 'profiles') -Filter '*.json' -File) {
        $profile = Read-WolfHubJson -Path $file.FullName
        foreach ($id in $profile.packages) {
            if (-not ($script:WolfHubCatalog.packages | Where-Object id -EQ $id)) { throw "Profile '$($profile.id)' refers to unknown package '$id'." }
        }
        $script:WolfHubProfiles[$profile.id] = $profile
    }
    $script:WolfHubEnvironment = $environment
    Invoke-WolfHubMainMenu
}
