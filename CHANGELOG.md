# Changelog

## 2.5.1 (2026-11-19)

### Fixed
- **Per-release history was hardly recorded.** A measurement younger than an hour was replaced instead of added, so with
  refreshes every 30 minutes the last point kept moving forward. Release pages and the day page therefore put
  downloads of several days on one day, and "Downloads per release" did not match "Downloads per repository".
  History is now recorded once per clock hour.
- A day's figure is only shown when the day before was measured too; a change over a longer gap (computer off) is no
  longer put on one day. This applies to every per-day chart, the heatmap and the day page.
- The day page splits each repository's downloads over its releases and shows what could not be split as
  "(not split)", so both boxes always add up to the same total.
- "Last 7 days" on a release page now uses the real change since a week ago.
- The hourly chart on the day page no longer draws a meaningless 7-day average line.
- The download calendar's "Last 7 days" counted 7 calendar days (including today, which is still running) and so
  differed from the cards and top repositories; it now uses the same last 7 x 24 hours.

### Changed
- Every refresh of the last 14 days is kept (was 2), and release history hourly for 14 days, so "During the day" stays
  detailed for two weeks.

## 2.5.0 (2026-11-16)

### New
- **Every block on the Overview is clickable** and opens detailed statistics, with "Back to overview" to return:
  - **Downloads**: downloads per day since the first refresh (with 7-day average), totals over time, a table per
    repository (total, last refresh, 7 days, per day, releases), platforms and top files
  - **Stars**: stars per day from the exact GitHub dates, a table per repository and the full list of stargazers
  - **Views** and **Clones**: per day for all repositories together (with unique visitors / cloners), a table per
    repository, and for views the referrers and popular pages
  - **Milestones**: every milestone with the date it was reached, progress and an estimate for the ones still to come
  - **Releases** and **Downloaded files**: complete, sortable lists (file sizes, downloads per day since publishing)
  - **Forks** and **Open issues** per repository
- **Click further**:
  - **a day** (any bar in a per-day chart): that day's downloads compared to normal and its rank, downloads during
    the day per hour, per repository and per release, who starred, views and clones per repository, releases
    published that day, and buttons for the previous and next day
  - **a release**: downloads, publish date and age, per day since release, last 7 days, share of the repository and
    rank, downloads per day of that release, every file with platform and share, and all releases of the repository
    to compare
  - **a repository**: all its numbers (downloads, stars, forks, issues, language, created, last push, license),
    downloads per day, views and clones per day, referrers, popular pages, stargazers and releases
  - "Back" returns step by step and says where to
- Release history is now kept much longer (hourly for 2 days, then one point per day) so per-day release figures
  stay available.
- Tables are sortable by clicking a column; click a row for its details, double-click to open it on GitHub.

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
