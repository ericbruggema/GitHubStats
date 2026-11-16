# Changelog

## 2.4.1 (2026-11-16)

### Changed
- **Downloads per day** chart on the Overview: one bar per day with a dashed 7-day average line, dates along the bottom
  and the exact number on hover. It sits next to a smaller "All repositories over time" chart.
- More on the first screen: smaller cards and growth in one slim line (last refresh, 24h, 7d and 30d for downloads
  and stars) instead of a large table.

## 2.4.0 (2026-11-16)

### New
- **Overview shows everything on one page**: cards for downloads, stars, views, clones, downloads per day, next
  milestone, repositories, releases, forks and open issues; a growth table (last refresh, 24 hours, 7 days, 30 days);
  the big chart; and panels for top repositories, downloads per day (heatmap), latest stars, traffic with most viewed
  repositories and referrers, latest releases, most downloaded files, platforms and highlights. The panels fill as many
  columns as the window allows.
- **Dates along every chart**: evenly spaced dates (hours, days, weeks or months, whatever fits) with light grid lines,
  instead of only the first and last date.

## 2.3.1 (2026-11-16)

### Fixed
- A new token, or new permissions on the same token (for example *public_repo* ticked on GitHub), did not refresh
  anything until the next scheduled refresh, and traffic stayed blocked for up to a day. Now a full refresh starts
  at once: when you save a new token, when **Check** finds permissions the last refresh didn't have, and at startup
  when the last refresh recorded missing access and the token now has it.

## 2.3.0 (2026-11-16)

### New
- **What your token can read** (Settings): checked as soon as you paste a token. Shows the kind of token, owner,
  expiry date, request limit and (classic) scopes, plus a ✓ or ✗ per feature, each tested on your own repositories.
  Anything missing comes with step-by-step instructions and a button that opens GitHub **pre-filled**
  (fine-grained: *Administration: read-only*; classic: *public_repo*).
- **Refresh every** is adjustable: Auto (every 30 minutes with a token, every 6 hours without), or 15 min to 24 hours.
  Shows how many API requests that costs per hour.
- **Refresh now** in Settings, the tray menu and the widget menu, too. With a token it works 5 minutes after the
  previous refresh (1 hour without).
- History stays compact with frequent refreshing: every snapshot of the last 2 days, then one per 6 hours.

### Fixed
- The main screens showed conclusions from before you added a token ("API requests left: 45 / 60", "star dates
  need a token"). They now follow the token you use, and the request counter updates as soon as the token is checked.
- Star dates stopped with a bare "Not Found": since July 2026 GitHub only lists stargazers to repository admins
  and collaborators. GitHub Stats now explains this and how to fix it (*public_repo* or a fine-grained token).

## 2.2.0 (2026-11-15)

### New: ten widget styles
Choose with right-click on the widget > **Style**, or in **Settings > Desktop widget**:

1. **Glass card**: downloads and stars with a soft area chart (the new default)
2. **Compact pill**: one line, perfect above the taskbar
3. **Milestone ring**: progress to your next download milestone, with an estimate of when you'll reach it
4. **Top repositories**: mini leaderboard with bars
5. **Star feed**: the latest stargazers (needs a token)
6. **Download heatmap**: downloads per day for the last 8 weeks, GitHub-style
7. **Big counter**: one big number that rolls up when downloads come in
8. **Trend chart**: downloads and stars on one scale, with a 7d / 30d switch
9. **Edge dock**: a slim strip on the right edge of the screen that slides away when you don't need it
10. **Pick your tiles**: choose up to 6 metrics (downloads, stars, 7-day changes, per day, views, clones, forks, ...)

Plus a **Size** option (small, normal, large) for every style.

## 2.1.2 (2026-11-15)

### Fixed
- The desktop widget no longer disappears with "Show desktop" (Win+D) or when other windows are minimised. It is now pinned to the desktop and kept out of Alt+Tab.
- The desktop widget could stretch far wider than the screen, pushing the numbers out of view.
- Right-click menus (widget and text boxes) were hard to read in dark mode. They now follow the theme: rounded, with a check mark for the current choice and a highlight on hover.

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
