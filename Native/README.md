# Native Application

See the [repository README](../README.md) for the authoritative installation, portable use, data storage, building, release packaging, and verification guide. Commands in this file assume the `Native` folder unless stated otherwise.

Native C# / WinUI 3 replacement for the earlier Python interface. Direct executable launch; no Python runtime required.

The searchable icon board uses real Windows icons and persistent labels. The desktop-map view reads Explorer's actual positions and supports zoom. Category filtering, keyboard focus, tooltips, and item inspection are supported. Shortcut health compares the bounded saved name table with both desktop folders and checks local shortcut targets. Valid Unicode names are preserved; binary layout records are never interpreted as filenames.

Normal mode stores snapshots in `%LOCALAPPDATA%\DesktopLayoutManager\backups`; portable mode uses `Data\backups` beside the executable. Legacy snapshots beside the executable in `backups` are also discovered. Each snapshot has Restore and Delete actions; deletion requires confirmation and moves the file to the Recycle Bin. Restoring layouts or changing arrangement saves a recovery snapshot first and requires confirmation because Explorer restarts. Offline registry inspection uses read-only `RegLoadAppKey` access; the selected path is captured on the UI thread before reading the file. Shortcut replacement backs up the existing link.

Build: `dotnet build Native.csproj -c Release -p:Platform=x64 -r win-x64`

Publish: `dotnet publish Native.csproj -c Release -p:Platform=x64 -r win-x64 -p:PublishTrimmed=false -p:WindowsAppSDKSelfContained=true`

Backend verification: `dotnet run --project ..\NativeChecks\NativeChecks.csproj -c Release`

Launch with `--verify` to capture the desktop map in light/dark modes, shortcut health, snapshots, and a compact window. Evidence is written to the active data directory. Verification does not restore layouts or repair shortcuts. Add `--verify-hive "<offline NTUSER.DAT path>"` to include read-only offline inspection; otherwise that check is skipped.
