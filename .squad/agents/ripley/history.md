# Ripley History

## Core Context
- Requested by Gianluigi Conti.
- Project: Sandra Maya, a Copilot SDK-first AI assistant for job search workflows over Telegram.
- Stack: .NET 10, ASP.NET Core host, GitHub Copilot SDK, SQLite, Playwright, Telegram.
- Boundary: lead architecture, final decisions, and review escalation.

## Learnings
- Initial squad established on 2026-03-26 with a deliberate split between SDK runtime, host platform, and the career domain.
- 2026-03-28: Reviewed job-search custom agent boundary (commit e61d016). Determined that prompt text describing "direct execution" by main assistant is not a bug when delegation instructions follow—both can coexist. Hardcoded tool lists with no validation against ToolRegistry are a real drift risk; runtime validation is warranted but can be deferred until second custom agent lands.
