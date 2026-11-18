#pragma warning disable OPENAI001 // Responses API types are marked experimental in the OpenAI SDK

using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Diagnostics;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = config["AZURE_AI_ENDPOINT"]
    ?? throw new InvalidOperationException(
        "AZURE_AI_ENDPOINT is not set. Run: dotnet user-secrets set \"AZURE_AI_ENDPOINT\" \"<your-endpoint>\"");

// Two deployments of the same resource. The names are whatever you called them.
var contenders = new[]
{
    new Contender("Astra", config["ASTRA_DEPLOYMENT"] ?? "gpt-6.2-astra", new Price(8.00m, 0.40m, 40.00m)),
    new Contender("Sol",   config["SOL_DEPLOYMENT"]   ?? "gpt-6.2-sol",   new Price(2.00m, 0.10m, 10.00m))
};

var clientOptions = new ResponsesClientOptions
{
    Endpoint = new Uri(new Uri(endpoint), "openai/v1/"),
    NetworkTimeout = TimeSpan.FromMinutes(3)
};

// Entra ID by default. Set AZURE_OPENAI_API_KEY to send an api-key header instead.
var apiKey = config["AZURE_OPENAI_API_KEY"];
AuthenticationPolicy authPolicy = string.IsNullOrWhiteSpace(apiKey)
    ? new BearerTokenPolicy(new DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default")
    : ApiKeyAuthenticationPolicy.CreateHeaderApiKeyPolicy(new ApiKeyCredential(apiKey), "api-key");

IChatClient CreateClient(string deployment) =>
    new ResponsesClient(authPolicy, clientOptions)
        .AsIChatClient(deployment)
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();

// The same three tasks go to both models, with the same reasoning effort.
var tasks = new WorkTask[]
{
    new("bug-investigation",
        "You are a senior engineer investigating a production bug. Pull both recent error logs " +
        "and recent commit history before concluding a root cause. State your recommended fix, " +
        "your confidence level, and the next action a human reviewer should take.",
        "Users report 'checkout-api' intermittently returns HTTP 500 on order submission since this morning. " +
        "What's going on and what should we do?",
        [AIFunctionFactory.Create(GetRecentErrorLogs), AIFunctionFactory.Create(GetRecentCommits)]),

    new("pr-triage",
        "You are a C# PR reviewer. Use only the supplied CI evidence. Separate observed facts from " +
        "hypotheses. Suggest one reproducible next test. Do not approve or merge the PR.",
        "PR #418: change to PromoCodeResolver; CI failed: DiscountTests.SinglePromo_WhenExpired " +
        "expected rejection, got success. Other 203 tests passed.",
        []),

    new("vendor-review",
        "Review only the supplied sources. For each finding, quote its source label. Distinguish a " +
        "missing fact from a negative fact. Suggest a human follow-up; do not make a legal determination.",
        """
        [SLA §4] Credits require a claim within 30 days of the incident.
        [Incident INC-204] Availability dropped below the SLA target on 2026-09-16.
        [Incident INC-204] The incident note does not record whether a claim was filed.

        What should the weekly vendor review flag?
        """,
        [])
};

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
var rows = new List<Row>();

foreach (var task in tasks)
{
    foreach (var contender in contenders)
    {
        var chat = CreateClient(contender.Deployment);
        var options = new ChatOptions
        {
            Tools = [.. task.Tools],
            // Keep this fixed while comparing models, or you are comparing settings, not models.
            Reasoning = new() { Effort = ReasoningEffort.Medium }
        };

        var timer = Stopwatch.StartNew();
        var response = await chat.GetResponseAsync(
            [new(ChatRole.System, task.System), new(ChatRole.User, task.User)],
            options, cts.Token);
        timer.Stop();

        var usage = response.Usage;
        var input = usage?.InputTokenCount ?? 0;
        var cached = usage?.CachedInputTokenCount ?? 0;
        var output = usage?.OutputTokenCount ?? 0;      // output already includes reasoning tokens
        var reasoning = usage?.ReasoningTokenCount ?? 0;
        var cost = contender.Price.Estimate(input, cached, output);

        Console.WriteLine($"###################### {task.Name} / {contender.Name} ######################");
        Console.WriteLine(response.Text);
        rows.Add(new Row(task.Name, contender.Name, input, cached, output, reasoning, timer.Elapsed.TotalSeconds, cost));
    }
}

Console.WriteLine();
Console.WriteLine($"{"task",-18} {"model",-6} {"input",7} {"cached",7} {"output",7} {"reason",7} {"secs",6} {"est. $",9}");
foreach (var r in rows)
{
    Console.WriteLine($"{r.Task,-18} {r.Model,-6} {r.Input,7} {r.Cached,7} {r.Output,7} {r.Reasoning,7} {r.Seconds,6:F1} {r.Cost,9:F5}");
}

// --- Tool stand-ins for real observability and source-control APIs ---

[Description("Gets recent error log entries for a named service.")]
static string GetRecentErrorLogs([Description("The service name, e.g. checkout-api")] string serviceName) =>
    serviceName == "checkout-api"
        ? "09:14 NullReferenceException at OrderTotalCalculator.Apply(discount). Occurs on ~8% of requests, only when a promo code is present."
        : "No recent errors found.";

[Description("Gets a summary of recent commits merged to a named service's main branch.")]
static string GetRecentCommits([Description("The service name, e.g. checkout-api")] string serviceName) =>
    serviceName == "checkout-api"
        ? "06:40 - 'Refactor discount pipeline to support stacked promo codes' (touches OrderTotalCalculator.cs, PromoCodeResolver.cs)."
        : "No recent commits found.";

// Types go after the top-level statements.
record Contender(string Name, string Deployment, Price Price);
record WorkTask(string Name, string System, string User, IList<AITool> Tools);
record Row(string Task, string Model, long Input, long Cached, long Output, long Reasoning, double Seconds, decimal Cost);

// USD per 1M tokens, Global Standard, short context. Check the current pricing page.
record Price(decimal Input, decimal CachedInput, decimal Output)
{
    public decimal Estimate(long input, long cached, long output) =>
        ((input - cached) * Input + cached * CachedInput + output * Output) / 1_000_000m;
}
