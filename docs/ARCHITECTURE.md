# Architecture

## Runtime flow

`wolfhub.ps1` loads `src/core/bootstrap.ps1`. Bootstrap reads environment capabilities, settings, catalog and profiles, validates every profile reference, then starts the TUI. Package execution lives in the package module and is only reached after selection and confirmation.

## Boundaries

- `src/core`: environment detection, JSON settings, log writing and startup orchestration
- `src/ui`: terminal rendering, status messages and interactive navigation
- `src/modules/packages`: package selection, installed detection and one-at-a-time WinGet calls
- `config/packages.json`: canonical package IDs and metadata
- `profiles/*.json`: references to canonical package IDs only
- `assets/themes`: visual theme data independent of package logic
- `assets/art/miku.txt`: optional Unicode art, never required for functionality

Settings are preferences, not machine inventory. Default logs go to the current user's local app data folder. The repository settings file contains only portable defaults.

## Adding an application

1. Confirm the exact WinGet ID with WinGet or the official package manifest/source.
2. Add a unique stable `id`, display `name`, exact `wingetId` and `category` to `config/packages.json`.
3. Reference the stable `id` in one or more profile JSON files.
4. Keep package arguments minimal; do not accept shell fragments from JSON.
5. Run `tests/Test-WolfHub.ps1` and review the package before selecting it in the UI.

Current initial IDs were carried over from `Winget.bat` and are explicitly marked unverified; this MVP does not claim to have checked them against the live WinGet catalog.

## Workspace inspection and legacy risks

Present in the inspected workspace: `Winget.bat`; three audio automation scripts plus an icon and thumbnail; `Desligar Monitor.exe`; `MixerVolumeWindows10.exe`; `ENERGIA MAXIMA EM NOTEBOOK.txt`; `estrutura.txt`; and a large `DriversRafa/Drivers B660M TUF` tree. The driver tree includes ASUS Intel chipset/MEI/Serial IO packages, Realtek 2.5GbE and Realtek audio packages, installer scripts, binary payloads and third-party components. The tree contains thousands of files and roughly 1.05 GB overall. No `.git` repository was present at inspection.

`Winget.bat` attempts 22 WinGet package IDs and then installs AudioDeviceCmdlets and BurntToast from PowerShell Gallery. It sets PSGallery to Trusted and invokes NuGet/module provider installation; these trust and installation side effects are excluded from WolfHub's catalog. Its IDs are useful candidates but have not been live-verified.

`audio/Alternar_caixinha.ps1` imports AudioDeviceCmdlets and changes default playback/recording devices based on fixed patterns for Caixinha, Logitech G733 and HyperX QuadCast. It expects an icon under a user documents folder. `AudioSwitcherTray.vbs` starts PowerShell with `-ExecutionPolicy Bypass` and contains a hard-coded user-profile script path. The tray script uses WinForms, AudioDeviceCmdlets, a fixed mutex name, these same device-name assumptions and periodic audio changes. `autostart-sound-changer.ps1` was present but its behavior was not independently documented in this implementation. None is called by WolfHub. The legacy audio folder is excluded from the repository because those scripts contain machine-specific paths and behavior.

`ENERGIA MAXIMA EM NOTEBOOK.txt` contains `powercfg` commands to duplicate and activate the built-in High Performance plan. This changes Windows power configuration and may not suit laptops; it is not called by WolfHub.

`DriversRafa` is tied to ASUS B660M TUF and particular Windows 11 x64 package builds. Its `Installer (Pack).bat` runs vendor installers, performs device queries, and warns that the Realtek LAN install can interrupt remote connectivity. Driver installation requires hardware and OS matching and remains out of scope. Third-party EXEs/DLLs and all driver payloads should stay out of Git. The two root portable EXEs are undocumented third-party binaries and are also excluded.

Workspace-relative paths in this report use the repository root as their base. Legacy absolute references found inside the excluded scripts include a user-profile documents path, plus the standard Windows PowerShell executable path in the VBS launcher.

The scripts and installers were inspected as text/tree metadata only; no original file was executed or modified.

The standalone audio application lives in `audio-app/`, separate from the WolfHub TUI. It stores preferences under `%LOCALAPPDATA%\WolfHubAudio\settings.xml`. Endpoint nicknames are local to the app. It uses Windows Core Audio COM interfaces for endpoint discovery/default selection and has an explicit UAC relaunch for endpoint visibility changes. The tray app keeps ordinary startup unelevated.
