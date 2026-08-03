# CI/CD Setup Guide

Automates the process in `Deployment_Guide.md`: every push to `main` builds the app, applies pending
EF Core migrations, and deploys to IIS on the Windows Server (10.69.1.5), fully automatically — no
approval step. The workflow lives at `.github/workflows/deploy.yml`.

**There is no manual gate before production changes take effect once this is set up.** The safety net
is code review before merging to `main`, not a deploy-time checkpoint — treat merges to `main`
accordingly.

Because BBF has no public IP (CGNAT) and only Cloudflare Tunnel exposes the app itself, GitHub's
cloud-hosted runners can't reach into the network to deploy. The workflow instead uses a **self-hosted
runner installed directly on the Windows Server** — it makes an outbound connection to GitHub, so no
inbound port needs to be opened.

This setup only needs to be done once. I can't perform these steps myself (no access to 10.69.1.5 from
where I run) — do them on the server.

---

## Step 1: Register the self-hosted runner on the Windows Server

On GitHub: go to the `taystop/BBF` repo → **Settings → Actions → Runners → New self-hosted runner**,
choose **Windows**, and follow the generated commands — they'll look like this (run in PowerShell on
10.69.1.5, as an administrator):

```powershell
mkdir C:\actions-runner ; cd C:\actions-runner

Invoke-WebRequest -Uri https://github.com/actions/runner/releases/download/v<version>/actions-runner-win-x64-<version>.zip -OutFile actions-runner.zip

# Extract with tar, not Expand-Archive — on this server Expand-Archive silently dropped several files
# (svc.cmd, config.sh, run.sh, svc.sh) with no error. tar (built into Windows Server 2022) doesn't have
# that problem. Confirm afterward with: Test-Path .\svc.cmd
tar -xf actions-runner.zip

./config.cmd --url https://github.com/taystop/BBF --token <TOKEN_FROM_GITHUB_PAGE>
```

When prompted for runner labels, add `bbf-server` (the workflow targets this label specifically, in
addition to the default `self-hosted` and `Windows` labels).

When `config.cmd` asks whether to run as a service, say yes — this installs and starts the Windows
service for you, so there's no separate `svc.cmd install` step needed.

**Service account:** when prompted for the service account, do not accept the `NT AUTHORITY\NETWORK
SERVICE` default — it lacks the rights to manage IIS and write to the site's physical path. On a
plain member server, `NT AUTHORITY\SYSTEM` works. **If the server is a domain controller** (ours is),
built-in machine accounts can't be added to local groups and `SYSTEM` will fail with "member has the
wrong account type" — use a domain account instead (`DOMAIN\Administrator` or a dedicated domain
service account with local admin rights on that box). Find your domain name with `whoami` or
`Get-ADDomain`.

**.NET SDK:** the workflow uses `actions/setup-dotnet` to install the .NET 10 SDK automatically on
the runner if it isn't already present, so you don't need to pre-install it — just note the first
run will take longer while it downloads.

---

## Step 2: Add the production connection string as a GitHub secret

The workflow needs the production database connection string to run `dotnet ef database update`
against the real database. This is separate from the app's own runtime secrets (see below) — the CI
job needs its own copy to pass to the EF CLI, since it runs before the new build's `appsettings.Production.json`
(untouched on the server) even comes into play.

In the repo: **Settings → Secrets and variables → Actions → New repository secret**

- Name: `PROD_DB_CONNECTION`
- Value: the same connection string that's in `D:\Website\BBF\appsettings.Production.json` on the
  server (e.g. `Server=RackServer;Database=BBF;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`)

**Where the app's own runtime secrets actually live:** all of them — DB connection string, Home
Assistant token, Plaid credentials, AMP/Emby credentials, document storage path — live in a single
file, `appsettings.Production.json`, edited directly on the server and never touched by a deploy (see
`Deployment_Guide.md` Step 3). **This was discovered the hard way**: the first live pipeline run didn't
exclude that file from its `robocopy /MIR`, wiped every production secret, and took the site down. The
workflow now excludes it explicitly — if you ever add a new settings file that holds secrets, add it
to the `/XF` list in `.github/workflows/deploy.yml` too, or the same thing happens again.

---

## Step 3: Verify

Push a small, low-risk change to `main` and watch **Actions** tab on GitHub:

1. `build` job runs on GitHub's hosted runner — confirms the app compiles.
2. `deploy` job runs on the self-hosted runner — publishes, applies migrations, stops the IIS site
   (`bigboisfederation` — not "BBF", despite what Step 4 of `Deployment_Guide.md` originally planned),
   copies files to `D:\Website\BBF` (not `C:\inetpub\BBF`), restarts the site, then hits
   `http://localhost:5000` as a smoke test.

If the `deploy` job never picks up, double check the runner is running (`Get-Service actions.runner.*`
on the server) and that its labels include `bbf-server`. If the stop/start-site steps fail with
"Cannot find path IIS:\Sites\...", the site name or path has drifted from what's in
`.github/workflows/deploy.yml` — run `Get-Website | Select-Object Name, PhysicalPath` on the server
and update the workflow (and this doc) to match.

---

## What this replaces vs. keeps

- Replaces the manual "Updating the Deployment" steps in `Deployment_Guide.md` (publish → robocopy →
  restart IIS) — those still work as a fallback if the runner is ever down.
- Keeps everything else in `Deployment_Guide.md` as-is: initial IIS site setup, Cloudflare Tunnel
  config, and `appsettings.Production.json` secrets are untouched by this pipeline.
- Migrations now apply automatically on every push to `main` (per your call) — there's no separate
  manual migration step anymore. If a migration ever needs to be reverted, that's still a manual
  `dotnet ef database update <PreviousMigration>` run by hand.
