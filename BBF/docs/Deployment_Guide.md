# BBF Deployment Guide

Deploy BBF to Windows Server 2022 (10.69.1.5) via IIS + Cloudflare Tunnel.

**Actual production values (confirmed 2026-08-03 — this doc originally specified different ones,
which broke the first CI/CD deploy run; if you change these on the server, update this doc too):**
- IIS site name: `bigboisfederation` (not `BBF`)
- Physical path: `D:\Website\BBF` (not `C:\inetpub\BBF`)
- App pool name: unconfirmed — verify with `Get-IISAppPool` before trusting the "BBF" references below

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

Copy the published files to the server:

```powershell
# On the server, create the deployment folder
mkdir C:\inetpub\BBF

# Create document storage and log folders
mkdir C:\BBFData\Documents
mkdir C:\inetpub\BBF\logs
```

Copy everything from `F:\Coding\BBF\publish\` to `D:\Website\BBF\` on the server, **excluding
`web.config` and `appsettings.Production.json`** — see Step 3 before running this for the first time.

```powershell
robocopy "F:\Coding\BBF\publish" "D:\Website\BBF" /MIR /XF web.config appsettings.Development.json appsettings.Production.json
```

---

## Step 3: Configure Production Secrets

**The real mechanism (confirmed 2026-08-03 after the first CI/CD deploy wiped production secrets):**
every secret — DB connection string, Home Assistant token, Plaid credentials, AMP username/password,
Emby API key, and the document storage path — lives in a single file, **`appsettings.Production.json`,
edited directly on the server and never overwritten by a deploy.** ASP.NET Core loads it automatically
after `appsettings.json` when `ASPNETCORE_ENVIRONMENT=Production` (set in `web.config`), so its values
override the blank placeholders committed to git.

The version committed to git (`BBF/appsettings.Production.json`) only has placeholder/blank values —
that's intentional, real secrets must never be committed. On the server, edit the real one directly:

```powershell
notepad D:\Website\BBF\appsettings.Production.json
```

and fill in the real values for each key (see the placeholder file in the repo for the exact shape:
`ConnectionStrings.DefaultConnection`, `HomeAssistant.Token`, `Plaid.ClientId`/`Plaid.Secret`,
`AMP.Username`/`AMP.Password`, `Emby.ApiKey`, `Documents.StoragePath`).

**Both automated (`deploy.yml`) and manual (`robocopy` above) deploys must exclude
`appsettings.Production.json`** from the sync, exactly like `web.config` — this is not optional, it's
the only thing standing between a routine deploy and wiping every credential the app has, as happened
on 2026-08-03. Keep a backup of the real file somewhere outside `D:\Website\BBF` (e.g. a password
manager or an offline copy) in case it's ever lost.

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
  - service: http_status:404
```

Add DNS records:

```powershell
cloudflared tunnel route dns bbf bigboisfederation.com
cloudflared tunnel route dns bbf "*.bigboisfederation.com"
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

---

## Updating the Deployment

**As of the CI/CD pipeline setup, pushes to `main` deploy automatically — see `CICD_Setup_Guide.md`.**
The manual steps below still work as a fallback if the self-hosted runner is ever down.

When you make changes:

```powershell
# On dev machine
cd F:\Coding\BBF
dotnet publish BBF/BBF.csproj -c Release -o ./publish

# Copy to server (stop the site first)
# In IIS Manager: stop the "bigboisfederation" site
robocopy "F:\Coding\BBF\publish" "D:\Website\BBF" /MIR /XF web.config appsettings.Development.json appsettings.Production.json
# In IIS Manager: start the "bigboisfederation" site
```

**Note:** Exclude `web.config` AND `appsettings.Production.json` from robocopy — the latter holds every
production secret (see Step 3) and forgetting it wipes them, as happened on 2026-08-03.

---

## Troubleshooting

**App won't start in IIS:**
- Check `D:\Website\BBF\logs\stdout*` for errors (note: the CI/CD pipeline's `/MIR` also deletes this
  `logs` folder on every deploy since it isn't part of the publish output — recreate it if you need
  stdout logging: `mkdir D:\Website\BBF\logs`)
- Enable stdout logging: set `stdoutLogEnabled="true"` in web.config
- Verify .NET 10 Hosting Bundle is installed: `dotnet --info` on server

**Cloudflare Tunnel not connecting:**
- Check tunnel status: `cloudflared tunnel info bbf`
- Verify DNS records in Cloudflare dashboard
- Check Windows Firewall isn't blocking cloudflared

**Database connection issues:**
- Verify SQL Server is running
- Check the connection string in web.config environment variables
- Ensure the BBF app pool identity has access to SQL Server

**Document uploads failing:**
- Verify `C:\BBFData\Documents` exists and IIS_IUSRS has write access
