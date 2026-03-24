# Running in Placeholder Mode (no Azure OpenAI credentials)

Maya gracefully degrades when Azure OpenAI credentials are absent.
The placeholder assistant handles messages with a canned response.

## What placeholder mode means

- The bot starts and responds normally — no errors.
- Replies are deterministic and non-AI, with text such as `"Sandra Maya orchestrator foundation is online."`
  and `"Azure OpenAI settings are not fully configured yet, so the placeholder orchestrator handled this message locally."`.
- All Telegram mock plumbing (send/receive, session state, history) works exactly the same.
- Use this mode to test **conversation flow**, **routing**, and **session lifecycle**
  without needing real credentials or incurring API cost.

## When to use placeholder mode

- CI pipelines where credentials are not available
- Quick smoke tests to verify the bot starts and responds
- Testing the `sandra-chat` tooling itself

## Enabling real AI responses

Set these environment variables **before** calling `sandra-chat start`:

```powershell
$env:AzureOpenAi__BaseUrl        = "https://your-resource.openai.azure.com/"
$env:AzureOpenAi__ApiKey         = "your-api-key"
$env:AzureOpenAi__DeploymentName = "gpt-4o"
```

The bot subprocess inherits the parent environment, so variables set in the Copilot
session are automatically passed through.

## Detecting which mode is active

Call `sandra-chat send "ping"` immediately after start. A placeholder reply will
mention `"placeholder orchestrator"` or `"Azure OpenAI settings are not fully configured yet"`.
A real AI reply will be conversational and will not include those setup messages.
