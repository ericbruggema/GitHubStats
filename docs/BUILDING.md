# Building from source

## Requirements

- Windows 10 or 11 (64-bit)
- [.NET SDK](https://dotnet.microsoft.com/download) **8 or newer**. With SDK 11 the build targets .NET 10,
  because a .NET 11 WPF build currently crashes at start.
- Internet for the first build (NuGet runtime packs)
- Optional, for the installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php)

## Build the program

```bat
build.bat
```

The result is `dist\GitHubStats.exe`: one self-contained file (about 70 MB) that runs on any 64-bit Windows 10/11
without .NET installed.

```bat
build.bat small
```

This makes a much smaller exe that needs the matching **.NET Desktop Runtime** on the PC that runs it.

## Build the installer

After `build.bat`:

```bat
"%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer.iss
```

The result is `installer\GitHubStats-x.y.z-Setup.exe`. The installer:

- installs per user (no admin rights), by default into `%LocalAppData%\Programs\GitHubStats`
- detects an existing installation (the *Start with Windows* registry value, or a `Desktop\GitHubStats\dist` folder)
  and suggests that folder, so updates land next to your data
- ships **only** `GitHubStats.exe`, so `config.json`, `data\` and `share\` are never overwritten
- leaves your settings and data in place when uninstalled

## Project layout

```
Core\      the data engine: GitHub API client, refresh cycle, history, charts, exports (no UI)
UI\        the Windows program (WPF, all UI in code, no XAML files)
docs\      guides and screenshots
installer.iss   Inno Setup script
build.bat       build script
```

## Releasing

1. Bump `<Version>` in `UI\GitHubStats.csproj` and `AppVer` in `installer.iss`.
2. `build.bat`, then compile `installer.iss`, and zip `dist\GitHubStats.exe` + `README.txt` as
   `GitHubStats-x.y.z-portable.zip`.
3. Create a GitHub release tagged `vX.Y.Z` with both files attached. The built-in update check looks for the
   asset ending in `Setup.exe`.
