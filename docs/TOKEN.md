# Personal access token

A token is **optional**, but strongly recommended.

| | Without a token | With a token |
|---|---|---|
| API requests per hour | 60 (shared with everything on your internet connection) | 5,000 |
| Downloads, stars, forks, releases | ✅ (slowly, for a few repositories) | ✅ |
| Real star dates and who starred | ❌ | ✅ |
| Traffic: views, clones, referrers, popular pages | ❌ | ✅ *with push access* |

GitHub Stats only **reads**. It never changes anything in your repositories.

## Option A: classic token, simplest (no traffic)

Good for downloads, stars and star dates.

1. Go to **[github.com/settings/tokens](https://github.com/settings/tokens)** → **Generate new token → Generate new token (classic)**.
2. **Note:** `GitHub Stats`. **Expiration:** your choice (*No expiration* means you never have to replace it).
3. Tick **no scopes at all**.
4. **Generate token** and copy it (it starts with `ghp_`).

Want traffic too? Tick the **`public_repo`** scope in step 3 instead.

## Option B: fine-grained token, recommended for traffic

GitHub only shares traffic data with people who have push access to a repository.
A fine-grained token can get read-only access to exactly that.

1. Go to **[github.com/settings/personal-access-tokens/new](https://github.com/settings/personal-access-tokens/new)**.
2. **Token name:** `GitHub Stats`. **Expiration:** your choice.
3. **Repository access:** *All repositories* (or *Only select repositories*).
4. **Permissions → Repository permissions → Administration:** `Read-only`.
   (*Metadata: Read-only* is added automatically.)
5. **Generate token** and copy it (it starts with `github_pat_`).

## Add it to GitHub Stats

1. **Settings → Personal access token**: paste the token. The 👁 button shows or hides it.
2. Click **Test**. You'll see something like:

   > ✓ Token works: 5,000 requests per hour  
   > ✓ Downloads, stars and star dates.  
   > ✓ Traffic (views, clones, referrers).

   A red ✗ next to *Traffic* means the token has no push access. Use option B, or tick `public_repo`.
3. That's it. The setting is saved automatically and used from the next refresh on.

## Where is it stored?

In `config.json` next to the program, on your own PC. It is only ever sent to `api.github.com`.
Backups made with **Settings → Backup** include `config.json`, so keep backup files private.

Prefer not to store it in a file? Set the environment variable **`GITHUB_TOKEN`** instead and leave the field
empty. GitHub Stats uses it and never writes it to `config.json`.

## Token expired?

The status bar and Overview show an error such as *Bad credentials*. Create a new token, paste it, and click **Test**.
