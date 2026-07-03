using Spectre.Console;

namespace WebapplicationFactoryRunner;

public enum OutputStyle
{
    Fancy,
    Plain,
    HotPink,
}

/// <summary>
/// All console output funnels through here so --plain and --hotpink apply everywhere.
/// </summary>
public class Output(OutputStyle style)
{
    public OutputStyle Style => style;

    public void Line(string text)
    {
        switch (style)
        {
            case OutputStyle.Plain:
                Console.WriteLine(text);
                break;
            case OutputStyle.HotPink:
                AnsiConsole.MarkupLine($"[hotpink]{Markup.Escape(text)}[/]");
                break;
            default:
                AnsiConsole.MarkupLine(Markup.Escape(text));
                break;
        }
    }

    /// <summary>Compact status table shown above the interactive menu each loop.</summary>
    public void StatusTable(string lastAction, IReadOnlyList<TestResult> results)
    {
        var passed = results.Count(r => r.Outcome == "Passed");
        var failed = results.Count(r => r.Outcome == "Failed");

        if (style == OutputStyle.Plain)
        {
            Console.WriteLine($"Last run: {lastAction} — Total: {results.Count}, Passed: {passed}, Failed: {failed}");
            return;
        }

        var pink = style == OutputStyle.HotPink;
        var table = new Table().RoundedBorder()
            .Title(pink ? "[hotpink]Last run[/]" : "[bold blue]Last run[/]");
        if (pink)
        {
            table.BorderColor(Color.HotPink);
        }
        table.AddColumn("Action");
        table.AddColumn("Total");
        table.AddColumn("Passed");
        table.AddColumn("Failed");
        table.AddRow(
            $"[{(pink ? "hotpink" : "default")}]{Markup.Escape(lastAction)}[/]",
            $"[{(pink ? "hotpink" : "default")}]{results.Count}[/]",
            $"[{(pink ? "hotpink" : "green")}]{passed}[/]",
            $"[{(pink ? "hotpink" : failed > 0 ? "red" : "grey")}]{failed}[/]");
        AnsiConsole.Write(table);
    }

    public void Results(IReadOnlyList<TestResult> results)
    {
        if (style == OutputStyle.Plain)
        {
            foreach (var r in results)
            {
                Console.WriteLine($"{r.Outcome,-10} {r.Name} ({r.Duration})");
                if (r.Error is not null)
                {
                    Console.WriteLine($"           {r.Error}");
                }
            }
            Console.WriteLine($"Total: {results.Count}, Passed: {results.Count(r => r.Outcome == "Passed")}, Failed: {results.Count(r => r.Outcome == "Failed")}");
            return;
        }

        var pink = style == OutputStyle.HotPink;
        var table = new Table().RoundedBorder();
        if (pink)
        {
            table.BorderColor(Color.HotPink);
        }
        table.AddColumn("Outcome");
        table.AddColumn("Test");
        table.AddColumn("Duration");

        foreach (var r in results)
        {
            var color = pink ? "hotpink" : r.Outcome switch
            {
                "Passed" => "green",
                "Failed" => "red",
                _ => "yellow",
            };
            table.AddRow(
                $"[{color}]{r.Outcome}[/]",
                $"[{(pink ? "hotpink" : "default")}]{Markup.Escape(r.Name)}[/]",
                $"[{(pink ? "hotpink" : "grey")}]{Markup.Escape(r.Duration)}[/]");
            if (r.Error is not null)
            {
                table.AddRow(string.Empty, $"[{(pink ? "hotpink" : "red")}]{Markup.Escape(r.Error)}[/]", string.Empty);
            }
        }
        AnsiConsole.Write(table);

        var passed = results.Count(r => r.Outcome == "Passed");
        var failed = results.Count(r => r.Outcome == "Failed");
        var summaryColor = pink ? "hotpink" : failed > 0 ? "red" : "green";
        AnsiConsole.MarkupLine($"[{summaryColor}]Total: {results.Count}, Passed: {passed}, Failed: {failed}[/]");
    }
}
