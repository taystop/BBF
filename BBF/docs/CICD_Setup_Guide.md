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
Expand-Archive -Path actions-runner.zip -DestinationPath .

./config.cmd --url https://github.com/taystop/BBF --token <TOKEN_FROM_GITHUB_PAGE>
```

When prompted for runner labels, add `bbf-server` (the workflow targets this label specifically, in
addition to the default `self-hosted` and `Windows` labels).

Install it as a Windows service so it survives reboots and doesn't need a logged-in session:

```powershell
./svc install
./svc start
```

**Service account:** the runner service needs local rights to stop/start the IIS site and write to
`C:\inetpub\BBF`. Running it as `NT AUTHORITY\SYSTEM` (the default for `svc install` when run as
Administrator) covers this. If you use a dedicated service account instead, grant it IIS management
rights and the same `icacls` grants documented in `Deployment_Guide.md` Step 4.

**.NET SDK:** the workflow uses `actions/setup-dotnet` to install the .NET 10 SDK automatically on
the runner if it isn't already present, so you don't need to pre-install it — just note the first
run will take longer while it downloads.

---

## Step 2: Add the production connection string as a GitHub secret

The workflow needs the production database connection string to run `dotnet ef database update`
against the real database (production secrets currently live in `web.config` on the server, which
`appsettings.json` intentionally leaves blank — the pipeline needs its own copy to pass to the EF
CLI).

In the repo: **Settings → Secrets and variables → Actions → New repository secret**

- Name: `PROD_DB_CONNECTION`
- Value: the same connection string currently in `web.config`'s `ConnectionStrings__DefaultConnection`
  environment variable (e.g. `Server=RackServer;Database=BBF;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`)

This is the only secret the pipeline needs — everything else (Home Assistant token, Plaid secrets,
etc.) stays in `web.config` on the server exactly as it is today, since the pipeline never touches
`web.config` (it's excluded from the robocopy, same as the manual process).

---

## Step 3: Verify

Push a small, low-risk change to `main` and watch **Actions** tab on GitHub:

1. `build` job runs on GitHub's hosted runner — confirms the app compiles.
2. `deploy` job runs on the self-hosted runner — publishes, applies migrations, stops the IIS site,
   copies files, restarts the site, then hits `http://localhost:5000` as a smoke test.

If the `deploy` job never picks up, double check the runner is running (`Get-Service actions.runner.*`
on the server) and that its labels include `bbf-server`.

---

## What this replaces vs. keeps

- Replaces the manual "Updating the Deployment" steps in `Deployment_Guide.md` (publish → robocopy →
  restart IIS) — those still work as a fallback if the runner is ever down.
- Keeps everything else in `Deployment_Guide.md` as-is: initial IIS site setup, Cloudflare Tunnel
  config, and `web.config` secrets are untouched by this pipeline.
- Migrations now apply automatically on every push to `main` (per your call) — there's no separate
  manual migration step anymore. If a migration ever needs to be reverted, that's still a manual
  `dotnet ef database update <PreviousMigration>` run by hand.
