using Spectre.Console.Cli;
using WebapplicationFactoryRunner;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("runner");
    config.AddCommand<RunCommand>("run")
        .WithDescription("Run the test suite (optionally filtered) and render results.");
    config.AddCommand<InteractiveCommand>("interactive")
        .WithDescription("Pick an individual test interactively and run it.");
    config.AddCommand<PesterCommand>("pester")
        .WithDescription("Run the black-box Pester tests against a running API instance.");
});
// Bare `runner` (no args) shows the help screen, not the test menu.
if (args.Length == 0)
{
    HelpScreen.Render();
    return 0;
}
return app.Run(args);
