param(
    [string]$Browser = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
)
$ErrorActionPreference = 'Stop'
$svgDir = Join-Path $PSScriptRoot 'svg'
$pngDir = Join-Path $PSScriptRoot 'png'
if (-not (Test-Path -LiteralPath $Browser)) { throw "Chrome was not found: $Browser" }
New-Item -ItemType Directory -Path $pngDir -Force | Out-Null
$temp = Join-Path $env:TEMP ('wolfhub-lucide-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $svgDir -Filter '*.svg') {
    $svg = (Get-Content -LiteralPath $file.FullName -Raw).Replace('currentColor', '#000000')
    $svg64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($svg))
    $html = '<html><head><style>*{margin:0;padding:0}html,body{width:24px;height:24px;overflow:hidden;background:transparent}img{display:block;width:24px;height:24px}</style></head><body><img src="data:image/svg+xml;base64,' + $svg64 + '"></body></html>'
    $htmlPath = Join-Path $temp ($file.BaseName + '.html')
    Set-Content -LiteralPath $htmlPath -Value $html -Encoding UTF8
    $png = Join-Path $pngDir ($file.BaseName + '.png')
    $fileUrl = 'file:///' + $htmlPath.Replace('\', '/')
    $browserArgs = @(
        '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
        '--no-default-browser-check', ('--user-data-dir=' + (Join-Path $temp 'profile')),
        '--force-device-scale-factor=4', '--window-size=24,24', ('--screenshot=' + $png), $fileUrl
    )
    $process = Start-Process -FilePath $Browser -ArgumentList $browserArgs -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $png)) { throw "SVG rasterization failed: $($file.Name)" }

    Add-Type -AssemblyName System.Drawing
    $source = [Drawing.Bitmap]::FromFile($png)
    $alphaImage = New-Object Drawing.Bitmap($source.Width, $source.Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $source.Height; $y++) {
        for ($x = 0; $x -lt $source.Width; $x++) {
            $pixel = $source.GetPixel($x, $y)
            $alpha = 255 - [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B))
            $alphaImage.SetPixel($x, $y, [Drawing.Color]::FromArgb($alpha, 255, 255, 255))
        }
    }
    $source.Dispose()
    $alphaPath = Join-Path $temp ($file.BaseName + '.alpha.png')
    $alphaImage.Save($alphaPath, [Drawing.Imaging.ImageFormat]::Png)
    $alphaImage.Dispose()
    Move-Item -LiteralPath $alphaPath -Destination $png -Force
}
Write-Output "Rendered Lucide assets to $pngDir"
