# Vasquez History

## Core Context
- Requested by Gianluigi Conti.
- Project: Sandra Maya, a Copilot SDK-first AI assistant for job search workflows over Telegram.
- Stack: .NET 10, ASP.NET Core host, GitHub Copilot SDK, SQLite, Playwright, Telegram.
- Boundary: tests, evals, regressions, review approval or rejection.

## Learnings
- Quality is a first-class boundary, not an afterthought or a shared leftover responsibility.
- Custom agent toolsets should only list tools that exist in the ToolRegistry; dead references cause SDK failures.
- Tests covering agent config structure (`CopilotCustomAgentProfilesTests`) validate tool names but don't cover runtime SDK availability; integration coverage is a future milestone.
- Delegation prompts belong in `CopilotCustomAgentProfiles`; `SystemPromptBuilder` embeds them into the main prompt.
