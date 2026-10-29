# WolfHub

WolfHub is a modular, local-first Windows setup toolkit. The first MVP is a PowerShell terminal interface with a Miku-inspired theme and an opt-in WinGet installer.

## Requirements

- Windows 10/11
- Windows PowerShell 5.1 minimum; PowerShell 7 is supported
- WinGet (App Installer) for package installation; the rest of the interface works without it
- No administrator privileges are required at startup

## Run

From this directory in PowerShell:

```powershell
.\wolfhub.ps1
```

The script does not change ExecutionPolicy. Installation is only attempted after selecting packages and confirming. WinGet's own installer may still request elevation for selected packages.

## Available today

- Terminal main menu and Miku Edition colors with a monochrome-friendly fallback
- Software catalog grouped by category, numeric selection, select all/clear, installed-package precheck, confirmation and per-package results
- Minimal, Personal and Support profiles backed by central catalog IDs
- Separate standalone Windows Audio Switcher app with tray controls, Core Audio endpoint discovery, device switching, local nicknames, speaker test and explicit UAC relaunch
- Basic interface settings and JSONL logs under `%LOCALAPPDATA%\WolfHub\logs`
- Explicit Coming Soon pages for Windows Tweaks, Debloat and My Toolkit

Catalog IDs originate from the existing `Winget.bat` and are marked `inherited-unverified`. Confirm IDs against the current WinGet source before relying on a particular package. The existing batch file's PowerShell Gallery module installation commands were deliberately excluded; they change repository trust and install external modules.

The Unicode art file is a placeholder. Replace `assets/art/miku.txt` with an art file you have permission to use. It is shown only when the terminal is large enough and each line fits.

The audio module does not install its optional `AudioDeviceCmdlets` dependency. Without it, the UAC relaunch and test tone remain available, while device enumeration/switching is unavailable. Nicknames are WolfHub-only. The speaker test plays a short tone through the current Windows default output.

## Validation

Run safe static checks (no real installations):

```powershell
.\tests\Test-WolfHub.ps1
```

## Project layout

- `src/core`: environment, configuration, logs and startup
- `src/ui`: rendering and menus
- `src/modules/packages`: package selection and WinGet execution
- `config`: central package catalog and default preferences
- `profiles`: package ID sets
- `assets`: theme and replaceable art placeholder
- `docs`: architecture and roadmap
- `tests`: safe JSON/path/profile/parser validation

## Legacy materials

Original scripts, tools and ASUS B660M TUF driver payloads remain in their original locations and are not invoked by WolfHub. They are excluded from Git where appropriate. See `docs/ARCHITECTURE.md` for inspection findings and risks.

## Standalone audio application

The separate tray application is in `audio-app/`. Build and usage instructions are in [audio-app/README.md](audio-app/README.md). It is distributed as a single Windows executable and retains the original audio scripts untouched.
