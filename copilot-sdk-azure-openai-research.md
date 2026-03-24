# Using Azure OpenAI / Azure AI Foundry as the backend for the GitHub Copilot SDK

## Executive Summary

Yes — the GitHub Copilot SDK can use Azure as its backend through the SDK's BYOK (Bring Your Own Key) provider configuration.[^1][^2] The supported setup is not “Copilot authenticated against Azure”; instead, you configure a session-level `provider` so the Copilot CLI runtime sends model traffic to your Azure endpoint with your credentials.[^1][^3] The most important implementation choice is matching the Azure endpoint shape to the right provider type: native Azure endpoints use `type: "azure"` with a host-only `baseUrl`, while Azure OpenAI-compatible `/openai/v1/` endpoints use `type: "openai"` with the full path in `baseUrl`.[^3] For GPT-5-style deployments, the docs recommend `wireApi: "responses"`; for older/default behavior, the SDK uses `"completions"`.[^4]

## What to do

### 1. Confirm you are using BYOK

The SDK documentation explicitly lists BYOK as the path for “using your own API keys,” and marks it as the only authentication mode that does **not** require a GitHub Copilot subscription.[^2] The same docs state that BYOK supports Azure AI Foundry, OpenAI, Anthropic, and other OpenAI-compatible endpoints.[^2]

### 2. Choose the right Azure endpoint shape

The SDK docs describe **two** Azure patterns, and they are easy to confuse.[^3]

| Azure shape | Example endpoint | `provider.type` | `baseUrl` rule | Extra config |
|---|---|---|---|---|
| Native Azure OpenAI endpoint | `https://my-resource.openai.azure.com` | `"azure"` | Host only — **do not** append `/openai/v1` | `azure.apiVersion` if you want to override the default `2024-10-21` |
| OpenAI-compatible Azure endpoint | `https://my-resource.openai.azure.com/openai/v1/` | `"openai"` | Include the full `/openai/v1/` path | For GPT-5 series, set `wireApi: "responses"` |

This distinction lines up with Azure's own docs: Azure still documents native deployment routes that use `/openai/deployments/{deployment-id}/...?...api-version=...`, while the newer Responses examples use the OpenAI-compatible `/openai/v1/` base URL.[^5][^6]

### 3. Always set `model`, and treat it as the Azure deployment name

When you use BYOK, the SDK requires a `model` value.[^7] In the Azure examples in the BYOK docs, the example comments label the `model` field as “Your deployment name,” which matches Azure's own Responses examples that say to replace the `model` value with your model deployment name.[^8][^6]

### 4. Use API-key auth for the supported path

Azure OpenAI itself supports both API keys and Microsoft Entra ID.[^5] However, the Copilot SDK BYOK docs explicitly say that BYOK only supports **static credentials**, does **not** support Microsoft Entra ID, does **not** support managed identities, and only accepts a static `bearerToken` string with no automatic refresh.[^9][^10] In other words: if you want a supported, low-friction setup today, use an Azure API key.[^9]

### 5. Pick the wire API deliberately

The BYOK docs define two wire formats: `"completions"` (default) and `"responses"`.[^4] The Azure AI Foundry quick-start examples use `wireApi: "responses"` for GPT-5.2 Codex deployments, and the docs say to use `"responses"` for GPT-5 series models that support the newer Responses API.[^8][^4]

### 6. If you rely on model discovery, provide your own model list

The BYOK docs note that the CLI server might not know what models your provider supports, and recommend supplying a custom `onListModels` handler if you need accurate `listModels()` behavior from the SDK.[^11]

## Recommended configurations

### Option A — Native Azure OpenAI endpoint

Use this when your endpoint is just the Azure host, for example `https://my-resource.openai.azure.com`.[^3]

```typescript
import { CopilotClient, approveAll } from "@github/copilot-sdk";

const client = new CopilotClient();
await client.start();

const session = await client.createSession({
  model: "my-gpt-4.1-deployment",
  onPermissionRequest: approveAll,
  provider: {
    type: "azure",
    baseUrl: "https://my-resource.openai.azure.com",
    apiKey: process.env.AZURE_OPENAI_KEY,
    azure: {
      apiVersion: "2024-10-21",
    },
  },
});
```

### Option B — OpenAI-compatible Azure AI Foundry endpoint

Use this when Azure gives you an OpenAI-compatible base URL ending in `/openai/v1/`.[^3][^6]

```typescript
import { CopilotClient, approveAll } from "@github/copilot-sdk";

const client = new CopilotClient();
await client.start();

const session = await client.createSession({
  model: "my-gpt-5-deployment",
  onPermissionRequest: approveAll,
  provider: {
    type: "openai",
    baseUrl: "https://my-resource.openai.azure.com/openai/v1/",
    apiKey: process.env.FOUNDRY_API_KEY,
    wireApi: "responses",
  },
});
```

## Common failure modes

### Wrong `type` for the endpoint

If you point `type: "openai"` at a native Azure host like `https://my-resource.openai.azure.com`, the docs explicitly say that is the wrong pairing; the correct choice is `type: "azure"`.[^12] Conversely, if Azure gives you an OpenAI-compatible `/openai/v1/` endpoint, the docs say to use `type: "openai"`.[^12]

### Missing `model`

The BYOK troubleshooting section is explicit: `model` is required when you use a custom provider.[^7]

### Assuming Entra ID is supported because Azure supports it

This is the biggest trap. Azure the service supports Entra ID, but the Copilot SDK's BYOK layer does not support Entra ID or managed identity for long-running authenticated sessions because it only accepts static credentials and does not refresh bearer tokens.[^5][^9][^10]

### Assuming the SDK will auto-discover Azure deployments

The docs say the CLI server may not know your provider's available models. If you need discovery, you should implement `onListModels` yourself.[^11]

## Practical recommendation

If your Azure tenant exposes an OpenAI-compatible `/openai/v1/` endpoint and you are targeting GPT-5-class models, use `provider.type = "openai"`, the full `/openai/v1/` base URL, an API key, and `wireApi = "responses"`.[^8][^4][^6] If you are using the classic native Azure endpoint shape, use `provider.type = "azure"`, pass only the host as `baseUrl`, and set `azure.apiVersion` if you need to pin or override the default version.[^3] If your organization requires Microsoft Entra ID or managed identity rather than API keys, the current documented BYOK path is a poor fit; the SDK docs treat that as unsupported, even though Azure itself supports those auth modes.[^5][^9]

## Confidence Assessment

### Certain

The SDK **does** support Azure-backed BYOK, with documented examples for Python, Node.js, Go, and .NET.[^8] The endpoint/type pairing rules are explicit in the BYOK docs, including the warning not to use `type: "openai"` against a native Azure host.[^3][^12] The auth limitation is also explicit: BYOK is documented around static API keys or static bearer tokens, and the docs directly call out the lack of Entra ID / managed identity support.[^9][^10]

### Inferred but highly likely

The docs use both “Azure OpenAI” and “Azure AI Foundry” terminology. The implementation detail that matters is the endpoint shape, not the marketing label: host-only native endpoints map to `type: "azure"`, while `/openai/v1/` endpoints map to `type: "openai"`.[^3][^5][^6] I also infer that “model” should always be treated as the Azure **deployment name** in practice, because both the SDK examples and Azure docs frame it that way.[^8][^6]

## Footnotes

[^1]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:3-14` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^2]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/index.md:7-28` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^3]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:172-177,192-195,213-239` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^4]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:179-184` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^5]: [Azure OpenAI inference reference](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/reference), captured in local research snapshot `azure-reference.md:5-25`.
[^6]: [Azure OpenAI Responses API docs](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/responses), captured in local research snapshot `azure-responses.md:5-24`.
[^7]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:329-343` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^8]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:16-39,68-78,112-118,147-153` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^9]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:297-307,320-323` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^10]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:275-287` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^11]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:289-291` (commit `40887393a9e687dacc141a645799441b0313ff15`).
[^12]: [github/copilot-sdk](https://github.com/github/copilot-sdk) `docs/auth/byok.md:346-371` (commit `40887393a9e687dacc141a645799441b0313ff15`).
