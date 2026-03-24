# Sandra Maya

`.NET 8` Telegram AI assistant with Azure OpenAI function calling, web browsing, job crawling, and self-improvement capabilities. Single-container Coolify deployment.

## What Sandra Maya Can Do

Sandra Maya is a personal AI assistant available through Telegram. She can:

- **Remember things** — save notes, search past conversations, maintain context across sessions
- **Manage your CV** — accept PDF/text CV uploads, parse and store them, use them for applications
- **Search for jobs** — crawl Swiss job sites (jobs.ch, jobagent.ch, schuljobs.ch, krippenstellen.ch) using Playwright browser automation
- **Track job applications** — log which jobs you've applied to, track status (Applied → Interviewing → Offer → etc.)
- **Write cover letters** — generate AI-powered cover letters tailored to specific job postings using your CV
- **Browse the web** — navigate any website, extract content, take screenshots, search for information
- **Self-improve** — propose and install new capabilities as Node.js/Python/PowerShell scripts
- **Connect to MCP servers** — extend functionality by connecting to Model Context Protocol servers

## Architecture

The assistant uses Azure OpenAI's function calling (tool use) to bridge AI reasoning with real actions:

```
User (Telegram) → Message Router → Orchestrator → Azure OpenAI (with tools)
                                        ↕
                              Tool Registry (20+ tools)
                                        ↕
                    Memory / Jobs / Web / Capabilities / MCP
```

### Available Tools

| Category | Tools | Description |
|----------|-------|-------------|
| Memory | `memory_save_note`, `memory_search`, `memory_get_cv` | Long-term memory operations |
| CV | `cv_ingest` | Process CV from message attachments |
| Jobs | `job_list_sites`, `job_search_saved`, `job_crawl`, `job_track_application`, `job_list_applications`, `job_activity_summary` | Job search and tracking |
| Cover Letter | `cover_letter_draft` | AI-powered cover letter generation |
| Web | `web_browse`, `web_search`, `web_extract_structured`, `web_screenshot` | Playwright-based web interaction |
| Capabilities | `capability_list`, `capability_propose`, `capability_execute` | Self-improvement lifecycle |
| MCP | `mcp_list_servers`, `mcp_add_server`, `mcp_remove_server` | MCP server management |

## Project Layout

- `src/SandraMaya.Application` — domain models, contracts, and abstractions
- `src/SandraMaya.Infrastructure` — SQLite persistence, file storage, PDF conversion
- `src/SandraMaya.Capabilities` — capability registry, execution, and activity tracking
- `src/SandraMaya.Host` — ASP.NET Core host, orchestrator, tool handlers, Playwright integration

## Configuration

### Required

| Variable | Description |
|----------|-------------|
| `Telegram__BotToken` | Telegram Bot API token |
| `AzureOpenAi__BaseUrl` | Azure OpenAI endpoint URL |
| `AzureOpenAi__ApiKey` | Azure OpenAI API key |
| `AzureOpenAi__DeploymentName` | Model deployment name (e.g., `gpt-4o`) |

### Optional

| Variable | Default | Description |
|----------|---------|-------------|
| `Storage__Root` | `App_Data` (local), `/data` (container) | Root storage path |
| `AzureOpenAi__ProviderType` | `azure` | Provider type |
| `AzureOpenAi__ApiVersion` | `2024-10-21` | API version |
| `Runtime__NodeCommand` | `node` | Node.js binary path |
| `Runtime__PythonCommand` | `python` | Python binary path |
| `Runtime__PlaywrightCommand` | `node` | Playwright CLI path |

Storage paths are resolved from `Storage__Root`:
- `Storage__SqlitePath` — SQLite database
- `Storage__UploadsPath` — uploaded files
- `Storage__CapabilitiesPath` — capability registry
- `Storage__GeneratedCapabilitiesPath` — AI-generated capability scripts
- `Storage__WorkPath` — working directory for Playwright scripts
- `Storage__TempPath` — temporary files

## Run Locally

```powershell
dotnet restore .\src\SandraMaya.Host\SandraMaya.Host.csproj
dotnet run --project .\src\SandraMaya.Host\SandraMaya.Host.csproj
```

The host can start without `Telegram__BotToken`; Telegram polling will be skipped and `/health` will report degraded.

## Docker / Coolify

```bash
docker build \
  --build-arg PROJECT_PATH=src/SandraMaya.Host/SandraMaya.Host.csproj \
  -t sandra-maya .
```

- Single container: ASP.NET Core + Telegram polling + Node.js + Python + Playwright Chromium
- Mount a persistent volume at `/data` for SQLite, uploads, and capabilities
- Playwright browsers are installed by default (`INSTALL_PLAYWRIGHT_BROWSERS=true`)
- Health check: `GET /health` on port `8080`
- Set `INSTALL_PLAYWRIGHT_BROWSERS=false` at build time to skip browser installation (disables web browsing tools)

## Self-Improvement

Sandra Maya can extend herself by:

1. Recognizing a need the AI can't fulfill with existing tools
2. Proposing a new capability (Node.js, Python, or PowerShell script)
3. Registering the capability in the registry
4. Executing it on future requests

**Safety tiers:**
- **Auto-enable**: Capabilities with `local-readonly` containment (no network, no writes)
- **Approval required**: Capabilities needing `networked` or `elevated` containment — Sandra Maya asks the user via Telegram before first execution

