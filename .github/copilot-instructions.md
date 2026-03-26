# Sandra Maya Copilot Instructions

## Project summary

- Sandra Maya is a Copilot SDK-first personal AI assistant that interacts with the user over Telegram.

- The product goal is not just chat. Maya should remember user context, help with job-search workflows, and improve itself over time by gaining new capabilities through tools, skills, MCP integrations, and other Copilot SDK-native extension points.

## Build, test, and lint commands

- Restore and run the host from the repo root:

  ```powershell
  dotnet restore .\src\SandraMaya.Host\SandraMaya.Host.csproj
  dotnet build .\src\SandraMaya.Host\SandraMaya.Host.csproj --no-restore
  dotnet run --project .\src\SandraMaya.Host\SandraMaya.Host.csproj
  ```

- Install Playwright before using crawler or web tools:

  ```powershell
  .\scripts\install-playwright.ps1
  ```

- Build the local chat CLI used for end-to-end bot testing:

  ```powershell
  dotnet build .\tools\SandraMaya.ChatCli\SandraMaya.ChatCli.csproj
  ```

- Run the full automated test suite:

  ```powershell
  dotnet test .\SandraMaya.slnx --no-restore --nologo
  ```

- Run a single test method without cross-project filter noise by targeting the test project directly:

  ```powershell
  dotnet test .\tests\SandraMaya.Host.Tests\SandraMaya.Host.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName=SandraMaya.Host.Tests.StorageLayoutTests.Create_ResolvesRuntimeSkillsPathFromContentRoot"
  ```

- Run a whole test class:

  ```powershell
  dotnet test .\tests\SandraMaya.Host.Tests\SandraMaya.Host.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~SandraMaya.Host.Tests.StorageLayoutTests"
  ```

- There is no dedicated lint script or separate lint command in the repository. `Directory.Build.props` enables .NET analyzers, so `dotnet build` / `dotnet test` are the analyzer-backed validation paths.

## High-level architecture

- The solution is split into three main projects:
  - `src/SandraMaya.Application`: domain contracts, abstractions, and models for memory, CV ingestion, job crawling, job tracking, and cover-letter generation.
  - `src/SandraMaya.Infrastructure`: SQLite-backed persistence, local file storage, PDF conversion, job crawling foundations, and concrete application services.
  - `src/SandraMaya.Host`: the ASP.NET Core host that wires Telegram, the GitHub Copilot SDK runtime, Playwright-based web tools, MCP management, and tool handlers.

- The main runtime flow is: Telegram update handling / inbound routing -> `CopilotSdkAssistantOrchestrator` -> Copilot SDK session -> DI-registered tool handlers -> application/infrastructure services.

- `ServiceCollectionExtensions` is the composition root. It binds options, constructs `StorageLayout`, registers infrastructure services through `AddMemoryFoundation`, registers every `IToolHandler`, and swaps the infrastructure crawl-strategy placeholders for host implementations (`HostPlaywrightJobCrawlStrategy`, `HostScriptedHttpJobCrawlStrategy`).

- `CopilotSdkAssistantOrchestrator` is the key bridge to the Copilot SDK. It resolves the user, builds the system prompt, opens or resumes infinite sessions per conversation, exposes the registered tools to the SDK, and points the runtime at the configured skill directories.

- `SystemPromptBuilder` is part of the behavior architecture, not just prompt text. It appends the assistant persona, repo authoring areas, the currently available tools, and user-specific context such as CV presence, saved job postings, and tracked applications on every turn.

- Storage has two important roots:
  - `StorageLayout.Root` defaults to `App_Data` for SQLite, uploads, work files, and temp files.
  - `StorageLayout.RuntimeSkillsPath` is resolved from the host content root, not from `App_Data`, and defaults to `src/SandraMaya.Host/Assistant/Skills` via configuration + startup path resolution.

- `StorageBootstrapService` creates the storage directories and SQLite file at startup, including the runtime skills directory, so skill discovery is designed around normal folders on disk.

## Key conventions

- Be Copilot SDK first. When adding or changing behavior, try to map the feature to native Copilot SDK concepts before inventing custom layers: sessions, skills, tools, agents, modes, MCP integration, and related SDK primitives should be the first design surface.

- Prefer the Copilot SDK skill model directly. Runtime skills live as folders under `src/SandraMaya.Host/Assistant/Skills`, with `SKILL.md` and any scripts/assets kept together in the same folder. Do not add extra registries, catalogs, or shadow metadata layers for skills unless there is a concrete limitation that forces it.

- Because the Copilot SDK and its .NET surface evolve quickly, if you are not sure whether a capability already exists natively, research the latest .NET API and feature set with Context7 before designing custom abstractions.

- Use repo-relative paths when referring to authoring areas in prompts or instructions. The canonical repo areas are:
  - `src/SandraMaya.Host/Assistant/Skills`
  - `src/SandraMaya.Host/Playwright`
  - `src/SandraMaya.Host/Program.cs`
  - `src/SandraMaya.Host/Configuration`

- When a skill, crawler, or external workflow discovers jobs, route persistence through `jobs_ingest_batch` / `IJobCrawlIngestionService` instead of writing directly to SQLite. Deduplication, normalization, and ingestion invariants are centralized there.

- New assistant capabilities usually surface as `IToolHandler` implementations registered in DI. Tool handlers accept JSON arguments plus `ToolExecutionContext`, and JSON responses should go through `ToolResult.Json(...)`, which uses the shared camelCase serializer options defined with the tool-calling infrastructure.

- Do not hardcode storage paths. Use `StorageLayout` and the bound `Storage` options so the same code works with local `App_Data`, container `/data`, and alternate configured roots.

- Telegram is optional at startup. If `Telegram:BotToken` is not configured, the host still runs and exposes `/health`; only Telegram polling is skipped.

- The repo targets `.NET 10`, uses nullable reference types and implicit usings globally, and relies on xUnit plus AwesomeAssertions in tests.
