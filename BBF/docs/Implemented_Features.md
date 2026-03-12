# BBF — Implemented Features

## Overview

BBF is a custom Blazor Web App serving as a unified home dashboard with local AI chat capabilities. It runs on a Dell PowerEdge R730XD (Windows Server 2022) with a GTX 1070 GPU for local LLM inference.

---

## Tech Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | .NET | 10.0 |
| Frontend | Blazor Web App (Interactive Server) | - |
| UI Library | MudBlazor | 9.1.0 |
| Database | SQL Server 2025 | - |
| ORM | Entity Framework Core | 10.0.4 |
| Auth | ASP.NET Core Identity | 10.0.4 |
| LLM Engine | Ollama (Docker, GPU) | latest |
| LLM Model | Qwen3.5 9B (Q4_K_M) | - |
| Web Search | SearXNG (Docker) | latest |
| Markdown | Markdig | 1.1.1 |

---

## Completed Phases

### Phase 1: Ollama LLM Runtime
- Ollama running in Docker with NVIDIA GPU passthrough (`runtime: nvidia`)
- Qwen3.5 9B model loaded (~6.4GB of 8GB VRAM)
- Accessible at `http://10.69.1.5:11434`
- Previous WSL Ollama installation removed to avoid port conflicts

### Phase 2: SQL Server Setup
- SQL Server 2025 installed on Windows Server 2022
- Database: `BBFSite` with SQL authentication
- Remote access enabled via TCP/IP + firewall rule (restricted to 10.69.1.0/24)
- Old SQL Server instance removed prior to installation

### Phase 3: Blazor Web App — Foundation
- .NET 10 Blazor Web App with Interactive Server rendering
- MudBlazor 9.1.0 Material Design UI
- ASP.NET Core Identity (login, register, 2FA, passkeys)
- Dark/light theme toggle (defaults to dark)
- Responsive sidebar navigation with drawer
- Credentials stored in .NET User Secrets (repo-safe for public visibility)
- Custom 404 page

**Database Entities:**
- `ChatConversation` — conversation containers with model tracking
- `ChatMessage` — individual messages (user/assistant roles)
- `ServiceLink` — bookmarkable service links with health check URLs
- `WikiArticle` — knowledge base articles with categories and tags
- `ServiceHealthLog` — health check history with response times
- `AppSetting` — key-value application settings

### Phase 4: LLM Chat Integration
- Full chat page at `/chat` and `/chat/{id}`
- Streaming responses from Ollama (real-time token display)
- Conversation sidebar with search/filter and delete
- New conversation creation with auto-titling from first message
- Model selector populated from Ollama's available models
- Stop button to cancel generation mid-stream (partial response saved)
- Enter to send, Shift+Enter for newlines
- Conversation persistence to SQL Server
- Markdown rendering in assistant responses (Markdig)
  - Bold, italic, lists, code blocks, tables, blockquotes, links, headings
  - Styled for both dark and light themes
- Error handling for Ollama connectivity issues

**Ollama Configuration:**
- `think: false` — thinking mode disabled (9B model loops with it enabled)
- `num_ctx: 8192` — context window
- `repeat_penalty: 1.2` — reduces repetition
- `temperature: 0.7, top_p: 0.8, top_k: 20` — Qwen recommended non-thinking sampling
- System prompt: general-purpose assistant, honest about knowledge gaps

### Web Search Integration (SearXNG)
- SearXNG self-hosted meta search engine running in Docker
- Accessible at `http://10.69.1.5:8888` with JSON API enabled
- Toggle button in chat toolbar (on by default)
- Searches web for user's query before sending to LLM
- Top 5 results injected as context into the prompt
- "Searching the web..." indicator during search phase
- Graceful fallback if SearXNG is unreachable
- Eliminates hallucination for factual queries

---

## Dashboard Page

The home page (`/`) displays:
- Ollama health status (online/offline indicator)
- Conversation count
- Active service count
- Published wiki article count
- Quick links from ServiceLinks table
- Available Ollama models list

---

## Infrastructure

### Docker Containers (on Windows Server 2022)
1. **Ollama** — port 11434, GPU passthrough, `ollama_data` volume
2. **SearXNG** — port 8888, bind mount to `C:\docker\searxng\config`

### Firewall Rules
- SQL Server (1433/TCP) — restricted to 10.69.1.0/24
- SearXNG (8888/TCP) — restricted to 10.69.1.0/24

---

## Remaining Phases

- **Phase 5:** Home Assistant integration (TP-Link plugs, Ecobee, Dreo fan, cameras)
- **Phase 6:** Documentation wiki with RAG-powered search
- **Phase 7:** Service status dashboard with background health checks
- **Phase 8:** Service links and bookmarks management page
- **Phase 9:** Cloudflare Tunnel + domain configuration + IIS deployment
- **Phase 10:** Extended integrations (Emby, AMP, voice control, budget tools)

---

*Last Updated: 2026-03-12*
