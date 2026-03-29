# Bishop History

## Core Context
- Requested by Gianluigi Conti.
- Project: Sandra Maya, a Copilot SDK-first AI assistant for job search workflows over Telegram.
- Stack: .NET 10, ASP.NET Core host, GitHub Copilot SDK, SQLite, Playwright, Telegram.
- Boundary: Copilot SDK runtime, skills, tools, agents, subagents, MCP, prompt and session orchestration.

## Learnings
- The team expects current knowledge of GitHub Copilot SDK primitives and AI coding agent patterns.
- Hardcoded tool names in agent configs are a known maintenance point; tool renames are rare but should be coordinated.
- Prompt duplication (MainAssistantDelegationSection repeated per turn) is intentional for boundary reinforcement in infinite sessions.
- Custom agent config allocation on session init is negligible overhead; premature caching adds complexity without benefit.
- Key files: CopilotCustomAgentProfiles.cs (agent registry), SystemPromptBuilder.cs (prompt composition), CopilotSdkAssistantOrchestrator.cs (session orchestration).
- ToolRegistry is the single source of truth for tool handlers; agent tool lists should ideally reference handler metadata, but hardcoding is acceptable for now given team coordination.
