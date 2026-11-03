# Architecture

## Runtime flow

`wolfhub.ps1` opens `hub-app/dist/WolfHub.exe`, building it with `hub-app/build.ps1` when absent. `-Console` selects the alternate PowerShell TUI and loads `src/core/bootstrap.ps1`. The desktop build embeds the package catalog, module/action manifests, package profiles, Lucide assets and (when built) the standalone WolfHub Audio executable. This lets the central app load its declared modules when copied away from the source tree.

The WinForms hub reads JSON module and action metadata through `FeatureCatalog.cs`. UI pages invoke existing package, toolkit and Windows actions; `StateBackup.cs` captures and restores only the initial state supported by those actions. WolfHub Audio remains a separately built executable and keeps its own XML settings and runtime behavior.

## Boundaries

- `src/core`: environment detection, JSON settings, log writing and startup orchestration
- `src/ui`: terminal rendering, status messages and interactive navigation
- `src/modules/packages`: package selection, installed detection and one-at-a-time WinGet calls
- `config/packages.json`: canonical package IDs and metadata
- `profiles/*.json`: references to canonical package IDs only
- `assets/themes`: visual theme data independent of package logic
- `assets/art/miku.txt`: optional Unicode art, never required for functionality
- `hub-app/FeatureCatalog.cs`: loads the declarative module map and per-action effect/permission/reversal descriptions
- `hub-app/StateBackup.cs`: local baseline capture, comparison, recovery copy and selective restore for supported Windows settings
- `config/modules.json`: module purpose, status, preference location and data directory
- `config/features.json`: user-facing action contract, permissions, scope and reversibility

Settings are preferences, not machine inventory. Default logs go to the current user's local app data folder. The repository settings file contains only portable defaults.

## Module boundaries and profiles

The hub is a coordinator. `modules.json` identifies the toolkit, package installer, Windows tweaks, WolfHub Audio, legacy drivers and future Debloat module. Package profiles only reference IDs in the central package catalog; they are not scripts and do not contain installation commands. The Audio Switcher has its own executable, build, profile model and `%LOCALAPPDATA%\WolfHubAudio\settings.xml`; the hub presents it as a module and can install/update its binary without resetting those preferences.

`features.json` is the declarative contract for state-changing actions. Before starting an action, the hub presents its effect, expected privilege, reversibility and action-specific context. If the manifest entry is missing, the action is blocked instead of using an undocumented confirmation.

## Baseline backup and restore

Before the first supported package/tweak/driver action, the hub creates `%LOCALAPPDATA%\WolfHub\backups\baseline.json` if it does not exist. The baseline records the active power-plan GUID, the current user's Explorer `HideFileExt` and `Hidden` values, the complete managed context-menu registry subtree (as a `.reg` export), and the installed status of known WolfHub package IDs when the WinGet inventory was available. No full installed-program inventory or personal documents are copied.

The restore screen compares the baseline with current supported values and asks for confirmation. Immediately before restore, it creates a `before-restore-*` recovery snapshot. Restore reactivates the saved power plan and restores the two Explorer values and managed registry subtree. It does not uninstall packages, downgrade versions, remove duplicated power plans or roll back drivers. The package inventory is reference information only. This is not a Windows restore point or a full system image.

The backup is not created during application startup or validation. It is written on the user's explicit request or immediately before a confirmed action that can change the system.

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

## Standalone WolfHub Audio application

`audio-app/` is a separate WinForms application; it does not load the PowerShell TUI modules. `AudioSwitcher.cs` contains its presentation controls and the Core Audio/WASAPI services. `build.ps1` compiles the standalone executable with the .NET Framework compiler available on Windows. Lucide resources are embedded during the build; no PowerShell Gallery module is required by the app.

### Audio UI and state flow

- `MainForm` composes the main window, global settings screen and tray menu. The profile sidebar is hidden while global settings are open.
- `AudioDeviceCard` presents the primary output, optional secondary output and recording endpoint in a vertical stack. `StatusBadgeControl` separates the profile's stored value from the endpoint currently selected as a Windows default.
- `AudioCore` enumerates endpoints and sets Windows defaults. The recording meter uses a shared WASAPI capture stream only while the window is visible. Output tests open a render stream on the selected endpoint instead of changing the Windows default.
- Selecting a profile only changes the profile being viewed. A double-click in the sidebar or **Ativar perfil** applies the primary output and available recording endpoint. Profile selector changes are saved immediately; changing primary output or microphone also applies it immediately when the viewed profile is already the applied profile. The optional secondary endpoint is saved without activation.
- The secondary endpoint can be empty. The UI disables switching if either endpoint is missing, unavailable or identical. The tray and the in-window controls use the profile's primary/secondary IDs.
- Global settings enumerate and filter endpoints independently of the viewed profile. They support local aliases, WolfHub list visibility, optional inactive-endpoint display, and startup with Windows.

### Persistence and elevation

The app preserves `%LOCALAPPDATA%\WolfHubAudio\settings.xml` and the endpoint IDs stored there. Profiles, aliases, WolfHub-hidden endpoint IDs and startup preference are persisted through `Preferences`. New XML fields remain optional when older files are loaded. Aliases are display names within WolfHub Audio only.

Ordinary startup runs unelevated. Hiding an endpoint always filters it from WolfHub profile selectors. When the app is already elevated, it additionally attempts the Windows endpoint-visibility policy call. The user can explicitly restart the app through UAC when that OS-level action is needed; the app does not elevate silently. Windows/driver support may affect the policy call.

### Validation limits

`WolfHubAudio.exe --self-test` covers endpoint enumeration, embedded icons, profile state and XML serialization, plus microphone meter calculations/error mapping. It does not validate physical speaker output, live microphone signal, DPI layout on every display, or every vendor's endpoint policy behavior. See [audio-app/README.md](../audio-app/README.md) and [audio-app/WOLFHUB-AUDIO.md](../audio-app/WOLFHUB-AUDIO.md) for end-user behavior.
