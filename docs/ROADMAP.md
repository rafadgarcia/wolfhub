# Roadmap

## Complete in MVP

- Windows and PowerShell detection, WinGet availability and admin status
- Main TUI, Miku color fallback and optional guarded art loading
- Central JSON catalog and three profile selections
- Package selection, installed precheck, explicit confirmation, per-item execution results and JSONL logs
- Basic settings and safe static validation script
- Separate single-executable Audio Switcher app with tray, endpoint selection, local nicknames, speaker test and explicit UAC relaunch
- Per-profile primary, secondary and recording device selections; tray switching follows the active profile
- Legacy inventory and risk notes

## MVP limitations

- Initial WinGet IDs are inherited candidates and have not been checked against the live catalog.
- Installed detection parses human-readable `winget list` output; localization and output changes can affect matching.
- The TUI is line-oriented with number-based selection (arrow-key navigation is future work).
- Miku art remains a documented placeholder until a permitted art file is provided.
- No installation integration tests are run; package install must be separately confirmed in the application.
- The inspected workspace was not initialized as a Git repository.

## Future modules

- Windows Tweaks with previews, explicit confirmation, idempotence and rollback guidance
- Debloat with individually reviewed packages and simulation
- Portable toolkit with checksummed downloads
- Hardware-aware official driver lookup and user-reviewed installation
- Installer packaging/signing and public release process for the standalone audio app
- Global endpoint rename remains out of scope; app-local nicknames are portable and reversible
- Signed/versioned distribution bootstrap and update verification
- Better terminal key navigation and richer terminal-size/color capability detection
