# Changelog

## 2.1.1 (2026-11-15)

### Changed
- The tray hover card now appears after **2 seconds** on the icon, so a right-click for the menu is no longer
  covered by it. After a click or opening the menu, the card stays away until the mouse leaves the icon.

## 2.1.0 (2026-11-15)

### New
- **About page**: the story behind GitHub Stats, links (GitHub, LinkedIn, Facebook, feedback) and credits.
- **Redesigned Settings** in the Windows 11 style: grouped cards, toggle switches, segmented choices,
  and everything **saves automatically**.
- **Token test**: shows the request limit and whether star dates and traffic work.
- **↻ Refresh now** in the status bar (1 hour after the previous refresh), with a live countdown to the next automatic refresh.
- **Hide repositories** from every total, chart, widget and export. Their data is kept.
- **Download milestones editor** (add/remove; `2k`-style input).
- **Backup and restore**: one zip with settings and all history; restore saves the current data first.
- **Accent colours**: blue, purple, green, orange, pink, teal.
- **Search box** above the repositories table.
- **Sidebar icons** and a highlighted current page.
- **Update check** once a day, or on demand, with a download link in the status bar.
- **Installer** (Inno Setup): per user, no admin rights, never overwrites settings or data.
- Modern thin **scrollbars** and a flat **slider**.

### Fixed
- The repositories table's scrollbar now lines up with the detail pane's scrollbar.
- Builds with the .NET 11 SDK target .NET 10 (a .NET 11 WPF build crashed at start).

## 2.0.0

- Native Windows program (WPF): no browser, no web server.
- Downloads per repository and release, star dates, traffic history, charts with hover values.
- Tray icon with hover card and notifications, desktop widget, taskbar badge, fireworks for new stars.
- Export to CSV, JSON, web page, README block and badges.
