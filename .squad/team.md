# Sandra Maya Squad

## Project Context

- User: Gianluigi Conti
- Product: Sandra Maya is a Copilot SDK-first AI assistant for job search workflows that interacts over Telegram.
- Stack: .NET 10, ASP.NET Core host, GitHub Copilot SDK, SQLite, Playwright, Telegram, local runtime skills.
- Goal: deliver top-class product development with current knowledge of GitHub Copilot SDK primitives such as skills, tools, agents, subagents, MCP integrations, and runtime orchestration.

## Members

| Name | Role | Ownership | Badge |
| --- | --- | --- | --- |
| Ripley | Lead | Architecture boundaries, final decisions, review gate | 🏗️ Lead |
| Bishop | Copilot SDK Runtime | Skills, tools, agents, subagents, MCP, prompt/session/runtime design | 🤖 Runtime |
| Apone | Host & Platform | ASP.NET Core host, Telegram wiring, DI, configuration, secrets, health, deployment wiring | ⚙️ Platform |
| Hicks | Career Domain | Job discovery, ingestion, tracking, applications, memory, CVs, cover letters, and document flows | 🔧 Domain |
| Vasquez | Quality | Tests, evals, regressions, review approval or rejection | 🧪 Reviewer |
| Scribe | Session Logger | Decisions ledger, orchestration logs, cross-agent context sharing | 📋 Logger |
| Ralph | Work Monitor | Work queue, backlog scanning, keep-alive monitoring | 🔄 Monitor |

## Boundaries

- Keep Copilot SDK runtime work inside Bishop's boundary unless a cross-cutting architectural decision is required.
- Keep host, startup, configuration, and operations work inside Apone's boundary.
- Keep the full job-search and user-artifact domain inside Hicks' boundary to avoid a split product brain.
- Route reviewer and quality verdicts through Vasquez.
- Route architecture and boundary changes through Ripley.
