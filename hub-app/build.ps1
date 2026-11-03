param([string]$OutputName = 'WolfHub.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $root
$source = Join-Path $root 'WolfHubApp.cs'
$featureSource = Join-Path $root 'FeatureCatalog.cs'
$backupSource = Join-Path $root 'StateBackup.cs'
$iconRoot = Join-Path $repoRoot 'audio-app\assets\lucide'
$iconPngRoot = Join-Path $iconRoot 'png'
$iconNames = @('volume-2','speaker','mic','play','pencil','ellipsis','plus','settings-2','eye','eye-off','arrow-left-right','save','chevron-down','shield-check','refresh-cw')
$output = Join-Path $root ('dist\' + $OutputName)
$frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $frameworkCompiler)) {
    $frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $frameworkCompiler)) { throw 'Windows .NET Framework C# compiler was not found.' }
$icon = Join-Path $repoRoot 'audio-app\assets\app-icon.ico'
New-Item -ItemType Directory -Path (Split-Path -Parent $output) -Force | Out-Null
$response = Join-Path $root 'build.rsp'
$lines = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/platform:anycpu',
    '/codepage:65001',
    ('/out:"{0}"' -f $output),
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Xml.dll',
    '/reference:System.Web.Extensions.dll'
)
if (Test-Path -LiteralPath $icon) { $lines += ('/win32icon:"{0}"' -f $icon) }
foreach ($name in $iconNames) {
    $png = Join-Path $iconPngRoot ($name + '.png')
    if (Test-Path -LiteralPath $png) {
        $lines += ('/resource:"{0}",WolfHub.Icons.{1}.png' -f $png,$name)
    }
}
$lines += ('/resource:"{0}",WolfHub.Config.packages.json' -f (Join-Path $repoRoot 'config\packages.json'))
$lines += ('/resource:"{0}",WolfHub.Config.features.json' -f (Join-Path $repoRoot 'config\features.json'))
$lines += ('/resource:"{0}",WolfHub.Config.modules.json' -f (Join-Path $repoRoot 'config\modules.json'))
foreach ($profileName in @('minimal','personal','support')) {
    $profileFile = Join-Path $repoRoot ('profiles\{0}.json' -f $profileName)
    if (Test-Path -LiteralPath $profileFile) { $lines += ('/resource:"{0}",WolfHub.Profiles.{1}.json' -f $profileFile,$profileName) }
}
$audioExe = Join-Path $repoRoot 'audio-app\dist\WolfHubAudio.exe'
$audioBuild = Join-Path $repoRoot 'audio-app\build.ps1'
$audioNeedsBuild = -not (Test-Path -LiteralPath $audioExe -PathType Leaf)
if (-not $audioNeedsBuild) {
    $audioStamp = (Get-Item -LiteralPath $audioExe).LastWriteTimeUtc
    $audioInputs = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'audio-app') -File -Recurse | Where-Object {
        $_.FullName -notmatch '[\\/]dist[\\/]' -and $_.FullName -notmatch '[\\/]icons-temp[\\/]' -and $_.Extension -in @('.cs','.ps1','.png','.svg','.ico')
    }
    $audioNeedsBuild = @($audioInputs | Where-Object { $_.LastWriteTimeUtc -gt $audioStamp }).Count -gt 0
}
if ($audioNeedsBuild -and (Test-Path -LiteralPath $audioBuild -PathType Leaf)) { & $audioBuild }
if (Test-Path -LiteralPath $audioExe -PathType Leaf) { $lines += ('/resource:"{0}",WolfHub.Audio.WolfHubAudio.exe' -f $audioExe) }
else { throw 'WolfHub Audio could not be built and cannot be embedded as an independent module.' }
$lines += @(('"{0}"' -f $source),('"{0}"' -f $featureSource),('"{0}"' -f $backupSource))
Set-Content -LiteralPath $response -Value $lines -Encoding ASCII
try { & $frameworkCompiler ('@' + $response) }
finally { Remove-Item -LiteralPath $response -Force -ErrorAction SilentlyContinue }
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }
Write-Output "Built standalone app: $output"
