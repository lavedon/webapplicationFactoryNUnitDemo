using Spectre.Console;

namespace WebapplicationFactoryRunner;

public static class HelpScreen
{
    public static void Render()
    {
        AnsiConsole.Write(new FigletText("runner").Color(Color.Purple));
        AnsiConsole.MarkupLine("[bold]Taco Bell API demo test runner[/] — NUnit (in-process) and Pester (black-box HTTP) suites.\n");

        var commands = new Table().RoundedBorder().Title("[bold]Commands[/]");
        commands.AddColumn("Command");
        commands.AddColumn("What it does");
        commands.AddColumn("Example");
        commands.AddRow(
            "[green]run[/]",
            "Run the whole NUnit suite via dotnet test",
            "[grey]runner run[/]");
        commands.AddRow(
            "[green]run -f|--filter <expr>[/]",
            "Run matching NUnit tests (pass-through to dotnet test --filter)",
            "[grey]runner run -f \"FullyQualifiedName~AuthTests\"[/]");
        commands.AddRow(
            "[green]pester[/]",
            "Run the Pester suite against a RUNNING API (start it first: cd API; dotnet run)",
            "[grey]runner pester[/]");
        commands.AddRow(
            "[green]pester -u|--url <baseurl>[/]",
            "Same, against a different API instance",
            "[grey]runner pester -u https://localhost:7016[/]");
        commands.AddRow(
            "[green]interactive[/]",
            "Menu mode: pick NUnit or Pester, then run ALL or a single test",
            "[grey]runner interactive[/]");
        AnsiConsole.Write(commands);

        var switches = new Table().RoundedBorder().Title("[bold]Global switches (work on every command)[/]");
        switches.AddColumn("Switch");
        switches.AddColumn("Effect");
        switches.AddRow("[green]--plain[/]", "Plain Console.WriteLine output, no colors or tables");
        switches.AddRow("[green]--hotpink[/]", "[hotpink]Everything in hot pink[/] (mutually exclusive with --plain)");
        switches.AddRow("[green]-h|--help[/]", "Detailed option help for any command");
        AnsiConsole.Write(switches);

        AnsiConsole.MarkupLine("\n[grey]Every run also prints the equivalent manual command (dotnet test / Invoke-Pester),[/]");
        AnsiConsole.MarkupLine("[grey]so you can reproduce it without the runner.[/]");
    }
}
