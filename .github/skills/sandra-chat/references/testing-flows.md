# Testing Bot Flows End-to-End

This file describes how to test the running Maya bot during development.
It does **not** define Maya runtime skills.
If you want to verify Maya's self-improvement behavior, do that through chat by exercising
the runtime capability tools (`capability_propose`, `capability_list`, `capability_execute`).

Use `sandra-chat start` / `sandra-chat send` / `sandra-chat stop` to exercise complete
conversation paths against the real Maya codebase.

## Structuring a test run

1. **Start the session** — call `sandra-chat start` once.
2. **Send messages** — call `sandra-chat send` for each turn. Each call blocks until the
   bot replies (or times out).
3. **Assert** — inspect stdout for expected keywords, tone, or structure.
4. **Inspect history** — call `sandra-chat history --json` to review the full transcript.
5. **Stop** — always call `sandra-chat stop` to avoid orphaned processes.

## Checking bot replies

`sandra-chat send` returns the bot's reply as a plain string. You can:

- Check for keywords: does the reply mention "Zurich", "remote", a job title?
- Check for absence: make sure the bot doesn't hallucinate roles that don't exist.
- Check tone: is the reply helpful, concise, not repetitive?

## Handling timeouts

If the bot is slow (for example a real Copilot SDK turn through a remote model provider), increase `timeout_seconds`:

```bash
sandra-chat send "Summarise all jobs" --timeout 60
```

Default is 30 seconds — sufficient for local smoke tests and most short turns.

## Testing multi-turn context

Maya maintains conversation context across turns. Verify this by:

1. Sending user details in turn 1: `"I am looking for senior roles in Zurich"`
2. Asking a follow-up in turn 2: `"What did I just tell you?"`
3. Confirming the reply references the earlier context.

## Resetting between scenarios

To run a second independent scenario, call `sandra-chat stop` then `sandra-chat start`
again. Each `start` creates a fresh session with an empty conversation history.

## Interpreting `sandra-chat history --json`

Returns a JSON array:

```json
[
  { "role": "user",      "text": "Hello",         "at": "2026-03-24T21:00:00Z" },
  { "role": "assistant", "text": "Hi! How can...", "at": "2026-03-24T21:00:01Z" }
]
```

Use `role` to separate user messages from bot replies. Use `at` to measure
response latency.
