<p align="center">
  <img src="Native/Assets/AppIcon.png" alt="Desktop Layout Manager icon" width="112" />
</p>

# Desktop Layout Manager

A native Windows utility for viewing desktop icons, saving and restoring layouts, checking broken shortcuts, and inspecting saved layouts from offline Windows profile backups.

Built with **C# and WinUI 3**. No Python, script launcher, or browser-based interface.

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Using the App](#using-the-app)
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
| Desktop layout | Real Windows icons with persistent names, search, and category filters. |
| Desktop map | Current Explorer icon positions, labels, and zoom controls. |
| Shortcut health | Checks saved desktop entries and local shortcut targets with readable missing-target details. |
| Snapshots | JSON layout backups, restoration, and confirmed deletion to the Recycle Bin. |
| Offline recovery | Inspects desktop layout information in an offline `NTUSER.DAT` and can apply it to the current profile. |
| Arrangement | Disables automatic arrangement and resets the saved sorting setting. |
| Appearance | Native window chrome, custom application icon, and system, light, and dark themes. |

## Requirements

- Windows 10 version 1809 (build 17763) or later, or Windows 11.
- The release script targets Windows x64; other architectures are not currently distributed by that script.
- Windows Explorer for live position reading and layout changes.
- A writable location for backups and diagnostic output.

Release packaging is configured to include .NET and Windows App SDK runtimes. End users do not need Python or a separately installed .NET SDK. Building from source requires the development tools below.

## Installation

The release script creates these artifacts. Filenames describe build outputs, not an already-published release.

| Artifact | Purpose |
| --- | --- |
| `DesktopLayoutManager-<version>-Setup-x64.exe` | Per-user installation with Start menu shortcuts and an uninstaller. |
| `DesktopLayoutManager-<version>-Portable-x64.zip` | Extract and run without installing. |
| `SHA256SUMS.txt` | SHA-256 checksums for both packages. |

### Installer

1. Run the setup executable.
2. Choose a folder; the default is `%LOCALAPPDATA%\Programs\DesktopLayoutManager`.
3. Optionally select the desktop shortcut.
4. Launch **Desktop Layout Manager** from the Start menu or installation folder.

Installation is per-user and does not request administrator rights by default. Uninstall through Windows **Settings > Apps**, or the Start menu uninstall shortcut. The installer does not intentionally remove application data stored outside the installation folder.

### Portable

1. Extract the entire ZIP to a writable folder.
2. Run `DesktopLayoutManager.exe`.
3. Keep `portable.flag`, all accompanying libraries, and `Assets` together with the executable.

This is a complete application folder, not a single-file EXE. The included `portable.flag` selects a `Data` folder beside the executable. Move the whole folder to retain your data. Do not run directly inside the ZIP.

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

Where a replacement is available, review it before applying the repair. The existing shortcut is backed up before replacement. The app does not download or reinstall missing programs.

### Offline Recovery

1. Obtain a readable offline copy of your profile's `NTUSER.DAT` from a backup.
2. Select it in **Offline recovery**.
3. Click **Inspect** and review the saved layout details.
4. Only confirm restoration when you intend to apply that layout to your current desktop.

Inspection opens the hive read-only. Restoration writes desktop layout values to the **current Windows profile**, not the selected source hive. It does not recover the whole profile, documents, or software. Avoid actively loaded profile hives and preserve an untouched source copy.

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

Development requires Windows, the **.NET 10 SDK**, and Windows SDK / WinUI 3 build tooling. Visual Studio with WinUI application development tools is a suitable environment. Initial NuGet restore requires internet access.

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

The script publishes the native application, includes this README, adds `portable.flag` to the portable copy, compiles setup, and creates checksums:

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

Checks cover layout parsing, Unicode, malformed input rejection, snapshot byte preservation, icon retrieval, and snapshot deletion. They read the current desktop and create a temporary snapshot that is then moved to the Recycle Bin. They do not restore layouts or repair shortcuts.

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
| App will not launch | Extract or install the complete package. Check `startup-error.txt` in the data root. |
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
