# Contributing

Thanks for helping with this sample. It sends the same three tasks to GPT-6.2 Astra and GPT-6.2 Sol and prints tokens, latency and estimated cost side by side. Keep changes focused on making that comparison easy to read, run and trust.

## Getting started

1. Fork the repository and clone your fork.
2. Install the .NET 10 SDK.
3. Create a branch for your change:

   ```bash
   git switch -c your-change-name
   ```

4. Restore and build from the project folder:

   ```bash
   dotnet restore
   dotnet build --no-restore
   ```

Building needs package access but no Azure credentials. A live run needs a Microsoft Foundry resource with a GPT-6.2 Astra deployment and a GPT-6.2 Sol deployment, and it costs money. Set it up as described in [README.md](README.md).

## Making changes

- Follow the existing C# style: top-level statements, nullable reference types, and records for small data types.
- Keep it to one `Program.cs`. The point of the sample is that you can read it top to bottom.
- Both models must get the same prompts, the same tools and the same reasoning effort. If a change treats the two models differently, say why in the pull request, because it changes what the comparison means.
- Prices live in the `Price` records in `Program.cs`. If you update them, cite the pricing page you used and the date, and note whether they are Global Standard, Data Zone or provisioned, short or long context.
- Keep the tools as simulations that return hard-coded data. Don't add calls to real systems.
- Update the README when you change setup, configuration, dependencies or behaviour.
- Avoid unrelated refactoring and dependency updates in the same pull request.
- Keep endpoints, keys, tenant IDs, deployment names you don't want public, and real customer or vendor data out of commits, logs, issues and screenshots. Use synthetic data in examples.

## Validation

Before opening a pull request, run:

```bash
dotnet restore
dotnet build --no-restore
git diff --check
```

There is no automated test project, because the program's output comes from live model calls. For changes to the tasks, the tools or the cost calculation, do a live run if you have access to deployments, and check that:

- both models answer every task, and the tool-using task calls both tools;
- the table at the end lists six rows, and the `cached`, `output` and `reason` columns look plausible;
- the estimated cost matches a hand calculation from the token counts and the `Price` records for at least one row.

Model output varies between runs, so report what you saw instead of expecting exact text. If you can't do a live run, say so in the pull request and list the checks you did complete.

## Pull requests

Open a pull request against the default branch. Include:

- The problem and the resulting behaviour.
- Any setup or compatibility changes reviewers need to know about.
- How you validated the change, and what you could not check.

## Reporting problems

Use GitHub Issues for bugs and ideas. For a suspected security problem, use the repository's private vulnerability reporting instead of a public issue. Don't paste keys, tokens or endpoints you want to keep private.
