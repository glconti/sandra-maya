# Squad Decisions

This file is append-only. New decisions should be added as new dated entries.

### 2026-03-26: Initial squad created
- User: Gianluigi Conti
- Product context: Sandra Maya is a Copilot SDK-first AI assistant for job search workflows over Telegram.
- Team roster approved: Ripley, Bishop, Apone, Hicks, Vasquez, Scribe, Ralph.

### 2026-03-26: Bounded contexts are intentionally narrow
- Bishop owns Copilot SDK runtime concerns: skills, tools, agents, subagents, MCP, and prompt or session runtime design.
- Apone owns host and platform concerns: ASP.NET Core host, Telegram integration, DI, config, secrets, health, and deployment wiring.
- Hicks owns the complete career-domain surface: job discovery, ingestion, tracking, applications, memory, CVs, cover letters, and document flows.

### 2026-03-26: Reviewer and decision gates
- Vasquez is the quality reviewer and can approve, reject, or force reassignment.
- Ripley owns architecture boundaries and final cross-context decisions.
