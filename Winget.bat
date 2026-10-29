@echo off
title Pos-formatacao
cd /d "%~dp0"

echo ==========================================
echo        POS-FORMATACAO - PROGRAMAS
echo ==========================================
echo.

echo ==========================================
echo              RUNTIMES
echo ==========================================
echo.

echo [1/24] Microsoft .NET Desktop Runtime 10
winget install -e --id Microsoft.DotNet.DesktopRuntime.10 --accept-package-agreements --accept-source-agreements

echo.
echo [2/24] Microsoft .NET Desktop Runtime 8
winget install -e --id Microsoft.DotNet.DesktopRuntime.8 --accept-package-agreements --accept-source-agreements

echo.
echo [3/24] Microsoft Visual C++ 2015-2022 x64
winget install -e --id Microsoft.VCRedist.2015+.x64 --accept-package-agreements --accept-source-agreements

echo.
echo [4/24] Microsoft Visual C++ 2015-2022 x86
winget install -e --id Microsoft.VCRedist.2015+.x86 --accept-package-agreements --accept-source-agreements

echo.
echo ==========================================
echo              PROGRAMAS
echo ==========================================
echo.

echo [5/24] Google Chrome
winget install -e --id Google.Chrome --accept-package-agreements --accept-source-agreements

echo.
echo [6/24] Chrome Remote Desktop
winget install -e --id Google.ChromeRemoteDesktop --accept-package-agreements --accept-source-agreements

echo.
echo [7/24] Steam
winget install -e --id Valve.Steam --accept-package-agreements --accept-source-agreements

echo.
echo [8/24] WinRAR
winget install -e --id RARLab.WinRAR --accept-package-agreements --accept-source-agreements

echo.
echo [9/24] OpenRGB
winget install -e --id OpenRGB.OpenRGB --accept-package-agreements --accept-source-agreements

echo.
echo [10/24] PawnIO
winget install -e --id namazso.PawnIO --accept-package-agreements --accept-source-agreements

echo.
echo [11/24] Microsoft PowerToys
winget install -e --id Microsoft.PowerToys --accept-package-agreements --accept-source-agreements

echo.
echo [12/24] HWiNFO
winget install -e --id REALiX.HWiNFO --accept-package-agreements --accept-source-agreements

echo.
echo [13/24] CrystalDiskInfo Shizuku Edition
winget install -e --id CrystalDewWorld.CrystalDiskInfo.ShizukuEdition --accept-package-agreements --accept-source-agreements

echo.
echo [14/24] CrystalDiskMark Shizuku Edition
winget install -e --id CrystalDewWorld.CrystalDiskMark.ShizukuEdition --accept-package-agreements --accept-source-agreements

echo.
echo [15/24] WizTree
winget install -e --id AntibodySoftware.WizTree --accept-package-agreements --accept-source-agreements

echo.
echo [16/24] K-Lite Codec Pack Mega + MPC-HC
winget install -e --id CodecGuide.K-LiteCodecPack.Mega --accept-package-agreements --accept-source-agreements

echo.
echo [17/24] qBittorrent
winget install -e --id qBittorrent.qBittorrent --accept-package-agreements --accept-source-agreements

echo.
echo [18/24] OBS Studio
winget install -e --id OBSProject.OBSStudio --accept-package-agreements --accept-source-agreements

echo.
echo [19/24] Git
winget install -e --id Git.Git --accept-package-agreements --accept-source-agreements

echo.
echo [20/24] GitHub CLI
winget install -e --id GitHub.cli --accept-package-agreements --accept-source-agreements

echo.
echo [21/24] League of Legends BR
winget install -e --id RiotGames.LeagueOfLegends.BR --accept-package-agreements --accept-source-agreements

echo.
echo [22/24] Fan Control
winget install -e --id Rem0o.FanControl --accept-package-agreements --accept-source-agreements

echo.
echo ==========================================
echo         MODULOS DO POWERSHELL
echo ==========================================
echo.

echo [23/24] AudioDeviceCmdlets
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"Set-PSRepository -Name PSGallery -InstallationPolicy Trusted; Install-PackageProvider -Name NuGet -Force -Scope CurrentUser; Install-Module AudioDeviceCmdlets -Scope CurrentUser -Force -AllowClobber"

echo.
echo [24/24] BurntToast
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"Set-PSRepository -Name PSGallery -InstallationPolicy Trusted; Install-Module BurntToast -Scope CurrentUser -Force -AllowClobber"

echo.
echo ==========================================
echo              CONCLUIDO
echo ==========================================
echo.
echo Verificando instalacoes...
echo.

winget list --id Microsoft.DotNet.DesktopRuntime.10
winget list --id Microsoft.DotNet.DesktopRuntime.8
winget list --id Microsoft.VCRedist.2015+.x64
winget list --id Microsoft.VCRedist.2015+.x86
winget list --id Google.Chrome
winget list --id Google.ChromeRemoteDesktop
winget list --id Valve.Steam
winget list --id RARLab.WinRAR
winget list --id OpenRGB.OpenRGB
winget list --id namazso.PawnIO
winget list --id Microsoft.PowerToys
winget list --id REALiX.HWiNFO
winget list --id CrystalDewWorld.CrystalDiskInfo.ShizukuEdition
winget list --id CrystalDewWorld.CrystalDiskMark.ShizukuEdition
winget list --id AntibodySoftware.WizTree
winget list --id CodecGuide.K-LiteCodecPack.Mega
winget list --id qBittorrent.qBittorrent
winget list --id OBSProject.OBSStudio
winget list --id Git.Git
winget list --id GitHub.cli
winget list --id RiotGames.LeagueOfLegends.BR
winget list --id Rem0o.FanControl

echo.
echo ==========================================
echo        VERIFICACAO DE FERRAMENTAS
echo ==========================================
echo.

echo Git:
git --version

echo.
echo GitHub CLI:
gh --version

echo.
echo PawnIO:
driverquery /v | findstr /i "pawn"

echo.
echo .NET instalado:
dotnet --list-runtimes

echo.
echo AudioDeviceCmdlets:
powershell -NoProfile -Command "Get-Module -ListAvailable AudioDeviceCmdlets | Select-Object Name, Version, Path"

echo.
echo BurntToast:
powershell -NoProfile -Command "Get-Module -ListAvailable BurntToast | Select-Object Name, Version, Path"

echo.
echo ==========================================
echo              FINALIZADO
echo ==========================================
echo.
echo A janela ficara aberta.
pause