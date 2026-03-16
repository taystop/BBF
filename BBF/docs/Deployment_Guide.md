# BBF Deployment Guide

Deploy BBF to Windows Server 2022 (10.69.1.5) via IIS + Cloudflare Tunnel.

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

Copy everything from `F:\Coding\BBF\publish\` to `C:\inetpub\BBF\` on the server.

If the server has access to the F: drive:
```powershell
robocopy "F:\Coding\BBF\publish" "C:\inetpub\BBF" /MIR /XF appsettings.Development.json
```

---

## Step 3: Configure Production Secrets

Set environment variables for the IIS app pool (avoids secrets in files):

```powershell
# Open IIS Manager → Application Pools → BBF pool → Advanced Settings
# Or set via command line:

# Option A: Use environment variables in the web.config
# Edit C:\inetpub\BBF\web.config and add environmentVariables:
```

Edit `C:\inetpub\BBF\web.config` to include secrets:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\BBF.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
          <environmentVariable name="ConnectionStrings__DefaultConnection" value="Server=localhost;Database=BBF;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" />
          <environmentVariable name="HomeAssistant__Token" value="YOUR_HA_TOKEN_HERE" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

**Important:** Replace `YOUR_HA_TOKEN_HERE` with the actual Home Assistant token.

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

When you make changes:

```powershell
# On dev machine
cd F:\Coding\BBF
dotnet publish BBF/BBF.csproj -c Release -o ./publish

# Copy to server (stop the site first)
# In IIS Manager: stop the BBF site
robocopy "F:\Coding\BBF\publish" "C:\inetpub\BBF" /MIR /XF web.config appsettings.Development.json
# In IIS Manager: start the BBF site
```

**Note:** Exclude `web.config` from robocopy so you don't overwrite production secrets.

---

## Troubleshooting

**App won't start in IIS:**
- Check `C:\inetpub\BBF\logs\stdout*` for errors
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
