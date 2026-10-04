# BBF Deployment Guide

Deploy BBF to Windows Server 2022 (10.69.1.5) via IIS + Cloudflare Tunnel.

As of the Phase 5 rewrite, there are **two** separate IIS sites to deploy: `BBF.Web` (the Blazor
Server site everyone browses to) and `BBF.Api` (the backend every page now calls instead of touching
the database or LAN services directly — also what `BBF.Mobile` will talk to once that exists). They
run side by side on the same box and share the same SQL Server database.

**Actual production values for BBF.Web (confirmed 2026-08-03 — this doc originally specified
different ones, which broke the first CI/CD deploy run; if you change these on the server, update
this doc too):**
- IIS site name: `bigboisfederation` (not `BBF`)
- Physical path: `D:\Website\BBF` (not `C:\inetpub\BBF`)
- App pool name: unconfirmed — verify with `Get-IISAppPool` before trusting the "BBF" references below

**Planned values for BBF.Api (not yet deployed — these are what `.github/workflows/deploy.yml` and
`BBF`'s `Api:BaseUrl` config already assume; confirm and correct this note the same way the BBF.Web
one above was corrected, the first time you actually set this site up):**
- IIS site name: `bbf-api`
- Physical path: `D:\Website\BBF.Api`
- Port: `5001` (BBF.Web reaches it over `http://localhost:5001` — same box, no Cloudflare hop)
- App pool name: `BBF.Api`

## Prerequisites

- Windows Server 2022 with IIS enabled
- .NET 10 ASP.NET Core Hosting Bundle installed on the server
- Cloudflare account with bigboisfederation.com managed
- SQL Server running on the server

---

## Step 1: Install ASP.NET Core Hosting Bundle (if not already)

Download and install on the server:
- https://dotnet.microsoft.com/download/dotnet/10.0 → ASP.NET Core Hosting Bundle

Restart IIS after installing:
```powershell
net stop was /y
net start w3svc
```

---

## Step 2: Deploy Application Files

Copy the published files to the server — once for BBF.Web, once for BBF.Api:

```powershell
# On the server, create the deployment folders
mkdir D:\Website\BBF
mkdir D:\Website\BBF.Api

# Create document storage and log folders
mkdir C:\BBFData\Documents
mkdir D:\Website\BBF\logs
mkdir D:\Website\BBF.Api\logs
```

Copy everything from each project's `publish\` output to its own folder on the server, **excluding
`web.config` and `appsettings.Production.json`** — see Step 3 before running this for the first time.

```powershell
robocopy "F:\Coding\BBF\publish-web" "D:\Website\BBF" /MIR /XF web.config appsettings.Development.json appsettings.Production.json
robocopy "F:\Coding\BBF\publish-api" "D:\Website\BBF.Api" /MIR /XF web.config appsettings.Development.json appsettings.Production.json
```

---

## Step 3: Configure Production Secrets

**The real mechanism (confirmed 2026-08-03 after the first CI/CD deploy wiped production secrets):**
every secret lives in **`appsettings.Production.json`, edited directly on the server and never
overwritten by a deploy.** ASP.NET Core loads it automatically after `appsettings.json` when
`ASPNETCORE_ENVIRONMENT=Production` (set in `web.config`), so its values override the blank
placeholders committed to git. **This now applies to two separate files, one per site** — BBF.Api
introduced its own copy of this same pattern (and its own new secret types: `Jwt.SigningKey`,
`Jwt.InternalServiceKey`) rather than sharing BBF.Web's file, since the two apps run as separate IIS
sites/processes with separate working directories.

- `D:\Website\BBF\appsettings.Production.json` — DB connection string, `Jwt.InternalServiceKey`
  (must be the *same* value as BBF.Api's copy below — this is the shared key the two apps use to trust
  each other's internal-token exchange), plus everything BBF.Web's Identity/cookie login still needs
  directly.
- `D:\Website\BBF.Api\appsettings.Production.json` — DB connection string, `Jwt.SigningKey`,
  `Jwt.InternalServiceKey` (same value as above), Home Assistant token, Plaid credentials, AMP
  username/password, Emby API key, and the document storage path. Everything that actually talks to
  the database or a LAN service now lives here, not in BBF.Web's copy.

The versions committed to git (`BBF/appsettings.Production.json`, `BBF.Api/appsettings.Production.json`)
only have placeholder/blank values — that's intentional, real secrets must never be committed. On the
server, edit the real ones directly:

```powershell
notepad D:\Website\BBF\appsettings.Production.json
notepad D:\Website\BBF.Api\appsettings.Production.json
```

and fill in the real values for each key (see each project's placeholder file in the repo for the
exact shape). `Jwt:SigningKey` and `Jwt:InternalServiceKey` need real random values you generate
yourself — e.g. `[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))` in
PowerShell gives a reasonable one. Use a *different* random value for each of the two keys, but copy
`Jwt:InternalServiceKey` identically into both files — if the two don't match, BBF.Web's cookie-to-JWT
exchange will fail with 401s on every page.

**Both automated (`deploy.yml`) and manual (`robocopy` above) deploys must exclude
`appsettings.Production.json`** from the sync, exactly like `web.config` — for both sites, not just
BBF.Web. This is not optional, it's the only thing standing between a routine deploy and wiping every
credential the app has, as happened on 2026-08-03. Keep a backup of both real files somewhere outside
`D:\Website\` (e.g. a password manager or an offline copy) in case either is ever lost.

---

## Step 4: Create IIS Site

In IIS Manager:

1. **Application Pools** → Add Application Pool:
   - Name: `BBF`
   - .NET CLR version: `No Managed Code`
   - Managed pipeline mode: `Integrated`

2. **Sites** → Add Website:
   - Site name: `BBF`
   - Application pool: `BBF`
   - Physical path: `C:\inetpub\BBF`
   - Binding: `http` on port `5000` (Cloudflare Tunnel will handle external access)
   - Host name: leave blank

Or via PowerShell:
```powershell
Import-Module WebAdministration

# Create app pool
New-WebAppPool -Name "BBF"
Set-ItemProperty "IIS:\AppPools\BBF" -Name "managedRuntimeVersion" -Value ""

# Create site
New-Website -Name "BBF" -PhysicalPath "C:\inetpub\BBF" -ApplicationPool "BBF" -Port 5000
```

3. Set folder permissions:
```powershell
icacls "C:\inetpub\BBF" /grant "IIS_IUSRS:(OI)(CI)RX"
icacls "C:\BBFData" /grant "IIS_IUSRS:(OI)(CI)F"
icacls "C:\inetpub\BBF\logs" /grant "IIS_IUSRS:(OI)(CI)F"
```

4. Verify the site is running by browsing to `http://10.69.1.5:5000` from your network.

---

## Step 4b: Create the BBF.Api IIS Site

Same idea as Step 4, as a separate site on port 5001:

1. **Application Pools** → Add Application Pool:
   - Name: `BBF.Api`
   - .NET CLR version: `No Managed Code`
   - Managed pipeline mode: `Integrated`

2. **Sites** → Add Website:
   - Site name: `bbf-api` (matches `.github/workflows/deploy.yml` — if you name it something else,
     update the workflow's `Stop-Website`/`Start-Website` calls to match, the same way `bigboisfederation`
     had to be corrected for BBF.Web)
   - Application pool: `BBF.Api`
   - Physical path: `D:\Website\BBF.Api`
   - Binding: `http` on port `5001`
   - Host name: leave blank

Or via PowerShell:
```powershell
Import-Module WebAdministration

New-WebAppPool -Name "BBF.Api"
Set-ItemProperty "IIS:\AppPools\BBF.Api" -Name "managedRuntimeVersion" -Value ""

New-Website -Name "bbf-api" -PhysicalPath "D:\Website\BBF.Api" -ApplicationPool "BBF.Api" -Port 5001
```

Set folder permissions:
```powershell
icacls "D:\Website\BBF.Api" /grant "IIS_IUSRS:(OI)(CI)RX"
icacls "D:\Website\BBF.Api\logs" /grant "IIS_IUSRS:(OI)(CI)F"
```

Verify by browsing to `http://10.69.1.5:5001/health` from your network — it should return `healthy`.

**Do this (and the Cloudflare Tunnel update in Step 5) before merging a change that touches
`.github/workflows/deploy.yml`'s `deploy-api` job**, or that job's `Start-Website -Name "bbf-api"`
step will fail on the next push (harmlessly — it won't affect `deploy-web`, which is a separate job,
but BBF.Api won't be running until you create the site and push again).

---

## Step 5: Install and Configure Cloudflare Tunnel

On the server, open PowerShell as Administrator:

```powershell
# Install cloudflared
winget install Cloudflare.cloudflared

# Authenticate with Cloudflare (opens browser)
cloudflared tunnel login

# Create the tunnel
cloudflared tunnel create bbf

# Note the tunnel ID from the output (e.g., a1b2c3d4-...)
```

Create the config file at `C:\Users\<your-server-user>\.cloudflared\config.yml`:

```yaml
tunnel: <TUNNEL_ID>
credentials-file: C:\Users\<your-server-user>\.cloudflared\<TUNNEL_ID>.json

ingress:
  - hostname: bigboisfederation.com
    service: http://localhost:5000
  - hostname: "*.bigboisfederation.com"
    service: http://localhost:5000
  # BBF.Api needs to be reachable from the public internet too — BBF.Mobile will call it from
  # cellular networks, not just the home LAN. BBF.Web itself does NOT go through this hostname; it
  # calls BBF.Api over plain http://localhost:5001 since they're on the same box.
  #
  # IMPORTANT — this rule MUST come before the general api.bigboisfederation.com rule below: it
  # blocks the one endpoint that must never be reachable from outside the LAN.
  # /api/auth/internal-token lets BBF.Web trade its cookie session for a JWT for ANY user id, trusted
  # only because "the caller is on the same box" is the entire security model for that endpoint — see
  # the comment on it in BBF.Api/Endpoints/AuthEndpoints.cs. Exposing it publicly would let anyone who
  # also obtains Jwt:InternalServiceKey mint a token for any account with no password at all.
  - hostname: api.bigboisfederation.com
    path: ^/api/auth/internal-token$
    service: http_status:404
  - hostname: api.bigboisfederation.com
    service: http://localhost:5001
  - service: http_status:404
```

Add DNS records:

```powershell
cloudflared tunnel route dns bbf bigboisfederation.com
cloudflared tunnel route dns bbf "*.bigboisfederation.com"
cloudflared tunnel route dns bbf api.bigboisfederation.com
```

Test the tunnel:

```powershell
cloudflared tunnel run bbf
```

If it works, install as a Windows service for auto-start:

```powershell
cloudflared service install
```

---

## Step 6: Verify External Access

1. Browse to `https://bigboisfederation.com` — should show the BBF welcome screen
2. Log in and verify all features work:
   - AI Chat with streaming
   - Document uploads
   - Home Assistant controls
   - Service health checks
   - Wiki pages
3. Browse to `https://api.bigboisfederation.com/health` — should return `healthy`
4. Confirm the internal-token endpoint is actually blocked publicly:
   `https://api.bigboisfederation.com/api/auth/internal-token` should return a 404 from Cloudflare
   (not reach BBF.Api at all) regardless of request method or body.

---

## Updating the Deployment

**As of the CI/CD pipeline setup, pushes to `main` deploy automatically — see `CICD_Setup_Guide.md`.**
The manual steps below still work as a fallback if the self-hosted runner is ever down. Repeat them
for whichever site(s) you changed — they're independent deploys with no required order between them,
though if a change touched `BBF.Data`'s EF model, apply migrations once (from `BBF/`, not `BBF.Api/`
— they share one database) before restarting either site.

```powershell
# On dev machine
cd F:\Coding\BBF
dotnet publish BBF/BBF.csproj -c Release -o ./publish-web
dotnet publish BBF.Api/BBF.Api.csproj -c Release -o ./publish-api

# Copy to server (stop the site(s) first)
# In IIS Manager: stop "bigboisfederation" and/or "bbf-api"
robocopy "F:\Coding\BBF\publish-web" "D:\Website\BBF" /MIR /XF web.config appsettings.Development.json appsettings.Production.json
robocopy "F:\Coding\BBF\publish-api" "D:\Website\BBF.Api" /MIR /XF web.config appsettings.Development.json appsettings.Production.json
# In IIS Manager: start "bigboisfederation" and/or "bbf-api"
```

**Note:** Exclude `web.config` AND `appsettings.Production.json` from robocopy, for *both* sites — each
one's copy holds real production secrets (see Step 3) and forgetting either wipes them, the same way
BBF.Web's was wiped on 2026-08-03.

---

## Troubleshooting

**App won't start in IIS:**
- Check `D:\Website\BBF\logs\stdout*` (or `D:\Website\BBF.Api\logs\stdout*`) for errors (note: the
  CI/CD pipeline's `/MIR` also deletes this `logs` folder on every deploy since it isn't part of the
  publish output — recreate it if you need stdout logging: `mkdir D:\Website\BBF\logs` /
  `mkdir D:\Website\BBF.Api\logs`)
- Enable stdout logging: set `stdoutLogEnabled="true"` in web.config
- Verify .NET 10 Hosting Bundle is installed: `dotnet --info` on server

**BBF.Web's pages all fail / every API call 401s:**
- This is almost always `Jwt:InternalServiceKey` mismatched between
  `D:\Website\BBF\appsettings.Production.json` and `D:\Website\BBF.Api\appsettings.Production.json`
  — they must be byte-for-byte identical. Also check `BBF.Api/appsettings.Production.json` has a
  non-empty `Jwt:SigningKey` — an empty one throws on every request (even `/health`), since the JWT
  auth handler validates its config eagerly regardless of whether the route requires auth.

**Cloudflare Tunnel not connecting:**
- Check tunnel status: `cloudflared tunnel info bbf`
- Verify DNS records in Cloudflare dashboard
- Check Windows Firewall isn't blocking cloudflared

**Database connection issues:**
- Verify SQL Server is running
- Check the connection string in each site's `appsettings.Production.json`
- Ensure both the `BBF` and `BBF.Api` app pool identities have access to SQL Server

**Document uploads failing:**
- Verify `C:\BBFData\Documents` exists and IIS_IUSRS has write access
- Documents now flow entirely through BBF.Api (BBF.Web proxies the download request, but the actual
  disk I/O and `Documents:StoragePath` config live only in `BBF.Api/appsettings.Production.json`)
