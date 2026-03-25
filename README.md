# Maya

`.NET 10` Telegram AI assistant built on the GitHub Copilot SDK, with web browsing, job crawling, MCP integration, and SDK skill discovery. Single-container Coolify deployment.

## What Maya Can Do

Maya is a personal AI assistant available through Telegram. She can:

- **Remember things** — save notes, search past conversations, maintain context across sessions
- **Manage your CV** — accept PDF/text CV uploads, parse and store them, use them for applications
- **Search for jobs** — crawl Swiss job sites (jobs.ch, jobagent.ch, schuljobs.ch, krippenstellen.ch) using Playwright browser automation
- **Track job applications** — log which jobs you've applied to, track status (Applied → Interviewing → Offer → etc.)
- **Write cover letters** — generate AI-powered cover letters tailored to specific job postings using your CV
- **Browse the web** — navigate any website, extract content, take screenshots, search for information
- **Use SDK skills** — discover additional workflow skills from configured Copilot SDK skill directories
- **Connect to MCP servers** — extend functionality by connecting to Model Context Protocol servers

## Architecture

The assistant uses the GitHub Copilot SDK to bridge agent reasoning with real actions:

```
User (Telegram) → Message Router → Copilot SDK Orchestrator → Copilot CLI runtime
                                                ↕
                                          Tool Registry
                                                ↕
                            Memory / Jobs / Web / MCP + SDK skills
                                                ↕
                       GitHub-authenticated model or Azure/OpenAI BYOK provider
```

### Available Tools

| Category | Tools | Description |
|----------|-------|-------------|
| Memory | `memory_save_note`, `memory_search`, `memory_get_cv` | Long-term memory operations |
| CV | `cv_ingest` | Process CV from message attachments |
| Jobs | `job_list_sites`, `job_search_saved`, `job_crawl`, `job_track_application`, `job_list_applications`, `job_activity_summary` | Job search and tracking |
| Cover Letter | `cover_letter_draft` | AI-powered cover letter generation |
| Web | `web_browse`, `web_search`, `web_extract_structured`, `web_screenshot` | Playwright-based web interaction |
| MCP | `mcp_list_servers`, `mcp_add_server`, `mcp_remove_server` | MCP server management |

## Project Layout

- `src/SandraMaya.Application` — domain models, contracts, and abstractions
- `src/SandraMaya.Infrastructure` — SQLite persistence, file storage, PDF conversion
- `src/SandraMaya.Host` — ASP.NET Core host, orchestrator, tool handlers, Playwright integration

## Configuration

### Required

Pick one AI runtime mode:

- **GitHub-authenticated Copilot mode** — set `CopilotRuntime__Model`
- **BYOK mode** — set the `AzureOpenAi__*` values below so the Copilot SDK uses your Azure-hosted deployment

| Variable | Description |
|----------|-------------|
| `Telegram__BotToken` | Telegram Bot API token |
| `CopilotRuntime__Model` | Model name for GitHub-authenticated Copilot sessions (for example `gpt-5-mini`) |
| `AzureOpenAi__BaseUrl` | Azure OpenAI endpoint URL |
| `AzureOpenAi__ApiKey` | Azure OpenAI API key |
| `AzureOpenAi__DeploymentName` | Azure deployment/model name used by Copilot SDK BYOK |

### Optional

| Variable | Default | Description |
|----------|---------|-------------|
| `Storage__Root` | `App_Data` (local), `/data` (container) | Root storage path |
| `CopilotRuntime__CliPath` | empty | Optional path to the `copilot` CLI executable |
| `CopilotRuntime__GitHubToken` | empty | Optional GitHub token for Copilot-authenticated sessions |
| `CopilotRuntime__UseLoggedInUser` | `true` | Whether to use the logged-in `copilot`/GitHub CLI identity |
| `CopilotRuntime__LogLevel` | `info` | Copilot SDK CLI log level |
| `CopilotRuntime__WorkingDirectory` | app root | Working directory passed to Copilot sessions |
| `CopilotRuntime__ClientName` | `Sandra Maya` | Client name reported to the Copilot runtime |
| `AzureOpenAi__ProviderType` | `azure` | Provider type |
| `AzureOpenAi__ApiVersion` | `2024-10-21` | API version |
| `AzureOpenAi__WireApi` | empty | Optional BYOK wire API (for example `responses`) |
| `Runtime__PlaywrightCommand` | `node` | Playwright CLI path |

Storage paths are resolved as follows:
- `Storage__SqlitePath` — SQLite database
- `Storage__UploadsPath` — uploaded files
- `Storage__RuntimeSkillsPath` — SDK skill root discovered by the Copilot SDK, resolved from the host content root
- `Storage__WorkPath` — working directory for Playwright scripts
- `Storage__TempPath` — temporary files

## Run Locally

```powershell
# Restore and run the host (Windows PowerShell)
dotnet restore .\src\SandraMaya.Host\SandraMaya.Host.csproj
dotnet run --project .\src\SandraMaya.Host\SandraMaya.Host.csproj
```

Before running crawlers or web-based tools, install Playwright and browsers in the repository root:

- Windows (PowerShell):

```powershell
.\scripts\install-playwright.ps1
```

- Linux / macOS (bash):

```bash
bash ./scripts/install-playwright.sh
```

The host can start without `Telegram__BotToken`; Telegram polling will be skipped and `/health` will report degraded.

## Chat CLI (`sandra-chat`)

`sandra-chat` is a local CLI tool that lets an AI agent or a human have a live conversation with Maya **without a real Telegram account**. It starts a lightweight mock Telegram Bot API server and launches the real bot pointed at it. Each command is a one-shot subprocess — ideal for AI agent automation.

### Build

```powershell
dotnet build .\tools\SandraMaya.ChatCli\SandraMaya.ChatCli.csproj
```

The binary is output to `tools/SandraMaya.ChatCli/bin/Debug/net10.0/sandra-chat.exe`.

### Quick start

```powershell
# 1a. GitHub-authenticated Copilot mode
$env:CopilotRuntime__Model = "gpt-5-mini"

# 1b. Or configure Azure BYOK for the Copilot SDK instead
#     `sandra-chat start` launches the host in Development so dotnet user-secrets are loaded.
$env:AzureOpenAi__BaseUrl = "https://your-resource.openai.azure.com/"
$env:AzureOpenAi__ApiKey  = "your-key"
$env:AzureOpenAi__DeploymentName = "gpt-4o"

# 2. Start the session (from the repo root)
.\tools\SandraMaya.ChatCli\bin\Debug\net10.0\sandra-chat.exe start

# 3. Send a message and get the bot's reply
.\tools\SandraMaya.ChatCli\bin\Debug\net10.0\sandra-chat.exe send "What jobs are available in Zurich?"

# 4. Get structured JSON output (useful for AI agent parsing)
.\tools\SandraMaya.ChatCli\bin\Debug\net10.0\sandra-chat.exe send "Remember my name is Alice" --json

# 5. Review the full conversation
.\tools\SandraMaya.ChatCli\bin\Debug\net10.0\sandra-chat.exe history

# 6. Stop everything
.\tools\SandraMaya.ChatCli\bin\Debug\net10.0\sandra-chat.exe stop
```

### Commands

| Command | Description |
|---------|-------------|
| `start [--port N] [--chat-id N] [--user NAME] [--bot-project PATH]` | Start mock server + bot, write session state |
| `send <message> [--json] [--timeout N]` | Send one message, wait for reply, print and exit |
| `stop` | Shut down bot and mock server |
| `status [--json]` | Show running state |
| `history [--json]` | Print full conversation log |

### How it works

```
sandra-chat start
  └─ spawns: sandra-chat serve --port <N>   (mock Telegram API + management API)
  └─ spawns: dotnet run SandraMaya.Host     (real bot, Telegram__ApiBaseUrl=http://localhost:<N>/)

sandra-chat send "hello"
  └─ POST /cli/send → server enqueues fake Telegram update
       → bot long-polls getUpdates → processes message → calls sendMessage
       → server captures reply → returns to CLI
  └─ CLI prints reply text, exits 0

sandra-chat stop
  └─ POST /cli/shutdown + kill PIDs from ~/.sandra-maya-chat/session.json
```

### For AI agents

Each `send` call returns only the bot reply on stdout (or a JSON envelope with `--json`):

```
# Plain text — capture as a variable
$reply = .\sandra-chat.exe send "What is 2+2?"
# JSON — parse with ConvertFrom-Json
$result = .\sandra-chat.exe send "Summarise my CV" --json | ConvertFrom-Json
Write-Host $result.reply
Write-Host "Took $($result.elapsed_ms) ms"
```

## Docker / Coolify

```bash
docker build \
  --build-arg PROJECT_PATH=src/SandraMaya.Host/SandraMaya.Host.csproj \
  -t sandra-maya .
```

- Single container: ASP.NET Core + Telegram polling + Node.js + Python + Playwright Chromium
- Mount a persistent volume at `/data` for SQLite, uploads, and SDK skill folders
- Playwright browsers are installed by default (`INSTALL_PLAYWRIGHT_BROWSERS=true`)
- Health check: `GET /health` on port `8080`
- Set `INSTALL_PLAYWRIGHT_BROWSERS=false` at build time to skip browser installation (disables web browsing tools)

## SDK Skills

Maya can load additional Copilot SDK skills from configured skill directories.

- By default, put each skill in its own folder under `src\SandraMaya.Host\Assistant\Skills\` (or the path configured by `Storage__RuntimeSkillsPath`)
- Keep `SKILL.md` and any scripts/assets together inside that folder
- The host preserves SDK `SkillDirectories` support so the Copilot SDK can discover those folders directly
- Generic helper tools for listing, saving, or executing skills are intentionally not part of the runtime surface


