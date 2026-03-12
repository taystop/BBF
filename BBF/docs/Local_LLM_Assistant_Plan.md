# Local LLM Assistant & Home Dashboard Implementation Plan

## Overview

Deploy a local AI assistant powered by Qwen3.5 9B on the Windows Server 2022 (10.69.1.5) alongside a custom Blazor web application that serves as a unified home dashboard. The dashboard integrates LLM chat, Home Assistant device control, network documentation, service status monitoring, and quick-access links — all accessible via a personal domain through Cloudflare.

**Key Goals:**
- Run Qwen3.5 9B locally with zero cloud dependency for AI
- Build a custom Blazor Web App with MudBlazor UI as the central home dashboard
- Integrate with Home Assistant for smart home control (TP-Link plugs, Ecobee thermostat, Dreo fan, cameras)
- Provide a searchable documentation wiki for network and device knowledge
- Display real-time server/service status monitoring
- Serve as a link hub for all self-hosted services
- Host on a personal domain via Cloudflare
- Design for GPU upgrade path (GTX 1070 → RTX 3090 Ti)

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Prerequisites](#prerequisites)
3. [Hardware Considerations & GPU Upgrade Path](#hardware-considerations--gpu-upgrade-path)
4. [Phase 1: Ollama LLM Runtime](#phase-1-ollama-llm-runtime)
5. [Phase 2: SQL Server Setup](#phase-2-sql-server-setup)
6. [Phase 3: Blazor Web App — Foundation](#phase-3-blazor-web-app--foundation)
7. [Phase 4: LLM Chat Integration](#phase-4-llm-chat-integration)
8. [Phase 5: Home Assistant Integration](#phase-5-home-assistant-integration)
9. [Phase 6: Documentation Wiki (RAG)](#phase-6-documentation-wiki-rag)
10. [Phase 7: Service Status Dashboard](#phase-7-service-status-dashboard)
11. [Phase 8: Service Links & Bookmarks](#phase-8-service-links--bookmarks)
12. [Phase 9: Domain & Cloudflare Configuration](#phase-9-domain--cloudflare-configuration)
13. [Phase 10: Extended Integrations](#phase-10-extended-integrations)
14. [Model Recommendations](#model-recommendations)
15. [Security Considerations](#security-considerations)
16. [Maintenance & Backup](#maintenance--backup)
17. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

### System Architecture

```
You (any device — LAN, WiFi, or remote via Cloudflare/Twingate)
        |
        | HTTPS (yourdomain.com)
        v
Cloudflare (DNS + Proxy/Tunnel)
        |
        v
Blazor Web App — MudBlazor UI (10.69.1.5:443)
   ├── LLM Chat Page ──────────► Ollama API (10.69.1.5:11434)
   │                                └── Qwen3.5 9B (GPU-accelerated)
   ├── Home Control Page ──────► Home Assistant API (10.69.1.153:8123)
   │                                ├── TP-Link Smart Plugs
   │                                ├── Ecobee Thermostat
   │                                ├── Dreo WiFi Tower Fan
   │                                ├── TP-Link Cameras (IoT VLAN)
   │                                └── Emby Media Server
   ├── Documentation Wiki ─────► SQL Server (local) + RAG via Ollama
   ├── Service Status ─────────► Health checks to all services
   ├── Service Links ──────────► Bookmarks to Emby, HA, TrueNAS, AMP, etc.
   └── (Future) Budget Tools ──► SQL Server (local)
        |
        v
SQL Server (10.69.1.5)
   ├── Wiki articles & documentation
   ├── Service bookmarks & metadata
   ├── Chat history & conversations
   ├── User settings & preferences
   └── (Future) Budget/financial data
```

### Service Stack

| Service | Purpose | Port | Type | Host |
|---------|---------|------|------|------|
| **Blazor Web App** | Custom dashboard & UI | 443 (HTTPS) | IIS / Kestrel | Windows Server (10.69.1.5) |
| **Ollama** | LLM inference engine | 11434 | Docker | Windows Server (10.69.1.5) |
| **SQL Server** | Application database | 1433 | Windows Service | Windows Server (10.69.1.5) |
| **Home Assistant** | Smart home hub | 8123 | Existing install | Existing |
| **Cloudflare Tunnel** | Secure external access | — | cloudflared | Windows Server (10.69.1.5) |

### Data Flow

```
[Browser] ◄──HTTPS──► [Cloudflare] ◄──Tunnel──► [Blazor App on IIS/Kestrel]
                                                        |
                        +-------------------------------+-------------------------------+
                        |                               |                               |
                        v                               v                               v
                [Ollama REST API]              [HA REST API]                    [SQL Server]
                 POST /api/chat                GET/POST /api/                   Chat history
                 POST /api/generate            services, states                 Wiki content
                 Streaming responses           Device control                   Bookmarks
                                                                               Settings
```

**Why This Design:**
- **Custom Blazor app** gives you full control — no compromises of a prebuilt UI
- **MudBlazor** provides a polished Material Design component library out of the box
- **SQL Server** is the natural backend for a .NET stack on Windows Server
- **All LLM processing stays local** — no data leaves the network
- **Cloudflare Tunnel** provides secure external access without opening ports
- **Single server** hosts everything — simplified management
- **GPU upgrade is a drop-in improvement** — no architecture changes needed
- **Extensible** — budget tools, new integrations, etc. just become new pages/modules

---

## Prerequisites

### Windows Server Requirements

- [ ] Docker Desktop installed and running (confirmed ✅)
- [ ] .NET 8+ SDK installed (for Blazor development)
- [ ] IIS installed with ASP.NET Core Hosting Bundle (or use Kestrel standalone)
- [ ] SQL Server installed (Express is free and sufficient, or Developer Edition)
- [ ] SQL Server Management Studio (SSMS) for database management
- [ ] At least 30GB free disk space (models + database + app)
- [ ] NVIDIA drivers up to date for GTX 1070
- [ ] NVIDIA Container Toolkit installed (for GPU passthrough to Docker)
- [ ] Dual 1100W PSUs confirmed ✅ — ample power for any GPU upgrade
- [ ] Visual Studio 2022 or VS Code with C# Dev Kit (for development)

### Home Assistant Requirements

- [ ] Home Assistant instance running and accessible
- [ ] Existing integrations working (TP-Link plugs, Dreo fan, cameras)
- [ ] Admin access to Home Assistant
- [ ] Ecobee account credentials ready for integration
- [ ] Long-lived access token created (Profile → Security → Create Token)

### Network & Domain Requirements

- [ ] Domain configured in Cloudflare (confirmed ✅)
- [ ] DNS pointing to network (confirmed ✅)
- [ ] Cloudflare account with tunnel capability (free plan works)
- [ ] VLAN routing allows traffic between client devices and Windows Server

---

## Hardware Considerations & GPU Upgrade Path

### Current Hardware: GTX 1070 (8GB VRAM)

**Qwen3.5 9B Performance:**
| Metric | Value |
|--------|-------|
| Model size on disk | 6.6GB (Q4_K_M) |
| VRAM usage | ~7GB |
| Fits in 8GB VRAM | Yes — tight but functional |
| Expected speed | ~20-30 tokens/sec |
| Context window | Standard (model supports large context, but limited by VRAM) |

The GTX 1070 will run Qwen3.5 9B, but with ~7GB of 8GB VRAM occupied, there's very little headroom. You may see occasional CPU offloading at longer context lengths.

### Upgrade: RTX 3090 Ti (24GB VRAM) — Recommended Path

**What it unlocks:**
| Model | Parameters | VRAM Usage | Speed |
|-------|-----------|------------|-------|
| Qwen3.5 9B | 9.65B | ~7GB | Fast (~40+ tok/s) — room to breathe |
| Qwen3 32B | 32B | ~18GB | Good (~20 tok/s) |
| Llama 3.1 70B (Q4) | 70B | ~22GB | Moderate (~15 tok/s) |
| Qwen3.5 9B + Qwen3 32B loaded simultaneously | — | ~25GB | Swap between them |

**Key benefit:** With 24GB, Qwen3.5 9B runs with tons of headroom for long conversations and large context, plus you can keep a larger model available for complex tasks.

**Power:** The R730XD with dual 1100W PSUs (2200W total, typically in redundant mode so 1100W usable) handles the 3090 Ti's 450W TDP with no issues. The remaining ~650W is more than enough for dual Xeons + drives + everything else.

**Physical fit:** The R730XD has full-length PCIe slots. The 3090 Ti is a large card — measure your specific chassis clearance, but most R730XD configurations accommodate full-length dual-slot GPUs. You may need to remove the GPU shroud/cover plate if one exists.

### Alternative: Used Server GPU

| GPU | VRAM | Approx Cost | Notes |
|-----|------|-------------|-------|
| Tesla P40 | 24GB | ~$150-200 | No video out, passive cooling, great value, but older Pascal arch |
| NVIDIA A10 | 24GB | ~$800-1000 | Ampere arch, good performance, designed for servers |

**Recommendation:** The 3090 Ti is the best path since you'll already own it. The dual 1100W PSUs make power a non-issue.

---

## Phase 1: Ollama LLM Runtime

**Time estimate:** 30 minutes
**Difficulty:** Low

### Step 1.1: Install NVIDIA Container Toolkit

Enable Docker containers to access the GTX 1070 for GPU-accelerated inference.

```powershell
# Verify NVIDIA driver is installed and GPU is detected
nvidia-smi
```

You should see the GTX 1070 listed. If not, install/update NVIDIA drivers first.

1. Open Docker Desktop → **Settings → Resources → GPU**
2. Enable GPU support (Docker Desktop on Windows uses WSL2 backend for GPU passthrough)
3. Verify WSL2 is enabled and up to date:

```powershell
wsl --update
```

### Step 1.2: Create Docker Compose for Ollama

```powershell
mkdir C:\docker\ollama
```

Create `C:\docker\ollama\docker-compose.yml`:

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    restart: unless-stopped
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    environment:
      - OLLAMA_HOST=0.0.0.0

volumes:
  ollama_data:
```

### Step 1.3: Start Ollama

```powershell
cd C:\docker\ollama
docker compose up -d
```

### Step 1.4: Pull Qwen3.5 9B

```powershell
# Pull the target model
docker exec -it ollama ollama pull qwen3.5:9b

# Verify it works
docker exec -it ollama ollama run qwen3.5:9b "Say hello and tell me what model you are"
```

### Step 1.5: Verify GPU Usage

```powershell
# While a model is loaded, check GPU memory usage
nvidia-smi
```

You should see Ollama using ~7GB of the 8GB VRAM.

### Step 1.6: Test the REST API

```powershell
# Test the API endpoint that Blazor will call
curl http://localhost:11434/api/chat -d '{
  "model": "qwen3.5:9b",
  "messages": [{"role": "user", "content": "Hello"}],
  "stream": false
}'
```

---

## Phase 2: SQL Server Setup

**Time estimate:** 30-60 minutes
**Difficulty:** Low

### Step 2.1: Install SQL Server

If SQL Server isn't already installed:

1. Download **SQL Server 2022 Express** (free) or use **Developer Edition** (free for dev/test)
2. Run the installer → **Basic** installation is fine
3. Note the connection string after installation
4. Install **SQL Server Management Studio (SSMS)** for administration

### Step 2.2: Create the Application Database

Open SSMS and connect to the local instance, then run:

```sql
CREATE DATABASE HomeDashboard;
GO

USE HomeDashboard;
GO

-- Chat history
CREATE TABLE ChatConversations (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(200),
    Model NVARCHAR(100) DEFAULT 'qwen3.5:9b',
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
);

CREATE TABLE ChatMessages (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ConversationId INT FOREIGN KEY REFERENCES ChatConversations(Id) ON DELETE CASCADE,
    Role NVARCHAR(20) NOT NULL, -- 'user', 'assistant', 'system'
    Content NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE()
);

-- Service bookmarks
CREATE TABLE ServiceLinks (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Url NVARCHAR(500) NOT NULL,
    Icon NVARCHAR(100),           -- MudBlazor icon name
    Category NVARCHAR(50),        -- 'Media', 'Infrastructure', 'Monitoring', etc.
    Description NVARCHAR(500),
    SortOrder INT DEFAULT 0,
    HealthCheckUrl NVARCHAR(500), -- Optional URL to ping for status
    IsActive BIT DEFAULT 1
);

-- Documentation wiki
CREATE TABLE WikiArticles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    Category NVARCHAR(100),
    Tags NVARCHAR(500),           -- Comma-separated tags for search
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
);

-- Service health check log
CREATE TABLE ServiceHealthLog (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ServiceLinkId INT FOREIGN KEY REFERENCES ServiceLinks(Id) ON DELETE CASCADE,
    IsHealthy BIT NOT NULL,
    ResponseTimeMs INT,
    CheckedAt DATETIME2 DEFAULT GETUTCDATE()
);

-- App settings (key-value store for configuration)
CREATE TABLE AppSettings (
    [Key] NVARCHAR(100) PRIMARY KEY,
    [Value] NVARCHAR(MAX),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
);
GO

-- Seed initial service links
INSERT INTO ServiceLinks (Name, Url, Icon, Category, Description, SortOrder, HealthCheckUrl) VALUES
('Home Assistant', 'http://10.69.1.153:8123', 'Home', 'Smart Home', 'Home automation dashboard', 1, 'http://10.69.1.153:8123/api/'),
('Emby', 'http://10.69.1.5:8096', 'Movie', 'Media', 'Media streaming server', 2, 'http://10.69.1.5:8096/emby/System/Ping'),
('TrueNAS', 'http://10.69.1.3', 'Storage', 'Infrastructure', 'NAS management', 3, 'http://10.69.1.3/api/v2.0/system/state'),
('AMP', 'http://10.69.1.3:8080', 'SportsEsports', 'Gaming', 'Game server management', 4, NULL),
('Ollama', 'http://10.69.1.5:11434', 'Psychology', 'AI', 'Local LLM engine', 5, 'http://10.69.1.5:11434/api/tags'),
('EdgeRouter', 'https://10.69.1.1', 'Router', 'Infrastructure', 'Network router admin', 6, NULL),
('AdGuard Home', 'http://10.69.1.6:3000', 'Shield', 'Infrastructure', 'DNS ad blocking', 7, NULL);
GO

-- Seed initial app settings
INSERT INTO AppSettings ([Key], [Value]) VALUES
('OllamaBaseUrl', 'http://localhost:11434'),
('OllamaDefaultModel', 'qwen3.5:9b'),
('HomeAssistantUrl', 'http://10.69.1.153:8123'),
('HomeAssistantToken', ''),  -- Set after creating HA long-lived token
('HealthCheckIntervalSeconds', '60'),
('SiteName', 'Home Dashboard');
GO
```

**Note:** Adjust the seed data above to match your actual service URLs and ports. The AdGuard Home URL assumes the implementation from the AdGuard plan — update if different.

---

## Phase 3: Blazor Web App — Foundation

**Time estimate:** 2-4 hours (initial scaffold)
**Difficulty:** Medium

### Step 3.1: Create the Blazor Project

```powershell
# Create project directory
mkdir C:\Projects\HomeDashboard
cd C:\Projects\HomeDashboard

# Create Blazor Web App (Server-side rendering + interactive)
dotnet new blazor -n HomeDashboard -f net8.0 --interactivity Server

cd HomeDashboard

# Add MudBlazor
dotnet add package MudBlazor

# Add SQL Server / EF Core packages
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools

# Add HTTP client for Ollama and HA APIs
dotnet add package Microsoft.Extensions.Http

# Add authentication (optional but recommended)
dotnet add package Microsoft.AspNetCore.Authentication.Cookies
```

### Step 3.2: Configure MudBlazor

In `Program.cs`, add MudBlazor services:

```csharp
using MudBlazor.Services;

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
});
```

In `App.razor` (or `_Host.cshtml`), add MudBlazor resources:

```html
<link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

In `MainLayout.razor`, wrap content with MudBlazor providers:

```razor
@inherits LayoutComponentBase

<MudThemeProvider @bind-IsDarkMode="@_isDarkMode" Theme="@_theme" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit"
                       Edge="Edge.Start" OnClick="@ToggleDrawer" />
        <MudText Typo="Typo.h5" Class="ml-3">Home Dashboard</MudText>
        <MudSpacer />
        <MudIconButton Icon="@(_isDarkMode ? Icons.Material.Filled.LightMode : Icons.Material.Filled.DarkMode)"
                       Color="Color.Inherit" OnClick="@ToggleDarkMode" />
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" ClipMode="DrawerClipMode.Always" Elevation="2">
        <NavMenu />
    </MudDrawer>

    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.Large" Class="my-4">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

@code {
    private bool _drawerOpen = true;
    private bool _isDarkMode = true;

    private MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = Colors.Blue.Default,
            Secondary = Colors.Green.Accent4,
            AppbarBackground = Colors.Blue.Default,
        },
        PaletteDark = new PaletteDark()
        {
            Primary = Colors.Blue.Lighten1,
            Secondary = Colors.Green.Accent4,
        }
    };

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;
    private void ToggleDarkMode() => _isDarkMode = !_isDarkMode;
}
```

### Step 3.3: Create Navigation Menu

Create `NavMenu.razor`:

```razor
<MudNavMenu>
    <MudNavLink Href="/" Match="NavLinkMatch.All"
                Icon="@Icons.Material.Filled.Dashboard">Dashboard</MudNavLink>
    <MudNavLink Href="/chat"
                Icon="@Icons.Material.Filled.Chat">AI Chat</MudNavLink>
    <MudNavLink Href="/home-control"
                Icon="@Icons.Material.Filled.Home">Home Control</MudNavLink>
    <MudNavLink Href="/wiki"
                Icon="@Icons.Material.Filled.MenuBook">Documentation</MudNavLink>
    <MudNavLink Href="/services"
                Icon="@Icons.Material.Filled.Apps">Services</MudNavLink>
    <MudNavLink Href="/status"
                Icon="@Icons.Material.Filled.MonitorHeart">Status</MudNavLink>
</MudNavMenu>
```

### Step 3.4: Suggested Project Structure

```
HomeDashboard/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── Pages/
│   │   ├── Home.razor              -- Dashboard overview
│   │   ├── Chat.razor              -- LLM chat interface
│   │   ├── HomeControl.razor       -- HA device control
│   │   ├── Wiki.razor              -- Documentation wiki
│   │   ├── WikiArticle.razor       -- Single article view/edit
│   │   ├── Services.razor          -- Service bookmarks
│   │   └── Status.razor            -- Service health dashboard
│   └── Shared/
│       ├── ChatMessage.razor       -- Single chat message component
│       ├── DeviceCard.razor        -- HA device control card
│       ├── ServiceCard.razor       -- Service link card with status
│       └── StatusIndicator.razor   -- Health status badge
├── Data/
│   ├── HomeDashboardContext.cs     -- EF Core DbContext
│   └── Entities/                   -- EF Core entity models
│       ├── ChatConversation.cs
│       ├── ChatMessage.cs
│       ├── ServiceLink.cs
│       ├── WikiArticle.cs
│       ├── ServiceHealthLog.cs
│       └── AppSetting.cs
├── Services/
│   ├── OllamaService.cs           -- Ollama REST API client
│   ├── HomeAssistantService.cs     -- HA REST API client
│   ├── HealthCheckService.cs       -- Background service health checker
│   ├── WikiService.cs              -- Wiki CRUD + search
│   └── ChatService.cs             -- Chat history + Ollama orchestration
├── Program.cs
├── appsettings.json
└── HomeDashboard.csproj
```

### Step 3.5: Core Service — Ollama Client

Create `Services/OllamaService.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Runtime.CompilerServices;

public class OllamaService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _defaultModel;

    public OllamaService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _defaultModel = config["Ollama:DefaultModel"] ?? "qwen3.5:9b";
    }

    // Streaming chat response (for real-time token display)
    public async IAsyncEnumerable<string> ChatStreamAsync(
        List<ChatMessageDto> messages,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new
        {
            model = model ?? _defaultModel,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/chat")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _http.SendAsync(httpRequest,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaChatChunk>(line);
            if (chunk?.Message?.Content != null)
                yield return chunk.Message.Content;
        }
    }

    // Check if Ollama is running
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // List available models
    public async Task<List<string>> GetModelsAsync()
    {
        var response = await _http.GetFromJsonAsync<OllamaTagsResponse>($"{_baseUrl}/api/tags");
        return response?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
    }
}

// DTOs
public record ChatMessageDto(string Role, string Content);
public record OllamaChatChunk(OllamaChatMessage? Message, bool Done);
public record OllamaChatMessage(string Role, string Content);
public record OllamaTagsResponse(List<OllamaModel> Models);
public record OllamaModel(string Name, string ModifiedAt);
```

### Step 3.6: Core Service — Home Assistant Client

Create `Services/HomeAssistantService.cs`:

```csharp
using System.Net.Http.Json;
using System.Net.Http.Headers;

public class HomeAssistantService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public HomeAssistantService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["HomeAssistant:Url"] ?? "http://10.69.1.153:8123";
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config["HomeAssistant:Token"]);
    }

    // Get all entity states
    public async Task<List<HaEntityState>> GetStatesAsync()
    {
        return await _http.GetFromJsonAsync<List<HaEntityState>>($"{_baseUrl}/api/states")
               ?? new List<HaEntityState>();
    }

    // Get a specific entity
    public async Task<HaEntityState?> GetStateAsync(string entityId)
    {
        return await _http.GetFromJsonAsync<HaEntityState>($"{_baseUrl}/api/states/{entityId}");
    }

    // Call a service (turn on/off, set temperature, etc.)
    public async Task CallServiceAsync(string domain, string service, object data)
    {
        await _http.PostAsJsonAsync($"{_baseUrl}/api/services/{domain}/{service}", data);
    }

    // Convenience methods
    public Task TurnOnAsync(string entityId) =>
        CallServiceAsync(entityId.Split('.')[0], "turn_on", new { entity_id = entityId });

    public Task TurnOffAsync(string entityId) =>
        CallServiceAsync(entityId.Split('.')[0], "turn_off", new { entity_id = entityId });

    public Task SetTemperatureAsync(string entityId, double temp) =>
        CallServiceAsync("climate", "set_temperature", new { entity_id = entityId, temperature = temp });

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/api/");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

// DTOs
public record HaEntityState(
    string EntityId,
    string State,
    Dictionary<string, object>? Attributes,
    string LastChanged);
```

### Step 3.7: Register Services in Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Database
builder.Services.AddDbContext<HomeDashboardContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// HTTP clients for external APIs
builder.Services.AddHttpClient<OllamaService>();
builder.Services.AddHttpClient<HomeAssistantService>();

// Application services
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<WikiService>();
builder.Services.AddHostedService<HealthCheckService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

### Step 3.8: Configuration in appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=HomeDashboard;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "DefaultModel": "qwen3.5:9b"
  },
  "HomeAssistant": {
    "Url": "http://10.69.1.153:8123",
    "Token": ""
  }
}
```

**Important:** Store the Home Assistant token in user secrets for development, not in appsettings.json:

```powershell
cd C:\Projects\HomeDashboard\HomeDashboard
dotnet user-secrets set "HomeAssistant:Token" "your-long-lived-token-here"
```

---

## Phase 4: LLM Chat Integration

**Time estimate:** 2-3 hours
**Difficulty:** Medium

### Step 4.1: Chat Page

Create `Components/Pages/Chat.razor`:

```razor
@page "/chat"
@inject OllamaService Ollama
@inject ChatService ChatSvc

<MudText Typo="Typo.h4" Class="mb-4">AI Chat</MudText>

<MudGrid>
    <!-- Conversation sidebar -->
    <MudItem xs="3">
        <MudPaper Class="pa-2" Elevation="1" Style="height: 80vh; overflow-y: auto;">
            <MudButton FullWidth Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="NewConversation" Class="mb-2">New Chat</MudButton>
            @foreach (var conv in _conversations)
            {
                <MudNavLink OnClick="() => LoadConversation(conv.Id)"
                            Style="@(conv.Id == _currentConversationId ? "background: var(--mud-palette-action-default-hover);" : "")">
                    @conv.Title
                </MudNavLink>
            }
        </MudPaper>
    </MudItem>

    <!-- Chat area -->
    <MudItem xs="9">
        <MudPaper Class="pa-4" Elevation="1" Style="height: 80vh; display: flex; flex-direction: column;">
            <!-- Messages -->
            <div style="flex: 1; overflow-y: auto;" @ref="_messageContainer">
                @foreach (var msg in _messages)
                {
                    <MudPaper Class="@($"pa-3 mb-2 {(msg.Role == "user" ? "ml-auto" : "")}")"
                              Style="@($"max-width: 80%; {(msg.Role == "user" ? "background: var(--mud-palette-primary-darken);" : "")}")"
                              Elevation="0" Outlined="true">
                        <MudText Typo="Typo.caption" Color="Color.Secondary">
                            @(msg.Role == "user" ? "You" : "Qwen3.5")
                        </MudText>
                        <MudMarkdown Value="@msg.Content" />
                    </MudPaper>
                }
                @if (_isGenerating)
                {
                    <MudPaper Class="pa-3 mb-2" Elevation="0" Outlined="true" Style="max-width: 80%;">
                        <MudText Typo="Typo.caption" Color="Color.Secondary">Qwen3.5</MudText>
                        <MudMarkdown Value="@_streamingContent" />
                        <MudProgressLinear Color="Color.Primary" Indeterminate="true" Class="mt-1" />
                    </MudPaper>
                }
            </div>

            <!-- Input -->
            <MudTextField @bind-Value="_userInput" Label="Message" Variant="Variant.Outlined"
                          Lines="2" Immediate="true" OnKeyUp="HandleKeyUp"
                          Disabled="_isGenerating" Class="mt-2" />
            <MudButton OnClick="SendMessage" Variant="Variant.Filled" Color="Color.Primary"
                       Disabled="@(string.IsNullOrWhiteSpace(_userInput) || _isGenerating)"
                       Class="mt-2" FullWidth>
                @(_isGenerating ? "Generating..." : "Send")
            </MudButton>
        </MudPaper>
    </MudItem>
</MudGrid>
```

This gives you a ChatGPT-style interface with conversation history, streaming responses, and Markdown rendering — all in MudBlazor components.

### Step 4.2: Chat Features to Implement

- **Streaming responses** — tokens appear in real-time via `OllamaService.ChatStreamAsync`
- **Conversation persistence** — all chats saved to SQL Server via `ChatService`
- **Conversation titles** — auto-generate from first message or ask the LLM to summarize
- **Model selection** — dropdown to switch between pulled models
- **System prompt** — configurable per-conversation or global default
- **Markdown rendering** — MudBlazor's `MudMarkdown` component handles code blocks, lists, etc.
- **Stop generation** — cancel button using `CancellationToken`

---

## Phase 5: Home Assistant Integration

**Time estimate:** 2-3 hours
**Difficulty:** Medium

### Step 5.1: Add Ecobee to Home Assistant (Currently Missing)

1. In Home Assistant, go to **Settings → Devices & Services → Add Integration**
2. Search for **Ecobee**
3. Get your API key:
   - Go to https://www.ecobee.com/consumerportal/index.html
   - Log in → **Developer** section in the hamburger menu
   - Create a new application → copy the **API Key**
4. Enter the API key in Home Assistant
5. Follow the authorization flow (enter PIN on Ecobee thermostat portal)
6. Verify entities: thermostat, temperature sensors, humidity, occupancy

### Step 5.2: Home Control Page

Create `Components/Pages/HomeControl.razor`:

```razor
@page "/home-control"
@inject HomeAssistantService HA

<MudText Typo="Typo.h4" Class="mb-4">Home Control</MudText>

<MudGrid>
    <!-- Climate Section -->
    <MudItem xs="12">
        <MudText Typo="Typo.h6" Class="mb-2">Climate</MudText>
    </MudItem>
    <MudItem xs="12" md="6">
        <MudCard>
            <MudCardContent>
                <MudText Typo="Typo.h6">Ecobee Thermostat</MudText>
                <MudText>Current: @_thermostatTemp°F</MudText>
                <MudText>Mode: @_thermostatMode</MudText>
                <MudSlider @bind-Value="_targetTemp" Min="60" Max="85" Step="1"
                           Color="Color.Primary" Class="mt-3">
                    Target: @_targetTemp°F
                </MudSlider>
                <MudButton OnClick="SetTemperature" Variant="Variant.Filled"
                           Color="Color.Primary" Class="mt-2">Set Temperature</MudButton>
            </MudCardContent>
        </MudCard>
    </MudItem>
    <MudItem xs="12" md="6">
        <MudCard>
            <MudCardContent>
                <MudText Typo="Typo.h6">Dreo Tower Fan</MudText>
                <MudSwitch @bind-Value="_fanOn" Label="@(_fanOn ? "On" : "Off")"
                           Color="Color.Primary" CheckedChanged="ToggleFan" />
            </MudCardContent>
        </MudCard>
    </MudItem>

    <!-- Smart Plugs Section -->
    <MudItem xs="12">
        <MudText Typo="Typo.h6" Class="mb-2 mt-4">Smart Plugs</MudText>
    </MudItem>
    @foreach (var plug in _plugs)
    {
        <MudItem xs="12" sm="6" md="4">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h6">@plug.Name</MudText>
                    <MudSwitch Value="@(plug.State == "on")"
                               ValueChanged="(bool v) => TogglePlug(plug.EntityId, v)"
                               Color="Color.Primary"
                               Label="@plug.State" />
                </MudCardContent>
            </MudCard>
        </MudItem>
    }

    <!-- Cameras Section -->
    <MudItem xs="12">
        <MudText Typo="Typo.h6" Class="mb-2 mt-4">Cameras</MudText>
    </MudItem>
    @foreach (var cam in _cameras)
    {
        <MudItem xs="12" sm="6">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h6">@cam.Name</MudText>
                    <MudText Color="@(cam.State == "idle" ? Color.Success : Color.Warning)">
                        @cam.State
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
    }
</MudGrid>
```

### Step 5.3: LLM-Powered Home Control via Chat

The real power is controlling your home through natural language in the chat page. Add a system prompt and tool definitions so that when you ask the LLM to control devices, it generates structured output your app can parse and execute:

**System prompt for home automation context:**
```
You are a home assistant AI. When the user asks you to control a device, respond with a JSON action block that the system will execute:

Available devices:
- climate.ecobee (thermostat): set_temperature, set_hvac_mode
- fan.dreo_tower_fan: turn_on, turn_off
- switch.tp_link_* (smart plugs): turn_on, turn_off
- media_player.emby (media): media_play, media_pause

Example response for "set the temperature to 72":
I'll set the thermostat to 72°F.
```action
{"domain": "climate", "service": "set_temperature", "data": {"entity_id": "climate.ecobee", "temperature": 72}}
```

The Blazor app parses these action blocks from the LLM response and executes them via `HomeAssistantService.CallServiceAsync()`. This gives you natural language control through the chat interface.

---

## Phase 6: Documentation Wiki (RAG)

**Time estimate:** 2-3 hours
**Difficulty:** Medium

### Step 6.1: Wiki Page

Create a wiki system backed by SQL Server where you can:
- Create/edit articles with Markdown content
- Organize by category (Networking, Servers, Services, Guides)
- Search articles by title, content, and tags
- Import existing project documentation as wiki articles

### Step 6.2: RAG Integration for Smart Search

When you ask the LLM a question about your network, the app should:

1. **Search the wiki** — query SQL Server for relevant articles using full-text search
2. **Inject context** — include matching article content in the LLM's system prompt
3. **Generate answer** — the LLM answers using your actual documentation as context

Implementation flow in `ChatService`:

```csharp
public async IAsyncEnumerable<string> ChatWithRagAsync(
    string userMessage,
    List<ChatMessageDto> history,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    // 1. Search wiki for relevant content
    var relevantDocs = await _wikiService.SearchAsync(userMessage, maxResults: 3);

    // 2. Build context-enhanced system prompt
    var systemPrompt = "You are a knowledgeable network infrastructure assistant.\n\n";
    if (relevantDocs.Any())
    {
        systemPrompt += "Relevant documentation:\n\n";
        foreach (var doc in relevantDocs)
        {
            systemPrompt += $"--- {doc.Title} ---\n{doc.Content}\n\n";
        }
        systemPrompt += "Use the above documentation to answer accurately. "
                      + "Reference specific documents when possible.\n";
    }

    // 3. Send to Ollama with context
    var messages = new List<ChatMessageDto>
    {
        new("system", systemPrompt)
    };
    messages.AddRange(history);
    messages.Add(new("user", userMessage));

    await foreach (var token in _ollama.ChatStreamAsync(messages, ct: ct))
    {
        yield return token;
    }
}
```

### Step 6.3: SQL Server Full-Text Search

Enable full-text search for the wiki:

```sql
-- Enable full-text indexing on WikiArticles
CREATE FULLTEXT CATALOG WikiCatalog AS DEFAULT;

CREATE FULLTEXT INDEX ON WikiArticles(Title, Content, Tags)
    KEY INDEX PK__WikiArticles__Id
    ON WikiCatalog
    WITH STOPLIST = SYSTEM;
```

Query with:
```sql
SELECT * FROM WikiArticles
WHERE CONTAINS((Title, Content, Tags), @searchTerm)
ORDER BY RANK DESC;
```

### Step 6.4: Seed Wiki with Existing Docs

Import the project files as wiki articles:
- Each .md file → one wiki article
- Preserve Markdown formatting
- Categorize: Security, Networking, Services, Guides
- Tag appropriately for searchability

Build an import utility or admin page that reads .md files and creates wiki entries.

### Step 6.5: Keep Wiki Current

As you make changes to the network:
- Update wiki articles directly in the dashboard
- The LLM's RAG answers stay current automatically
- Consider adding a "Last verified" date to articles for staleness tracking

---

## Phase 7: Service Status Dashboard

**Time estimate:** 1-2 hours
**Difficulty:** Low-Medium

### Step 7.1: Background Health Check Service

Create `Services/HealthCheckService.cs` as a hosted background service:

```csharp
public class HealthCheckService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(IServiceScopeFactory scopeFactory, ILogger<HealthCheckService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HomeDashboardContext>();
            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);

            var services = await db.ServiceLinks
                .Where(s => s.HealthCheckUrl != null && s.IsActive)
                .ToListAsync(stoppingToken);

            foreach (var service in services)
            {
                var log = new ServiceHealthLog
                {
                    ServiceLinkId = service.Id,
                    CheckedAt = DateTime.UtcNow
                };

                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await http.GetAsync(service.HealthCheckUrl, stoppingToken);
                    sw.Stop();

                    log.IsHealthy = response.IsSuccessStatusCode;
                    log.ResponseTimeMs = (int)sw.ElapsedMilliseconds;
                }
                catch
                {
                    log.IsHealthy = false;
                    log.ResponseTimeMs = -1;
                }

                db.ServiceHealthLogs.Add(log);
            }

            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
```

### Step 7.2: Status Page

Display a dashboard showing:
- **Green/red indicators** for each service
- **Response time** graphs (using MudBlazor charts)
- **Uptime percentage** over last 24h / 7d / 30d
- **Last check timestamp**
- Real-time updates via Blazor Server SignalR connection

### Step 7.3: Notification Integration (Optional)

When a service goes down:
- Show a MudBlazor snackbar alert on the dashboard
- Optionally trigger a Home Assistant automation (flash a light, send notification)
- Log the outage duration

---

## Phase 8: Service Links & Bookmarks

**Time estimate:** 1 hour
**Difficulty:** Low

### Step 8.1: Services Page

Create a card grid of all your self-hosted services:

```razor
@page "/services"
@inject HomeDashboardContext Db

<MudText Typo="Typo.h4" Class="mb-4">Services</MudText>

@foreach (var category in _services.GroupBy(s => s.Category))
{
    <MudText Typo="Typo.h6" Class="mb-2 mt-4">@category.Key</MudText>
    <MudGrid>
        @foreach (var service in category.OrderBy(s => s.SortOrder))
        {
            <MudItem xs="12" sm="6" md="4">
                <MudCard Elevation="2" Class="cursor-pointer"
                         @onclick="() => NavigateToService(service.Url)">
                    <MudCardContent>
                        <div class="d-flex align-center gap-3">
                            <MudIcon Icon="@service.Icon" Size="Size.Large" />
                            <div>
                                <MudText Typo="Typo.h6">@service.Name</MudText>
                                <MudText Typo="Typo.caption">@service.Description</MudText>
                            </div>
                            <MudSpacer />
                            <StatusIndicator ServiceId="@service.Id" />
                        </div>
                    </MudCardContent>
                </MudCard>
            </MudItem>
        }
    </MudGrid>
}
```

### Step 8.2: Admin Functions

- Add/edit/remove service links from within the dashboard
- Drag-and-drop reordering
- Custom icons per service
- Category management

---

## Phase 9: Domain & Cloudflare Configuration

**Time estimate:** 1 hour
**Difficulty:** Low-Medium

### Step 9.1: Cloudflare Tunnel (Recommended Over Port Forwarding)

Cloudflare Tunnel creates an outbound-only connection from your server — **no ports need to be opened on the EdgeRouter**. This is significantly more secure than port forwarding.

1. **Install cloudflared on Windows Server:**

```powershell
# Download and install cloudflared
winget install Cloudflare.cloudflared

# Authenticate with Cloudflare
cloudflared tunnel login
```

2. **Create a tunnel:**

```powershell
cloudflared tunnel create home-dashboard
```

3. **Configure the tunnel** — create `C:\Users\<you>\.cloudflared\config.yml`:

```yaml
tunnel: <your-tunnel-id>
credentials-file: C:\Users\<you>\.cloudflared\<tunnel-id>.json

ingress:
  - hostname: yourdomain.com
    service: https://localhost:443
  - hostname: chat.yourdomain.com    # Optional subdomain for direct chat access
    service: https://localhost:443
  - service: http_status:404
```

4. **Add DNS records in Cloudflare:**

```powershell
cloudflared tunnel route dns home-dashboard yourdomain.com
cloudflared tunnel route dns home-dashboard chat.yourdomain.com  # Optional
```

5. **Install as a Windows service:**

```powershell
cloudflared service install
```

### Step 9.2: Cloudflare Access (Optional — Extra Security)

Add Cloudflare Access policies to require authentication before reaching your app:

1. In Cloudflare dashboard → **Zero Trust → Access → Applications**
2. Add application → Self-hosted
3. Set domain to `yourdomain.com`
4. Add an access policy (e.g., email OTP, GitHub login, etc.)
5. This adds a login screen before anyone can reach your Blazor app

This is separate from your app's own authentication and provides a second layer of protection.

### Step 9.3: Deploy Blazor App

**Option A: IIS (Recommended for Windows Server)**

1. Publish the app:
```powershell
cd C:\Projects\HomeDashboard\HomeDashboard
dotnet publish -c Release -o C:\inetpub\HomeDashboard
```

2. Install **ASP.NET Core Hosting Bundle** on the server
3. Create an IIS site pointing to `C:\inetpub\HomeDashboard`
4. Configure HTTPS with a certificate (Cloudflare can handle SSL termination)

**Option B: Kestrel Standalone (Simpler)**

```powershell
# Run directly
cd C:\inetpub\HomeDashboard
dotnet HomeDashboard.dll --urls "https://0.0.0.0:443;http://0.0.0.0:80"
```

Set up as a Windows Service for auto-start:
```powershell
sc.exe create HomeDashboard binPath="dotnet C:\inetpub\HomeDashboard\HomeDashboard.dll" start=auto
```

---

## Phase 10: Extended Integrations

**Time estimate:** Variable (ongoing)
**Difficulty:** Medium-High

### 10.1: Emby Media Control

1. Add the Emby integration in Home Assistant:
   - **Settings → Devices & Services → Add Integration → Emby**
   - Server URL: `http://10.69.1.5:8096`
   - Enter your Emby API key
2. Expose media player entities to the Blazor dashboard
3. Add a media section to the Home Control page (now playing, play/pause, browse library)
4. Control via LLM: "Play movie X on Emby"

### 10.2: AMP Game Server Status

Add a page or dashboard widget showing game server status:
- Query AMP's REST API on TrueNAS (10.69.1.3)
- Display player count, server uptime, online/offline status
- Start/stop servers from the dashboard
- LLM can answer "Is the Minecraft server running?"

### 10.3: System Health Monitoring

Add server hardware metrics to the status page:
- CPU usage, RAM, disk space via WMI/Performance Counters (Windows) or Glances (TrueNAS)
- GPU temperature and VRAM usage from `nvidia-smi`
- Docker container status
- Network throughput

### 10.4: Voice Control (After GPU Upgrade)

Once you have the 3090 Ti and faster inference:
1. Add **Whisper** (local speech-to-text) as a Docker container
2. Add **Piper** (local text-to-speech) as a Docker container
3. Integrate with the Blazor app via browser microphone API
4. Full voice loop: Speak → Whisper → Qwen3.5 → Piper → Audio response

### 10.5: Budget & Financial Tools (Future)

Add new SQL Server tables and Blazor pages for:
- Expense tracking and categorization
- Monthly budget overview with MudBlazor charts
- Recurring bill reminders
- Net worth tracking

This is a natural extension — same tech stack, just new pages and database tables.

---

## Model Recommendations

### For GTX 1070 (8GB VRAM) — Current

| Use Case | Model | Pull Command |
|----------|-------|--------------|
| **Primary / All-purpose** | Qwen3.5 9B | `ollama pull qwen3.5:9b` |
| **Code/config help** | Qwen3.5 9B (good at code) | Same model |
| **Fast fallback** | Phi-3 Mini (3.8B) | `ollama pull phi3:mini` |

**Qwen3.5 9B is the right choice for now** — strong tool use, multilingual, vision capable, and fits in 8GB VRAM at 6.6GB (Q4_K_M). The agent/tool-use capability is particularly valuable for Home Assistant integration.

### After GPU Upgrade (24GB VRAM — RTX 3090 Ti)

| Use Case | Model | Pull Command |
|----------|-------|--------------|
| **Primary / All-purpose** | Qwen3 32B | `ollama pull qwen3:32b` |
| **Best reasoning** | Llama 3.1 70B (Q4) | `ollama pull llama3.1:70b` |
| **Fast + capable** | Qwen3.5 9B | Keep as fast model |
| **RAG / Documents** | Command R+ 35B | `ollama pull command-r-plus:35b` |

Keep Qwen3.5 9B around as a fast model for simple tasks, and use the 32B+ models for complex reasoning and document analysis.

---

## Security Considerations

### Cloudflare Tunnel Security

- Cloudflare Tunnel creates outbound connections only — **no inbound ports exposed**
- All traffic is encrypted end-to-end via Cloudflare
- Consider adding Cloudflare Access (Phase 9.2) for authentication before your app
- Rate limiting is available in Cloudflare dashboard

### Application Authentication

- Implement authentication in the Blazor app (ASP.NET Identity or simple cookie-based auth)
- At minimum, require a login to access the dashboard
- The Home Assistant long-lived token is sensitive — store it in user secrets or environment variables, never in source code or appsettings.json in production

### Network Access

- **Do NOT expose Ollama port (11434) externally** — only the Blazor app should talk to it
- Ollama only needs to listen on localhost/Docker network
- Restrict SQL Server to localhost connections only
- HA API access is limited to the server via the long-lived token

### LLM-Executed Actions

- The LLM can only control devices you explicitly code support for
- Validate and sanitize all LLM-generated action blocks before executing
- Log all device control actions for audit trail
- Consider a confirmation step for destructive actions ("Are you sure you want to turn off all plugs?")

### Docker Security

- Keep Ollama container updated: `docker compose pull && docker compose up -d`
- Ollama only binds to the Docker network, not exposed to WAN

---

## Maintenance & Backup

### Regular Maintenance

| Task | Frequency | Action |
|------|-----------|--------|
| Update Ollama container | Monthly | `docker compose pull && docker compose up -d` |
| Update Qwen3.5 model | As releases come | `docker exec ollama ollama pull qwen3.5:9b` |
| Update Blazor app dependencies | Monthly | `dotnet outdated` in project directory |
| SQL Server backups | Daily (automated) | SQL Agent job or scheduled task |
| Review cloudflared | Monthly | `cloudflared update` |
| Review HA integrations | Monthly | Check for disconnected devices |
| Update wiki documentation | As changes are made | Edit articles in dashboard |

### Backup Strategy

**SQL Server Database:**
```sql
-- Automated daily backup via SQL Agent or scheduled task
BACKUP DATABASE HomeDashboard
TO DISK = 'C:\Backups\HomeDashboard.bak'
WITH COMPRESSION, INIT;
```

Set up a Windows Scheduled Task to run this daily.

**Ollama Models:**
- Models can be re-downloaded — backup is optional
- If desired: `docker run --rm -v ollama_data:/data -v C:\Backups:/backup alpine tar czf /backup/ollama.tar.gz /data`

**Blazor App Source:**
- Store in a Git repository (GitHub, self-hosted Gitea, etc.)
- Push regularly

**Docker Compose:**
- Back up `C:\docker\ollama\docker-compose.yml` alongside database backups

---

## Troubleshooting

### Ollama won't use GPU

```powershell
# Check if Docker can see the GPU
docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi

# If this fails:
# 1. Update NVIDIA drivers
# 2. Ensure Docker Desktop WSL2 backend is enabled
# 3. Update WSL2: wsl --update
# 4. Enable GPU in Docker Desktop → Settings → Resources → GPU
```

### Qwen3.5 9B runs out of VRAM

- Long conversations consume more VRAM for context — restart the model or limit context length
- Check `nvidia-smi` for other processes using GPU memory
- Reduce context with `"num_ctx": 4096` in Ollama API requests (default may be higher)
- After GPU upgrade, this becomes a non-issue

### Blazor app can't reach Ollama

- Verify Ollama is running: `docker ps | grep ollama`
- Test API: `curl http://localhost:11434/api/tags`
- Check Windows Firewall isn't blocking port 11434 on localhost
- Check Ollama logs: `docker logs ollama`

### Home Assistant API returns 401

- Long-lived access token may have expired or been revoked
- Regenerate in Home Assistant: Profile → Security → Long-Lived Access Tokens
- Update the token in user secrets / app configuration

### Cloudflare Tunnel not connecting

```powershell
# Check tunnel status
cloudflared tunnel info home-dashboard

# Check service is running
Get-Service cloudflared

# View logs
cloudflared tunnel run --loglevel debug home-dashboard
```

### SQL Server connection issues

- Verify SQL Server service is running: `Get-Service MSSQLSERVER`
- Check connection string uses correct instance name
- Ensure SQL Server allows TCP/IP connections (SQL Server Configuration Manager)

### Health checks showing false negatives

- Some services may not respond to the health check URL format
- Increase the HTTP timeout in `HealthCheckService` (default 5 seconds)
- Verify the health check URL is correct for each service
- Some services require authentication for API endpoints

---

## Implementation Timeline

### Week 1: Foundation
- [ ] Phase 1: Install Ollama + pull Qwen3.5 9B + verify GPU inference (30 min)
- [ ] Phase 2: Set up SQL Server + create database schema (30-60 min)
- [ ] Phase 3: Scaffold Blazor project + MudBlazor + basic layout (2-4 hours)

### Week 2: Core Features
- [ ] Phase 4: LLM chat page with streaming responses + chat history (2-3 hours)
- [ ] Phase 5: Home Assistant integration + Ecobee setup + home control page (2-3 hours)

### Week 3: Knowledge & Monitoring
- [ ] Phase 6: Documentation wiki + RAG search integration (2-3 hours)
- [ ] Phase 7: Service status dashboard + background health checks (1-2 hours)
- [ ] Phase 8: Service links/bookmarks page (1 hour)

### Week 4: Go Live
- [ ] Phase 9: Cloudflare Tunnel + domain setup + deploy to IIS (1 hour)
- [ ] Import existing project docs into wiki
- [ ] Test all integrations end-to-end
- [ ] Polish UI, fix bugs, adjust theme

### Ongoing
- [ ] Phase 10.1: Emby media control
- [ ] Phase 10.2: AMP game server status
- [ ] Phase 10.3: System health monitoring
- [ ] Phase 10.5: Budget/financial tools

### Future: GPU Upgrade
- [ ] Install RTX 3090 Ti in R730XD
- [ ] Pull larger models (Qwen3 32B)
- [ ] Phase 10.4: Voice control (Whisper + Piper)

---

## Cost Estimate

| Item | Cost | Notes |
|------|------|-------|
| Ollama | $0 | Free, open source |
| .NET 8 / Blazor | $0 | Free, open source |
| MudBlazor | $0 | Free, open source (MIT) |
| SQL Server Express/Developer | $0 | Free for dev, Express free for production |
| Cloudflare Tunnel | $0 | Free tier includes tunnels |
| Cloudflare Access | $0 | Free for up to 50 users |
| Domain | Already owned | — |
| GPU Upgrade (RTX 3090 Ti) | $0 | Already owned (future gaming PC upgrade) |

**Total: $0** — fully leveraging existing hardware, free software, and a domain you already own.

---

## Success Metrics

| Metric | Current | Target |
|--------|---------|--------|
| Smart home control via AI | Not available | All devices controllable via chat |
| Network documentation access | Manual file reading | Instant AI-powered answers via wiki + RAG |
| Service status visibility | Manual checking | Real-time dashboard with alerts |
| Self-hosted service access | Remember individual URLs | Single dashboard with all links |
| External access | Twingate only | Custom domain via Cloudflare Tunnel |
| Cloud dependency for AI | 100% | 0% |
| Response latency (8GB GPU) | N/A | < 5 seconds for short queries |
| Response latency (24GB GPU) | N/A | < 3 seconds for short queries |

---

*Created: 2026-03-11*
*Target Server: Dell PowerEdge R730XD (10.69.1.5) — Dual 1100W PSUs*
*Current GPU: GTX 1070 (8GB VRAM)*
*Planned GPU: RTX 3090 Ti (24GB VRAM)*
*LLM Model: Qwen3.5 9B (6.6GB Q4_K_M)*
*Tech Stack: Blazor Web App + MudBlazor + SQL Server + Ollama + Cloudflare Tunnel*
*Difficulty: Low (Phases 1-2), Medium (Phases 3-9), Medium-High (Phase 10)*
*Total Time: ~15-20 hours over 4 weeks*
