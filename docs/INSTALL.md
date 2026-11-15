# Installation

GitHub Stats runs on **Windows 10 and 11 (64-bit)**. It is fully self-contained, so you don't need to install .NET.

There are two ways to use it. Pick one:

| | Installer | Portable |
|---|---|---|
| File | `GitHubStats-x.y.z-Setup.exe` | `GitHubStats-x.y.z-portable.zip` |
| Start-menu shortcut | ✅ | ❌ |
| Uninstall via Windows Settings | ✅ | delete the folder |
| Admin rights needed | ❌ (installs for your user) | ❌ |
| Settings and data | next to the program | next to the program |

Both are downloaded from the [latest release](https://github.com/ericbruggema/GitHubStats/releases/latest).

## Installer

1. Download `GitHubStats-x.y.z-Setup.exe`.
2. Run it. Windows SmartScreen may say *"Windows protected your PC"* because the program is new and not
   code-signed. Click **More info → Run anyway**.
3. Choose a folder. The default is `%LocalAppData%\Programs\GitHubStats`. If you already use GitHub Stats,
   the installer finds your existing folder and suggests that one.
4. Optionally tick **Create a desktop shortcut**, then **Install**.
5. Leave **Start GitHub Stats** ticked and click **Finish**.

## Portable

1. Download `GitHubStats-x.y.z-portable.zip`.
2. Unzip it to any folder you can write to, such as `D:\Tools\GitHubStats` or a USB stick.
   *Not* `C:\Program Files`, because the program saves its settings next to itself.
3. Run `GitHubStats.exe`.

## First start

On the first start GitHub Stats creates `config.json` and a `data` folder next to the program. Then:

1. Open **Settings** and enter your **GitHub user**.
2. Paste a **personal access token** (optional, recommended) and click **Test**. See the [token guide](TOKEN.md).
3. The first refresh starts by itself. Follow it in the status bar at the bottom.

## Updating

GitHub Stats checks for a new version once a day (**Settings → Program → Updates**). When one is available,
a **⬇ Version x.y.z available** link appears in the status bar.

- **Installer:** download and run the new `Setup.exe`. It replaces only `GitHubStats.exe`.
  Your `config.json`, `data` folder and publish folder are **never overwritten**. If the program is running,
  the installer offers to close it first.
- **Portable:** close GitHub Stats, replace `GitHubStats.exe` with the new one, start it again.

Want to be extra safe? Make a backup first: **Settings → Backup → Make backup...**

## Where is my data?

Everything lives in the program folder:

```
GitHubStats.exe
config.json          settings (user, token, options)
data\
  stats.json         repositories, releases, downloads, history
  stars.json         who starred what and when
  traffic.json       views, clones, referrers (kept beyond GitHub's 14 days)
  ui.json            window position, theme, tray and widget options
  log.txt            what happened (handy when something goes wrong)
share\               publish folder: stats.md, stats.html, CSV files and badges
```

**Settings → Program** has buttons that open the program and data folders.

## Uninstalling

- **Installer:** Windows **Settings → Apps → Installed apps → GitHub Stats → Uninstall**.
  The program is removed. `config.json`, `data` and `share` are deliberately **kept**, so delete the folder
  yourself if you want everything gone.
- **Portable:** delete the folder.

If you enabled **Start with Windows**, switch it off in Settings first, or remove the `GitHubStats` entry
from **Task Manager → Startup apps**.
