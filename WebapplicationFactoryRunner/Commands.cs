using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace WebapplicationFactoryRunner;

public class GlobalSettings : CommandSettings
{
    [CommandOption("--plain")]
    [Description("Plain Console.WriteLine output instead of Spectre rendering.")]
    public bool Plain { get; init; }

    [CommandOption("--hotpink")]
    [Description("Render ALL output exclusively in hot pink.")]
    public bool HotPink { get; init; }

    public override ValidationResult Validate() =>
        Plain && HotPink
            ? ValidationResult.Error("--plain and --hotpink are mutually exclusive.")
            : ValidationResult.Success();

    public Output CreateOutput() =>
        new(Plain ? OutputStyle.Plain : HotPink ? OutputStyle.HotPink : OutputStyle.Fancy);
}

public class RunSettings : GlobalSettings
{
    [CommandOption("-f|--filter <EXPRESSION>")]
    [Description("Passed through to 'dotnet test --filter'.")]
    public string? Filter { get; init; }
}

public class RunCommand : Command<RunSettings>
{
    protected override int Execute(CommandContext context, RunSettings settings, CancellationToken cancellationToken)
    {
        var output = settings.CreateOutput();
        output.Line(settings.Filter is null
            ? "Running full test suite..."
            : $"Running tests matching: {settings.Filter}");
        output.Line("To run this yourself without the runner:");
        output.Line(settings.Filter is null
            ? "  dotnet test WebapplicationFactoryTests"
            : $"  dotnet test WebapplicationFactoryTests --filter \"{settings.Filter}\"");

        var results = DotnetTestRunner.Run(settings.Filter);
        output.Results(results);
        return results.Any(r => r.Outcome == "Failed") ? 1 : 0;
    }
}

public class PesterSettings : GlobalSettings
{
    [CommandOption("-u|--url <BASEURL>")]
    [Description("Base URL of the running API. Default: https://localhost:7016")]
    public string Url { get; init; } = "https://localhost:7016";
}

public class PesterCommand : Command<PesterSettings>
{
    protected override int Execute(CommandContext context, PesterSettings settings, CancellationToken cancellationToken)
    {
        var output = settings.CreateOutput();

        if (!PesterRunner.IsApiReachable(settings.Url))
        {
            output.Line($"API is not reachable at {settings.Url}.");
            output.Line("Start it first (cd API; dotnet run) or pass --url <baseurl>.");
            return 1;
        }

        output.Line($"Running Pester tests against {settings.Url} ...");
        output.Line("To run this yourself without the runner (PowerShell, API must be running):");
        output.Line("  Invoke-Pester -Path ./PesterTests");

        var results = PesterRunner.Run(settings.Url);
        output.Results(results);
        return results.Any(r => r.Outcome == "Failed") ? 1 : 0;
    }
}

public class InteractiveCommand : Command<PesterSettings>
{
    private const string RunAll = "Run ALL tests";

    protected override int Execute(CommandContext context, PesterSettings settings, CancellationToken cancellationToken)
    {
        var output = settings.CreateOutput();

        var framework = Select(settings, "Which test suite?",
            ["NUnit (in-process WebApplicationFactory)", "Pester (black-box HTTP against a running API)"]);
        if (framework is null)
        {
            return 1;
        }

        return framework.StartsWith("NUnit", StringComparison.Ordinal)
            ? RunNUnit(settings, output)
            : RunPester(settings, output);
    }

    private static int RunNUnit(PesterSettings settings, Output output)
    {
        output.Line("Discovering NUnit tests...");
        var tests = DotnetTestRunner.ListTests();
        if (tests.Count == 0)
        {
            output.Line("No tests found.");
            return 1;
        }

        var selected = Select(settings, "Pick a test (or run everything):", [RunAll, .. tests]);
        if (selected is null)
        {
            return 1;
        }

        string? filter = null;
        if (selected != RunAll)
        {
            // Strip any parameter list so parameterized test cases filter on the method name.
            filter = $"FullyQualifiedName~{selected.Split('(')[0]}";
        }

        output.Line(string.Empty);
        output.Line("To run this yourself without the runner:");
        output.Line(filter is null
            ? "  dotnet test WebapplicationFactoryTests"
            : $"  dotnet test WebapplicationFactoryTests --filter \"{filter}\"");
        output.Line(string.Empty);

        var results = DotnetTestRunner.Run(filter);
        output.Results(results);
        return results.Any(r => r.Outcome == "Failed") ? 1 : 0;
    }

    private static int RunPester(PesterSettings settings, Output output)
    {
        if (!PesterRunner.IsApiReachable(settings.Url))
        {
            output.Line($"API is not reachable at {settings.Url}.");
            output.Line("Start it first (cd API; dotnet run) or pass --url <baseurl>.");
            return 1;
        }

        output.Line("Discovering Pester tests...");
        var tests = PesterRunner.ListTests();
        if (tests.Count == 0)
        {
            output.Line("No tests found. Is Pester 5 installed?");
            return 1;
        }

        var selected = Select(settings, "Pick a test (or run everything):", [RunAll, .. tests]);
        if (selected is null)
        {
            return 1;
        }

        var filter = selected == RunAll ? null : $"*{selected}*";

        output.Line(string.Empty);
        output.Line("To run this yourself without the runner (PowerShell, API must be running):");
        output.Line(filter is null
            ? "  Invoke-Pester -Path ./PesterTests"
            : $"  Invoke-Pester -Path ./PesterTests -FullNameFilter '{filter}'");
        output.Line(string.Empty);

        var results = PesterRunner.Run(settings.Url, filter);
        output.Results(results);
        return results.Any(r => r.Outcome == "Failed") ? 1 : 0;
    }

    /// <summary>Menu that honors --plain (numbered list + stdin) and --hotpink.</summary>
    private static string? Select(GlobalSettings settings, string title, IReadOnlyList<string> choices)
    {
        if (settings.Plain)
        {
            Console.WriteLine(title);
            for (var i = 0; i < choices.Count; i++)
            {
                Console.WriteLine($"{i + 1,3}. {choices[i]}");
            }
            Console.Write("Selection: ");
            if (!int.TryParse(Console.ReadLine(), out var choice) || choice < 1 || choice > choices.Count)
            {
                Console.WriteLine("Invalid selection.");
                return null;
            }
            return choices[choice - 1];
        }

        var prompt = new SelectionPrompt<string>()
            .Title(settings.HotPink ? $"[hotpink]{title}[/]" : title)
            .PageSize(15)
            .AddChoices(choices);
        if (settings.HotPink)
        {
            prompt.HighlightStyle(new Style(Color.HotPink));
        }
        return AnsiConsole.Prompt(prompt);
    }
}
