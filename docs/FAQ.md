# FAQ

**Windows says "Windows protected your PC" when I run the installer.**
The program is new and not code-signed, so SmartScreen doesn't know it yet. Click **More info → Run anyway**.
The source code is all in this repository if you want to check or build it yourself.

**Why do my download numbers differ from what I expected?**
GitHub only counts files attached to releases, not the automatic *Source code (zip / tar.gz)* archives.
And it counts every download, including bots, CI runs and mirrors.

**Why can't I refresh whenever I want?**
To be gentle with GitHub's API (and your rate limit), automatic refreshes happen at most every 6 hours, and
**↻ Refresh now** at most once an hour. The numbers on GitHub don't change that fast anyway.

**The Stars page is empty.**
Star dates need a token. Add one in Settings (see the [token guide](TOKEN.md)). The list fills after the next complete refresh.

**Traffic says the token has no access.**
GitHub only gives traffic data to people with push access. Use a fine-grained token with
*Administration: read-only*, or a classic token with the `public_repo` scope. Click **Test** in Settings to check.

**I hid a repository. Is its data gone?**
No. Hidden repositories are only left out of what you see and export. Show it again and its full history is back.

**Does it work with private repositories or organisations?**
No. GitHub Stats shows the **public** repositories owned by one user.

**Can I run it from a USB stick?**
Yes. Use the portable zip. Settings and data travel with it.

**Will updating overwrite my statistics?**
No. The installer only replaces `GitHubStats.exe`. `config.json`, `data` and `share` are never touched.

**Does it send my data anywhere?**
Only requests to `api.github.com`. There is no telemetry and no account.

**Something went wrong. Where do I look?**
`data\log.txt` next to the program (**Settings → Program → Data folder**). Please attach it when you
[open an issue](https://github.com/ericbruggema/GitHubStats/issues). Check that it doesn't contain anything you
consider private first.

**How do I start completely fresh?**
Close GitHub Stats and delete `config.json` and the `data` folder next to the program. Make a backup first if in doubt.
