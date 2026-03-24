# Sandra Maya

`.NET`-first Telegram assistant host foundation for a single-container Coolify deployment.

## What is included

- ASP.NET Core host with DI and health checks
- configuration binding for Telegram and Azure OpenAI BYOK settings
- Telegram long-polling intake
- update routing and inbound message mapping abstractions
- placeholder assistant orchestrator with in-memory session tracking
- outbound Telegram message dispatcher
- SQLite-backed memory foundation for users, assistant profile state, uploaded assets, CV revisions, Markdown documents, structured profile snapshots, and normalized job postings
- job crawling foundation with a site registry, crawl strategy abstractions, and import-ready normalization for `jobs-ch`, `jobagent-ch`, `schuljobs-ch`, and `krippenstellen-ch`
- local file storage abstraction for uploads and generated artifacts plus a pluggable PDF-to-Markdown conversion service

## Project layout

- `src\SandraMaya.Application` — domain models, contracts, and memory/file-ingestion abstractions
- `src\SandraMaya.Infrastructure` — SQLite persistence, file storage, PDF conversion, and retrieval services
- `src\SandraMaya.Host` — main host/orchestrator service

## Configuration

Set these environment variables before running:

- `Telegram__BotToken`
- `AzureOpenAi__BaseUrl`
- `AzureOpenAi__ApiKey`
- `AzureOpenAi__DeploymentName`
- `Storage__Root` (defaults to `App_Data` locally, use `/data` in containers)

Optional Azure settings:

- `AzureOpenAi__ProviderType` (`azure` by default)
- `AzureOpenAi__ApiVersion` (`2024-10-21` by default for native Azure hosts)
- `AzureOpenAi__WireApi` (`responses` for GPT-5-style OpenAI-compatible endpoints)

Storage paths are resolved from `Storage__Root` unless an absolute override is supplied:

- `Storage__SqlitePath` (default `sqlite/sandra-maya.db`)
- `Storage__UploadsPath` (default `files`)
- `Storage__CapabilitiesPath` (default `capabilities`)
- `Storage__GeneratedCapabilitiesPath` (default `capabilities/generated`)
- `Storage__WorkPath` (default `work`)
- `Storage__TempPath` (default `tmp`)
- `Storage__CapabilityRegistryFileName` (default `capability-registry.json`)

The memory/document store and file-ingestion pipeline use this same `Storage__*` tree; there is no separate `Memory__*` or `FileStorage__*` path configuration.

Capability execution plans also use configurable runtime commands:

- `Runtime__DotNetCommand` (default `dotnet`)
- `Runtime__NodeCommand` (default `node`)
- `Runtime__PlaywrightCommand` (default `node`)
- `Runtime__PythonCommand` (default `python`)
- `Runtime__PowerShellCommand` (default `pwsh`)
- `Runtime__BashCommand` (default `bash`)

## Run

```powershell
dotnet restore .\src\SandraMaya.Host\SandraMaya.Host.csproj
dotnet run --project .\src\SandraMaya.Host\SandraMaya.Host.csproj
```

Health endpoint:

- `GET /health`

## Docker / Coolify

- Build with `docker build --build-arg PROJECT_PATH=src/SandraMaya.Host/SandraMaya.Host.csproj -t sandra-maya .`
- The image is single-container: ASP.NET Core host, Telegram polling worker, Node.js, Python, and optional Playwright browsers all run in one container.
- Mount a persistent Coolify volume at `/data` for SQLite, uploads, capability registry data, generated capabilities, work files, and temp files.
- The host bootstraps the storage tree on startup, creates the SQLite file if it does not exist yet, and stores the capability registry under `Storage__CapabilitiesPath`.
- Override `Runtime__*Command` only when your deployment needs non-default binary names or wrappers.
- Set `INSTALL_PLAYWRIGHT_BROWSERS=true` at build time only when browser automation is required.
- Container health is available at `GET /health` on port `8080`.

Current orchestrator behavior is intentionally a placeholder. It accepts Telegram text/files, creates or reuses an in-memory assistant session, and sends a diagnostic reply so future Copilot SDK/Azure orchestration can be plugged in cleanly.

