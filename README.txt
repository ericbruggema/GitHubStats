GitHub Stats 2.0 - a native Windows program (WPF), no browser, no web server.

BUILD
  Double-click build.bat (needs the .NET SDK 8 or newer, and internet for the first build).
  Result: dist\GitHubStats.exe - one portable file. Copy it anywhere and start it.

FIRST START
  It creates config.json and a data folder next to the .exe.
  Open "Settings" in the program: GitHub user, optional token, tray icon, widget, theme, start with Windows.

WHAT IT DOES
  - Downloads per repository / per release, stars (with real dates and names), forks, traffic history
  - Charts drawn natively; hover for exact values
  - Refreshes from GitHub at most 4 times a day (every 6 hours at the earliest)
  - Optional: tray icon with notifications, taskbar badge/progress, small desktop widget
  - Export: CSV, JSON, web page, README block, badges, publish folder
  - "Refresh now" in the status bar (1 hour after the previous refresh) with a live countdown
  - Settings: token test, hidden repositories, download milestones, accent colour,
    backup / restore (one zip), update check, all saved automatically
  - Search box above the repositories table

FILES
  Core\  the data engine (GitHub API, history, exports)       UI\  the Windows program
