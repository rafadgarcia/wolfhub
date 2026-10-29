param([string]$OutputName = 'WolfHubAudio.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'AudioSwitcher.cs'
$iconRoot = Join-Path $root 'assets\lucide'
$iconPngRoot = Join-Path $iconRoot 'png'
$iconNames = @('volume-2','speaker','mic','play','pencil','ellipsis','plus','settings-2','eye','eye-off','arrow-left-right','save','chevron-down','shield-check','refresh-cw')
$output = Join-Path $root ('dist\' + $OutputName)
$frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $frameworkCompiler)) {
    $frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $frameworkCompiler)) { throw 'Windows .NET Framework C# compiler was not found.' }
$icon = Join-Path $root 'assets\app-icon.ico'
New-Item -ItemType Directory -Path (Split-Path -Parent $output) -Force | Out-Null
$response = Join-Path $root 'build.rsp'
$lines = @('/nologo','/target:winexe','/optimize+','/platform:anycpu','/codepage:65001',('/out:"{0}"' -f $output),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Xml.dll')
if (Test-Path -LiteralPath $icon) { $lines += ('/win32icon:"{0}"' -f $icon) }
foreach ($name in $iconNames) {
    $png = Join-Path $iconPngRoot ($name + '.png')
    $svg = Join-Path (Join-Path $iconRoot 'svg') ($name + '.svg')
    if (-not (Test-Path -LiteralPath $png) -or -not (Test-Path -LiteralPath $svg)) { throw "Lucide icon asset is missing: $name" }
    $lines += ('/resource:"{0}",WolfHubAudio.Icons.{1}.png' -f $png,$name)
}
$lines += ('"{0}"' -f $source)
Set-Content -LiteralPath $response -Value $lines -Encoding ASCII
try { & $frameworkCompiler ('@' + $response) }
finally { Remove-Item -LiteralPath $response -Force -ErrorAction SilentlyContinue }
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }
Write-Output "Built standalone app: $output"
