<p align="center">
  <img src="Native/Assets/AppIcon.png" alt="Desktop Layout Manager icon" width="112" />
</p>

# Desktop Layout Manager

A native Windows utility for viewing desktop icons, saving Explorer's desktop layout registry values, checking missing shortcuts, and inspecting saved layout data in offline Windows profile hives.

Built with **C# and WinUI 3**. No Python, script launcher, or browser-based interface.

## Project Status

Version **1.0.0** is available as a Windows x64 installer and portable ZIP, alongside the source, backend checks, artwork, and packaging scripts.

## Downloads

- [Windows x64 installer](https://github.com/NightVibes33/Windows-Desktop-ICON-Manager/releases/download/v1.0.0/DesktopLayoutManager-1.0.0-Setup-x64.exe)
- [Windows x64 portable ZIP](https://github.com/NightVibes33/Windows-Desktop-ICON-Manager/releases/download/v1.0.0/DesktopLayoutManager-1.0.0-Portable-x64.zip)
- [SHA-256 checksums](https://github.com/NightVibes33/Windows-Desktop-ICON-Manager/releases/download/v1.0.0/SHA256SUMS.txt)
- [Release notes](https://github.com/NightVibes33/Windows-Desktop-ICON-Manager/releases/tag/v1.0.0)

## Contents

- [Features](#features)
- [Downloads](#downloads)
- [Requirements](#requirements)
- [Installation](#installation)
- [Using the App](#using-the-app)
- [Recover an Older NTUSER.DAT With ShadowExplorer](#recover-an-older-ntuserdat-with-shadowexplorer)
- [Data and Backups](#data-and-backups)
- [Building](#building)
- [Release Packaging](#release-packaging)
- [Verification](#verification)
- [Troubleshooting](#troubleshooting)
- [Limitations](#limitations)
- [Repository Structure](#repository-structure)
- [Contributing](#contributing)
- [License](#license)

## Features

| Area | Functionality |
| --- | --- |
| Desktop layout | Reads visible Explorer items and retrieves Windows Shell icons where their paths can be resolved; includes labels, search, and category filters. |
| Desktop map | Current Explorer icon positions, labels, and zoom controls. |
| Shortcut health | Compares saved layout names with the user/public desktop folders and checks fully qualified `.lnk` targets for file or folder existence. |
| Snapshots | JSON layout backups, restoration, and confirmed deletion to the Recycle Bin. |
| Offline recovery | Inspects desktop layout information in an offline `NTUSER.DAT` and can apply it to the current profile. |
| Arrangement | Shows the saved auto-arrange flag and offers to disable it, enable alignment, and remove the saved sorting value. |
| Appearance | Native window chrome, custom application icon, and system, light, and dark themes. |

## Requirements

- Windows with Windows Explorer. The project declares build 17763 as its minimum and targets the Windows 10 SDK, but that is not a tested Windows support matrix. Older Windows versions have not been validated.
- Use the x64 build. The live desktop reader uses a 64-bit Explorer structure layout; x86 and ARM64 publish profiles are present but are not verified implementations.
- Desktop icons must be visible for the live board and map. Hidden icons or unavailable Explorer cause that view to report an error.
- A writable location for backups and diagnostic output.

The published packages include .NET and Windows App SDK runtime files. A separately installed SDK is not needed to run them. Clean-machine and older-Windows compatibility still need testing. Building requires the development tools below; Python is not used.

## Installation

Download a package from [Downloads](#downloads). Choose the installer for a normal per-user installation or the portable ZIP to run from a writable folder without installing. Developers can use [Building](#building).

| Artifact | Purpose |
| --- | --- |
| `DesktopLayoutManager-1.0.0-Setup-x64.exe` | Per-user setup with Start menu shortcuts and an uninstaller. |
| `DesktopLayoutManager-1.0.0-Portable-x64.zip` | A complete application folder to extract and run. |
| `SHA256SUMS.txt` | Checksums for both downloads. |

### Installer

1. Run the setup executable.
2. Choose a folder; the default is `%LOCALAPPDATA%\Programs\DesktopLayoutManager`.
3. Optionally select the desktop shortcut.
4. Launch **Desktop Layout Manager** from the Start menu or installation folder.

Setup installs for the current user without requesting administrator rights by default. It creates Start menu and uninstall shortcuts, plus an optional desktop shortcut. It contains no instruction to delete the separate application data directory on uninstall. Upgrade behavior has not yet been tested.

### Portable

1. Extract the entire ZIP to a writable folder.
2. Run `DesktopLayoutManager.exe`.
3. Keep `portable.flag`, all accompanying libraries, and `Assets` together with the executable.

The portable ZIP contains a complete application folder, not a single-file EXE. Its `portable.flag` selects a `Data` folder beside the executable. Move the whole folder to retain your data. Do not run directly inside the ZIP.

To update, close the app, back up `Data`, and replace the application files with the newer package while preserving `Data` and `portable.flag`.

## Using the App

### Desktop Layout

Browse the labeled icon board, search for an item, or filter by category. Switch to the map to inspect current Explorer positions. Refresh after rearranging icons in Windows.

The map is a desktop viewer, not a drag-and-drop layout editor or replacement desktop.

### Snapshots

Use **Save snapshot** before changing arrangement or display configuration. Open **Snapshots** to review, restore, or delete saved layouts.

Restoration requires confirmation, saves a recovery snapshot first, and restarts Explorer. Open File Explorer windows may close. Check the real desktop and refresh the map after restoration to verify the result.

Deletion requires confirmation and moves the snapshot to the Recycle Bin. A layout snapshot does not back up desktop files or installed applications.

### Shortcut Health

Review missing entries and local shortcut targets. A disconnected drive or moved application may appear as a broken target; check the displayed location before repairing it.

The app searches the user and common Start menus for a usable source with the same filename. When found, the row offers **Restore**; otherwise **Choose source** accepts an `.exe`, `.lnk`, or `.url`. After confirmation, it copies a supplied shortcut or creates a shortcut to the supplied executable. An existing destination file is backed up first. This does not recover deleted file contents or download/reinstall missing programs.

### Offline Recovery

1. Obtain a readable offline copy of your profile's `NTUSER.DAT` from a backup. If you do not have one, see [ShadowExplorer recovery](#recover-an-older-ntuserdat-with-shadowexplorer) below.
2. Select it in **Offline recovery**.
3. Click **Inspect** and review the saved layout details.
4. Only confirm restoration when you intend to apply that layout to your current desktop.

Inspection opens the hive read-only and reports saved item names and registry value count. It does not preview that hive's saved icon positions on the map. Restoration writes saved values from `Software\Microsoft\Windows\Shell\Bags\1\Desktop` to that key in the **current Windows profile**, not the selected source hive. It does not recover the whole profile, documents, or software. Avoid actively loaded profile hives and preserve an untouched source copy.

## Recover an Older NTUSER.DAT With ShadowExplorer

If your icons were rearranged before you saved an app snapshot, an older Windows shadow copy may contain your previous layout in `NTUSER.DAT`. ShadowExplorer is a separate third-party tool; it is not bundled with Desktop Layout Manager. It can browse existing Windows shadow copies and export files from them. See the [official overview](https://www.shadowexplorer.com/).

### Export the Older Hive

1. Download ShadowExplorer from its [official downloads page](https://www.shadowexplorer.com/downloads.html), then install or extract it.
2. Open ShadowExplorer. If access is denied, try **Run as administrator**.
3. Select the drive containing your Windows user profile, usually **C:**.
4. Select a shadow-copy date from **before** the icon layout changed.
5. Browse to `Users\<your-profile-folder>` inside that copy and find `NTUSER.DAT`. This is the file directly inside your profile folder, not a file in its Desktop or AppData folders. The folder name may differ from your Windows display name.
6. Right-click `NTUSER.DAT`, choose **Export**, and select a separate destination such as `Documents\DesktopLayoutRecovery\2026-10-01`. Use a different destination folder for each date you export.

The date-selection and export workflow is described in the [official ShadowExplorer manual](https://www.shadowexplorer.com/documentation/manual.html).

**Never export over the live `C:\Users\<your-profile-folder>\NTUSER.DAT`.** Do not replace your current profile hive or import it into Registry Editor. Export a separate copy for this app to inspect. Profile hives can contain sensitive information; do not upload them to this repository or public issue reports.

### Inspect It in Desktop Layout Manager

1. Use **Save snapshot** to preserve your current saved layout.
2. Open **Offline recovery > Browse** and select the exported `NTUSER.DAT`.
3. Click **Inspect**. Check the reported saved item names and registry value count; inspection alone does not change your desktop.
4. If the file contains the layout you want, select **Restore layout** and read the confirmation before continuing. Explorer restarts and open folder windows may close.
5. Check your actual desktop afterward and refresh the map. If necessary, use **Snapshots** to restore the pre-change snapshot.

The app reads only the desktop layout key from that hive. It does not restore the entire profile. The export date does not guarantee Explorer had saved the exact arrangement you remember; another available date may be more useful. Restoration remains an implemented but not end-to-end-validated recovery operation.

### If No Older Copies Are Available

A useful shadow copy must already exist from before the change. Enabling System Protection now cannot recreate an earlier file. ShadowExplorer's [official FAQ](https://www.shadowexplorer.com/documentation/faq.html) explains this prerequisite.

If the date list is empty or your profile is absent, check that you selected the correct drive. If no suitable copy remains, use an existing profile backup or an app snapshot instead. Do not delete restore points or shadow copies while searching. Shadow copies are not a substitute for separate backups.

## Data and Backups

| Data | Installed / normal mode | Portable mode |
| --- | --- | --- |
| Data root | `%LOCALAPPDATA%\DesktopLayoutManager` | `<application folder>\Data` |
| Snapshots | `<data root>\backups\*.json` | `<data root>\backups\*.json` |
| Shortcut backups | `<data root>\shortcut-backups` | `<data root>\shortcut-backups` |
| Diagnostics and verification output | Data root | Data root |

Legacy JSON snapshots in a `backups` folder beside the executable are also discovered. To transfer snapshots, close the app and copy the JSON files into the destination mode's `backups` folder. Keep the originals until the copies load successfully.

Snapshots can reveal desktop item names. Diagnostics and screenshots can reveal local paths and desktop contents. Review and redact them before posting public issues. Personal backups, diagnostic files, and generated builds are excluded by `.gitignore` and should not be included in releases.

## Building

Development requires Windows, the **.NET 10 SDK**, and Windows SDK / WinUI 3 build tooling. Initial NuGet restore requires internet access. The project currently uses floating `1.*` Windows App SDK and `10.*` SDK BuildTools package versions, so dependency resolution is not pinned for reproducible builds.

Run from the repository root:

```powershell
dotnet restore .\Native\Native.csproj
dotnet build .\Native\Native.csproj -c Release -p:Platform=x64 -r win-x64
```

Publish a self-contained application folder:

```powershell
dotnet publish .\Native\Native.csproj -c Release -p:Platform=x64 -r win-x64 -p:PublishTrimmed=false -p:WindowsAppSDKSelfContained=true -o .\artifacts\payload
```

Run `artifacts\payload\DesktopLayoutManager.exe` with all published files present. This is an unpackaged WinUI 3 application; release packaging uses a traditional installer rather than MSIX.

## Release Packaging

Install **Inno Setup 6**, then run from the repository root:

```powershell
.\Packaging\Build-Release.ps1 -Version 1.0.0
```

If compiler discovery fails, specify its actual path:

```powershell
.\Packaging\Build-Release.ps1 -Version 1.0.0 -InnoCompiler "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

The script is written to publish the native application, include this README, add `portable.flag` to the portable copy, compile setup, and create checksums. A successful run should produce:

```text
dist/
  DesktopLayoutManager-1.0.0-Setup-x64.exe
  DesktopLayoutManager-1.0.0-Portable-x64.zip
  SHA256SUMS.txt
```

Versions must use `major.minor.patch`. Application staging folders under `artifacts` are recreated by the script; do not store personal files there. The installer uses a stable application ID for subsequent installations.

Before publishing, test fresh installation, upgrade, uninstallation, and an extracted portable copy. Verify icons and data locations in both modes. Code signing is not configured.

## Verification

Run focused backend checks:

```powershell
dotnet run --project .\NativeChecks\NativeChecks.csproj -c Release
```

Checks cover layout parsing, Unicode, malformed input rejection, snapshot byte preservation, icon retrieval, and snapshot deletion. They depend on the current Windows profile having a saved `IconLayouts` value and on visible desktop icons. The check compares the live item count to the saved name count and requires almost all icons to resolve; a legitimate difference between saved and live state can fail it. This is a machine-dependent integration check, not an isolated unit-test suite.

The checks create a temporary snapshot and then move it to the Recycle Bin. They do not restore layouts or repair shortcuts.

Capture the UI using a published build:

```powershell
.\artifacts\payload\DesktopLayoutManager.exe --verify
```

Review the generated screenshots, `verification.json`, and any `verification-error.txt` in the data root. Captures cover light/dark themes, shortcut health, snapshots, offline recovery, and a compact window.

Offline inspection is skipped unless you supply your own readable offline hive fixture:

```powershell
.\artifacts\payload\DesktopLayoutManager.exe --verify --verify-hive "D:\ProfileBackup\NTUSER.DAT"
```

This tests inspection only, not restoration. Normal interactive inspection accepts your selected file.

A successful build or launched process is not proof of successful restoration. Recovery needs explicit testing on a disposable profile and inspection of the visible desktop result.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| App will not launch | Keep the complete published folder together. If application startup reached its exception handler, it may have written `startup-error.txt` in the data root; failures before that point will not produce this file. |
| "Unable to complete action" | Read the accompanying message and `operation-errors.txt`. Report the exact action and Windows version. |
| Offline hive is in use / inaccessible | Choose a readable offline copy, not an active profile's `NTUSER.DAT`. |
| Offline hive has no layout | The file may not contain the desktop registry values expected by this app. |
| Shortcut target is missing | Check drive availability and the application's actual location before repairing. |
| Snapshot is not listed | Check the active mode's data folder; installed and portable modes do not automatically share data. |
| EXE or shortcut icon is missing | Check that the shortcut targets the current executable. Windows may cache an older file's icon. |
| Windows shows a trust warning | Signing is not configured. Verify source and checksums; do not disable protection for unknown downloads. |

## Limitations

- Windows only; the release workflow targets x64.
- Snapshots record Explorer's saved registry state, which can differ from visible positions until Explorer persists changes.
- Monitor topology, resolution, scaling, and Windows changes can affect restored layouts.
- Shortcut checks do not guarantee that every application, network location, or special shell item is usable.
- Restoration and arrangement changes restart Explorer. Save work before confirming.
- Parser and snapshot tests do not establish restore compatibility across Windows versions. Test important recovery workflows on a disposable profile first.
- No cloud synchronization, automatic background backup, or whole-profile recovery is provided.

## Repository Structure

```text
Native/                 C# / WinUI 3 application
  Assets/               Application artwork and executable icon
  Views/MainPage.cs     Interface and interaction flows
  DesktopService.cs     Desktop, shortcut, snapshot, and hive operations
  Native.csproj         Application project
  UI-AUDIT.md           UI review notes
NativeChecks/           Backend verification executable
Packaging/              Release script and Inno Setup definition
.gitignore              Local data and generated-output exclusions
README.md               User and contributor guide
```

`artifacts` and `dist` are generated output, not source directories. The previous Python application is not part of this project.

## Contributing

Keep changes focused and include relevant verification. Bug reports should include Windows build, application version, installation mode, reproduction steps, and exact errors. Redact private filenames, paths, and desktop contents.

Parsing and registry changes should include backend checks. Inspect UI changes in both themes and at compact sizes. Test recovery changes on a disposable profile, checking both the backup and the visible result.

## License

No license file is currently included. Public repository visibility alone does not grant a software license. Establish licensing before redistributing or incorporating the source into another project.
