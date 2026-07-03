using System.ComponentModel;
using System.Diagnostics;
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

        var api = PesterRunner.EnsureApiRunning(settings.Url, output.Line);
        try
        {
            output.Line($"Running Pester tests against {settings.Url} ...");
            output.Line("To run this yourself without the runner (with the API running):");
            output.Line("  Invoke-Pester -Path ./PesterTests");

            var results = PesterRunner.Run(settings.Url);
            output.Results(results);
            return results.Any(r => r.Outcome == "Failed") ? 1 : 0;
        }
        finally
        {
            PesterRunner.StopApi(api);
        }
    }
}

public class InteractiveCommand : Command<PesterSettings>
{
    private const string RunAllNUnit = "Run ALL NUnit tests";
    private const string RunAllPester = "Run ALL Pester tests";
    private const string PickNUnit = "Pick an individual NUnit test";
    private const string PickPester = "Pick an individual Pester test";
    private const string Back = "Back";
    private const string Exit = "Exit";

    // The API instance this session started (if any) — kept alive between menu
    // actions so consecutive Pester runs don't restart it, killed on Exit.
    private Process? _api;

    private (string Action, IReadOnlyList<TestResult> Results)? _lastRun;

    protected override int Execute(CommandContext context, PesterSettings settings, CancellationToken cancellationToken)
    {
        var output = settings.CreateOutput();
        try
        {
            while (true)
            {
                output.Line(string.Empty);
                if (_lastRun is { } last)
                {
                    output.StatusTable(last.Action, last.Results);
                }
                var choice = Select(settings, "Taco Bell API test runner",
                    [RunAllNUnit, RunAllPester, PickNUnit, PickPester, Exit]);

                switch (choice)
                {
                    case RunAllNUnit:
                        RunNUnit(output, filter: null);
                        break;
                    case RunAllPester:
                        RunPester(settings, output, filter: null);
                        break;
                    case PickNUnit:
                        PickAndRunNUnit(settings, output);
                        break;
                    case PickPester:
                        PickAndRunPester(settings, output);
                        break;
                    case Exit or null:
                        return 0;
                }
            }
        }
        finally
        {
            PesterRunner.StopApi(_api);
        }
    }

    private void PickAndRunNUnit(PesterSettings settings, Output output)
    {
        output.Line("Discovering NUnit tests...");
        var tests = DotnetTestRunner.ListTests();
        if (tests.Count == 0)
        {
            output.Line("No tests found.");
            return;
        }

        var selected = Select(settings, "Pick a test:", [Back, .. tests]);
        if (selected is null or Back)
        {
            return;
        }

        // Strip any parameter list so parameterized test cases filter on the method name.
        RunNUnit(output, $"FullyQualifiedName~{selected.Split('(')[0]}");
    }

    private void PickAndRunPester(PesterSettings settings, Output output)
    {
        output.Line("Discovering Pester tests...");
        var tests = PesterRunner.ListTests();
        if (tests.Count == 0)
        {
            output.Line("No tests found. Is Pester 5 installed?");
            return;
        }

        var selected = Select(settings, "Pick a test:", [Back, .. tests]);
        if (selected is null or Back)
        {
            return;
        }

        RunPester(settings, output, $"*{selected}*");
    }

    private void RunNUnit(Output output, string? filter)
    {
        output.Line("To run this yourself without the runner:");
        output.Line(filter is null
            ? "  dotnet test WebapplicationFactoryTests"
            : $"  dotnet test WebapplicationFactoryTests --filter \"{filter}\"");
        output.Line(string.Empty);

        var results = DotnetTestRunner.Run(filter);
        output.Results(results);
        _lastRun = (filter is null ? "NUnit: all tests" : $"NUnit: {filter}", results);
    }

    private void RunPester(PesterSettings settings, Output output, string? filter)
    {
        try
        {
            _api ??= PesterRunner.EnsureApiRunning(settings.Url, output.Line);
        }
        catch (InvalidOperationException ex)
        {
            output.Line(ex.Message);
            return;
        }

        output.Line("To run this yourself without the runner (with the API running):");
        output.Line(filter is null
            ? "  Invoke-Pester -Path ./PesterTests"
            : $"  Invoke-Pester -Path ./PesterTests -FullNameFilter '{filter}'");
        output.Line(string.Empty);

        var results = PesterRunner.Run(settings.Url, filter);
        output.Results(results);
        _lastRun = (filter is null ? "Pester: all tests" : $"Pester: {filter}", results);
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
            .Title(settings.HotPink ? $"[hotpink]{Markup.Escape(title)}[/]" : $"[bold blue]{Markup.Escape(title)}[/]")
            .PageSize(15)
            .AddChoices(choices);
        if (settings.HotPink)
        {
            prompt.HighlightStyle(new Style(Color.HotPink));
        }
        return AnsiConsole.Prompt(prompt);
    }
}
