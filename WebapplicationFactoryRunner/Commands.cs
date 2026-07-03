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

        var results = DotnetTestRunner.Run(settings.Filter);
        output.Results(results);
        return results.Any(r => r.Outcome == "Failed") ? 1 : 0;
    }
}

public class InteractiveCommand : Command<GlobalSettings>
{
    protected override int Execute(CommandContext context, GlobalSettings settings, CancellationToken cancellationToken)
    {
        var output = settings.CreateOutput();
        output.Line("Discovering tests...");
        var tests = DotnetTestRunner.ListTests();
        if (tests.Count == 0)
        {
            output.Line("No tests found.");
            return 1;
        }

        string selected;
        if (settings.Plain)
        {
            for (var i = 0; i < tests.Count; i++)
            {
                Console.WriteLine($"{i + 1,3}. {tests[i]}");
            }
            Console.Write("Select a test by number: ");
            if (!int.TryParse(Console.ReadLine(), out var choice) || choice < 1 || choice > tests.Count)
            {
                Console.WriteLine("Invalid selection.");
                return 1;
            }
            selected = tests[choice - 1];
        }
        else
        {
            var prompt = new SelectionPrompt<string>()
                .Title(settings.HotPink ? "[hotpink]Pick a test to run[/]" : "Pick a test to run")
                .PageSize(15)
                .AddChoices(tests);
            if (settings.HotPink)
            {
                prompt.HighlightStyle(new Style(Color.HotPink));
            }
            selected = AnsiConsole.Prompt(prompt);
        }

        // Strip any parameter list so parameterized test cases filter on the method name.
        var methodName = selected.Split('(')[0];
        output.Line($"Running: {selected}");
        var results = DotnetTestRunner.Run($"FullyQualifiedName~{methodName}");
        output.Results(results);
        return results.Any(r => r.Outcome == "Failed") ? 1 : 0;
    }
}
