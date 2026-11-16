# Personal access token

A token is **optional**, but strongly recommended.

| | Without a token | Token without repository access | Token with repository access |
|---|---|---|---|
| API requests per hour | 60 (shared with everything on your internet connection) | 5,000 | 5,000 |
| Refresh every | 6 hours at the fastest | 15 minutes at the fastest | 15 minutes at the fastest |
| Downloads, stars, forks, releases | ✅ (slowly, for a few repositories) | ✅ | ✅ |
| Star dates and who starred | ❌ | ❌ | ✅ |
| Traffic: views, clones, referrers, popular pages | ❌ | ❌ | ✅ |

**Repository access** means a classic token with the **`public_repo`** scope, or a fine-grained token with your
repositories and **Administration: read-only**.

> **Why star dates need repository access:** since July 2026 GitHub only shows *who* starred a repository (and when)
> to the repository's admins and collaborators
> ([GitHub changelog](https://github.blog/changelog/2026-06-30-upcoming-access-restrictions-to-public-api-endpoints-and-ui-views/)).
> Traffic has always worked this way.

GitHub Stats only **reads**. It never changes anything in your repositories.

## The quick way: let GitHub Stats check it

**Settings → What your token can read** checks your token as soon as you paste it, and shows:

- the kind of token (classic or fine-grained), its owner, expiry date and request limit
- the ticked scopes of a classic token
- a ✓ or ✗ for repositories and downloads, star dates, traffic and faster refreshing, each tested on one of your
  repositories
- for every ✗: step-by-step instructions and a button that opens GitHub with the right settings already filled in

## Option A: fine-grained token (recommended)

1. Open the **[pre-filled new-token page](https://github.com/settings/personal-access-tokens/new?name=GitHub%20Stats&administration=read)**.
   The name and *Administration: read-only* are already filled in.
2. **Repository access:** choose **All repositories** (or *Only select repositories* and pick yours).

   > **Can't find "Administration"?** With **Public repositories** selected, GitHub hides almost all permissions,
   > including Administration. Choose *All repositories* or *Only select repositories* and it appears.

3. **Permissions → Repository permissions → Administration:** `Read-only`.
   (*Metadata: Read-only* is added automatically.)
4. Choose an **expiration**, then **Generate token** and copy it (it starts with `github_pat_`).

Already have a fine-grained token? Open [your fine-grained tokens](https://github.com/settings/personal-access-tokens),
click it, **Edit**, and change steps 2 and 3. An edited token keeps its value, so in GitHub Stats just click **Check**.

## Option B: classic token

1. Open the **[pre-filled classic-token page](https://github.com/settings/tokens/new?scopes=public_repo&description=GitHub%20Stats)**.
   *public_repo* is already ticked.
2. Choose an **expiration**, **Generate token** and copy it (it starts with `ghp_`).

Already have a classic token without scopes? Open [your classic tokens](https://github.com/settings/tokens), click it,
tick **`public_repo`** and **Update token**. It keeps its value, so in GitHub Stats just click **Check**.

## Add it to GitHub Stats

1. **Settings → Personal access token**: paste the token. The eye button shows or hides it.
2. The check runs by itself (or click **Check**). Everything you need should have a green ✓.
3. That's it. The token is saved automatically and used from the next refresh on. **Refresh every → Auto**
   then refreshes every 30 minutes instead of every 6 hours.

## Where is it stored?

In `config.json` next to the program, on your own PC. It is only ever sent to `api.github.com`.
Backups made with **Settings → Backup** include `config.json`, so keep backup files private.

Prefer not to store it in a file? Set the environment variable **`GITHUB_TOKEN`** instead and leave the field
empty. GitHub Stats uses it and never writes it to `config.json`.

## Token expired?

The check shows *GitHub doesn't accept this token*, and the status bar and Overview show an error such as
*Bad credentials*. Create a new token, paste it, and it is checked by itself.
