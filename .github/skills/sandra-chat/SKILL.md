---
name: sandra-chat
description: 'Conducts live, stateful conversations with a locally running Maya bot for testing and verification. Use when asked to test the bot, verify bot responses, check conversation flows, test job search features, validate bot behaviour, or interact with Maya during development.'
---

# Interacting with Maya bot via sandra-chat

This repository skill is a **development-time testing aid for Copilot CLI**.
It helps you run and verify the local Maya bot during development.
It is **not** one of Maya's runtime skills.
Maya runtime skills are discovered by the Copilot SDK from configured
`SkillDirectories`, not from helper tools or files under `.github\skills\`.

Use the `sandra-chat` CLI directly from the shell tool to start a local bot session, send messages, inspect replies, and tear down cleanly.
The CLI does **not** use the real Telegram API — it runs a local mock server so no real account or token is needed.

If `sandra-chat` is not on `PATH`, call the built binary directly:

- Windows: `.\tools\SandraMaya.ChatCli\bin\Debug\net10.0\sandra-chat.exe`
- macOS/Linux: `./tools/SandraMaya.ChatCli/bin/Debug/net10.0/sandra-chat`

Prefer `sandra-chat` in examples below for readability.

## Quick start

```bash
sandra-chat start --user agent
sandra-chat send "Hello Maya!"
sandra-chat send "What jobs are available in Zurich?"
sandra-chat history --json
sandra-chat stop
```

## When to use this skill

- The user asks to test Maya end-to-end through the local bot runtime
- The user wants to verify bot replies or conversation flow without real Telegram
- The user wants to run a live multi-turn chat against the local bot during development
- The user wants to verify Maya's runtime tools or SDK-discovered skills by talking to the bot itself
- Keywords: maya, sandra-chat, telegram mock, bot testing, conversation flow, local bot

## Prerequisites

This repository requires the .NET 10 SDK pinned in `global.json` before the CLI can be built.

```powershell
dotnet --version
```

The CLI binary must be built before first use:

```powershell
dotnet build .\tools\SandraMaya.ChatCli\SandraMaya.ChatCli.csproj
```

## Workflow

1. Start the session once with `sandra-chat start`.
2. Send one or more messages with `sandra-chat send "..."`.
3. Inspect the transcript with `sandra-chat history --json` when needed.
4. Stop the session with `sandra-chat stop`.

Use `status --json` if you need to check whether a session is already running before starting a new one.

## Running without model configuration

`sandra-chat` can still be used when Maya has no Copilot runtime configured.
In that case, the bot starts normally and replies with the explicit fallback message
that the Copilot runtime is not configured. This is useful for testing bot startup,
routing, session lifecycle, and the mock Telegram plumbing without real credentials.

## Commands

### `sandra-chat start`

Launches the mock Telegram API server and the real Maya bot subprocess.
Call this **once** at the beginning of a test session.

```bash
sandra-chat start [--port N] [--chat-id N] [--user NAME] [--bot-project PATH]
```

> **Note**: First start can take 20–60 seconds while `dotnet run` compiles and boots the bot.

### `sandra-chat send`

Sends one message and waits for the bot's reply. Returns plain text.

```bash
sandra-chat send "<message>" [--json] [--timeout N]
```

Use `--json` when you want structured output for parsing.

### `sandra-chat stop`

Shuts down the bot subprocess and mock server. Clears session state.
Always call this when done to avoid orphaned processes.

```bash
sandra-chat stop
```

### `sandra-chat status`

Returns session state as JSON: port, PIDs, start time, username.
Use this to check whether a session is already running before calling `sandra-chat start`.

```bash
sandra-chat status --json
```

### `sandra-chat history`

Returns the full conversation log as a JSON array of `{ role, text, at }` entries.
Use this to verify the complete exchange after a test run.

```bash
sandra-chat history --json
```

## Example: Verify job search flow

```bash
sandra-chat start --user test-agent

sandra-chat send "What jobs are available?"
# verify reply mentions jobs or asks for location

sandra-chat send "Show me software engineer roles in Zurich"
# verify reply contains relevant job listings

sandra-chat history --json
# inspect the full conversation

sandra-chat stop
```

## Example: Check session before starting

```bash
sandra-chat status --json
# if status shows a running session, skip start

sandra-chat send "Are there any remote positions?"
sandra-chat stop
```

## Example: Multi-turn conversation test

```bash
sandra-chat start
sandra-chat send "Hi, my name is Alice"
sandra-chat send "What is your name?"
sandra-chat send "Can you help me find a job in Berlin?"
sandra-chat send "I prefer backend roles"
sandra-chat history --json
# verify context is maintained across turns
sandra-chat stop
```

## Specific tasks

- **Testing bot flows end-to-end** — [references/testing-flows.md](references/testing-flows.md)
