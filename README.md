# GPT-6.2 Astra vs Sol for C# Developers

> A runnable .NET 10 console app that sends the same three tasks to **GPT-6.2 Astra** and **GPT-6.2 Sol** in **Microsoft Foundry** through `Microsoft.Extensions.AI`, then prints a side-by-side table of tokens, latency and estimated cost.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)

- ⭐ If this sample helps you, star the repo so other .NET developers can find it.
- 🔁 Related: [GPT-6.1 Sol C# Demo](https://github.com/taswar/Gpt-6.1-Sol-CSharp-Demo) and [GPT-6 Astra C# Demo](https://github.com/taswar/GptSixAstraDemo)
- For my free ebook on prompt engineering, see [Prompt Engineering for .NET Developers](https://github.com/taswar/PromptEngineeringForDotNetDevelopers)

It's one `Program.cs` with no wrapper library. Both models get the same prompts, the same tools and the same reasoning effort (`Medium`), so the differences you see come from the models and not from the settings.

## What's inside

| # | Task | What it shows |
|---|------|---------------|
| 1 | **Bug investigation** | A checkout API returns intermittent HTTP 500s. The model calls two read-only tools (`GetRecentErrorLogs`, `GetRecentCommits`) before it names a root cause, a fix and a confidence level. |
| 2 | **PR triage** | Reviews a CI failure using only the supplied evidence, separates observed facts from hypotheses and suggests one reproducible next test. It never approves or merges. |
| 3 | **Vendor review** | Reviews SLA and incident notes, quotes the source label for every finding, and tells a *missing* fact apart from a *negative* one. |

Each task runs against both deployments, so one run makes at least six model requests. When all of them finish, the app prints a comparison table like this one (the numbers below are placeholders, not real results):

```
task               model    input  cached  output  reason   secs    est. $
bug-investigation  Astra      ...     ...     ...     ...    ...       ...
bug-investigation  Sol        ...     ...     ...     ...    ...       ...
```

The estimated cost is calculated from the `Price` records in `Program.cs` (USD per 1M tokens for input, cached input and output). Those prices are hard-coded, so check the current Foundry pricing page and update them before you rely on the numbers.

## Prerequisites

- **.NET 10 SDK**
- An Azure subscription with a **Microsoft Foundry** resource and two deployments: one for **GPT-6.2 Astra** and one for **GPT-6.2 Sol**
- The **Azure CLI**, signed in with `az login`, and the **Cognitive Services OpenAI User** role on the resource. Authentication goes through Entra ID (`DefaultAzureCredential`) by default.
- Or an API key for the resource, if you'd rather skip Entra ID for a first try (see step 1).

## Run it

**1. Clone the repo and store your settings** (user secrets are kept outside the repo):

```bash
git clone https://github.com/taswar/GPT-6-2-Astra-vs-Sol-CSharp-Sample.git
cd GPT-6-2-Astra-vs-Sol-CSharp-Sample
dotnet user-secrets set "AZURE_AI_ENDPOINT" "https://<your-resource>.cognitiveservices.azure.com/"
dotnet user-secrets set "ASTRA_DEPLOYMENT" "gpt-6.2-astra"
dotnet user-secrets set "SOL_DEPLOYMENT" "gpt-6.2-sol"
```

Use the resource's base endpoint. The app appends `openai/v1/` itself. The deployment names default to `gpt-6.2-astra` and `gpt-6.2-sol`, so you only need those two lines if yours are named differently.

To use an API key instead of Entra ID, also run `dotnet user-secrets set "AZURE_OPENAI_API_KEY" "<your-api-key>"` and skip `az login`. The app sends the key in the `api-key` header.

**2. Sign in and run:**

```bash
az login
dotnet run
```

## Configuration

| Setting | Required | Default | Purpose |
|---------|----------|---------|---------|
| `AZURE_AI_ENDPOINT` | Yes | none | Base endpoint of your Foundry resource |
| `ASTRA_DEPLOYMENT` | No | `gpt-6.2-astra` | Deployment name for GPT-6.2 Astra |
| `SOL_DEPLOYMENT` | No | `gpt-6.2-sol` | Deployment name for GPT-6.2 Sol |
| `AZURE_OPENAI_API_KEY` | No | none | Uses an `api-key` header instead of Entra ID |

## How the client is built

The app uses the Responses API (`/openai/v1/responses`) through `ResponsesClient`, wrapped as an `IChatClient` so function invocation works the same way for both models:

```csharp
IChatClient CreateClient(string deployment) =>
    new ResponsesClient(authPolicy, clientOptions)
        .AsIChatClient(deployment)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();
```

Reasoning effort is set once and shared by every call:

```csharp
Reasoning = new() { Effort = ReasoningEffort.Medium }
```

Keep that fixed while you compare models. If you change it for one model only, you're comparing settings, not models.

The OpenAI SDK marks the Responses types as experimental, which is why `Program.cs` starts with `#pragma warning disable OPENAI001`.

## Good to know

- **Live calls cost money.** One run makes at least six model requests, and the bug-investigation task makes extra calls while the models use the tools.
- **The tools are simulations.** The error logs, commit history, CI result and vendor documents are all hard-coded in `Program.cs`. Nothing touches a real system.
- **Output varies.** Responses are model-generated and differ between runs. Compare token counts and latency across several runs, not just one.
- **Output tokens already include reasoning tokens.** The `reason` column shows how many of the `output` tokens were spent on reasoning.
- **Timeouts.** Each HTTP request has a 3-minute network timeout, and the whole run shares one 10-minute timeout.

## Troubleshooting

| Error | Likely cause | Fix |
|-------|--------------|-----|
| `404 DeploymentNotFound` | `ASTRA_DEPLOYMENT` or `SOL_DEPLOYMENT` doesn't match a deployment on the resource | Set each one to the exact deployment name. A brand-new deployment can take about 5 minutes to become available. |
| `401` / `403` | Missing role, `az login` signed in to the wrong tenant, or a bad API key | Assign **Cognitive Services OpenAI User**, then run `az login --tenant <id>`; or check the key |
| `InvalidOperationException: AZURE_AI_ENDPOINT is not set` | User secrets are missing | Run the `dotnet user-secrets set` commands from inside the project folder |

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.AI` | 10.10.0 | `IChatClient` and function invocation |
| `Microsoft.Extensions.AI.OpenAI` | 10.10.1 | `IChatClient` adapter for the OpenAI `ResponsesClient` |
| `Azure.Identity` | 1.21.0 | `DefaultAzureCredential` for Entra ID sign-in |
| `Microsoft.Extensions.Configuration.UserSecrets` | 10.0.12 | Local development secrets |
| `Azure.AI.OpenAI` | 2.1.0 | No longer used by `Program.cs`. Safe to remove. |

## Project layout

```
GPT-6-2-Astra-vs-Sol-CSharp-Sample.csproj   Target framework, NuGet references, UserSecretsId
Program.cs                                  Client setup, the three tasks, the comparison table
```

## Learn more

- [Microsoft.Extensions.AI documentation](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
- [.NET Secret Manager](https://learn.microsoft.com/aspnet/core/security/app-secrets)

If this saved you time, a ⭐ helps other .NET developers find it.
