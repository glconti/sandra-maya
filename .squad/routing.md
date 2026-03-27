# Squad Routing

## Primary routes

| Signal | Route | Why |
| --- | --- | --- |
| Copilot SDK runtime, skills, tools, agents, subagents, MCP, session orchestration | Bishop | Keeps SDK-native behavior in one boundary |
| ASP.NET Core host, Telegram, DI, startup, config, secrets, health, deployment wiring | Apone | Keeps platform and runtime plumbing together |
| Job crawling, ingestion, job tracking, applications, memory, CVs, cover letters, document flows | Hicks | Consolidates one career-domain bounded context |
| Tests, evals, regressions, review gates | Vasquez | Single reviewer and quality owner |
| Boundary changes, scope conflicts, architecture decisions, reviewer escalations | Ripley | Single final decision-maker |
| Decision merges, logs, cross-agent context updates | Scribe | Preserves append-only shared state |
| Backlog scans, issue pickup loops, keep-working mode | Ralph | Keeps work moving without blocking |

## Default pairings

- Bishop + Apone: when a change spans SDK runtime and host wiring.
- Apone + Hicks: when platform integration affects domain behavior.
- Hicks + Vasquez: when domain work needs tests or a review gate.
- Ripley + Vasquez: when a reviewer rejection or boundary conflict needs escalation.

## Routing rule

Prefer the smallest owner set that preserves clean boundaries. If a task can stay within one bounded context, route it to that single owner first.
