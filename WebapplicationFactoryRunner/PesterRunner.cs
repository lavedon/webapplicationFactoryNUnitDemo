using System.Diagnostics;
using System.Xml.Linq;

namespace WebapplicationFactoryRunner;

public static class PesterRunner
{
    public static string FindPesterTests()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "PesterTests");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate the PesterTests folder. Run from inside the repository.");
    }

    public static string FindApiProject()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "API", "webapplicationFactoryDemo.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate API/webapplicationFactoryDemo.csproj. Run from inside the repository.");
    }

    /// <summary>
    /// Ensures the API is running: returns null if it already was, otherwise starts it
    /// with `dotnet run` and returns the process for the caller to kill when done.
    /// Throws if the API cannot be reached after startup.
    /// </summary>
    public static Process? EnsureApiRunning(string baseUrl, Action<string> log)
    {
        if (IsApiReachable(baseUrl))
        {
            return null;
        }

        log($"API not running at {baseUrl} — starting it (dotnet run)...");
        var process = Process.Start(new ProcessStartInfo(
            "dotnet", $"run --project \"{FindApiProject()}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

        for (var attempt = 0; attempt < 60; attempt++)
        {
            Thread.Sleep(500);
            if (IsApiReachable(baseUrl))
            {
                log("API is up.");
                return process;
            }
            if (process.HasExited)
            {
                break;
            }
        }

        try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
        throw new InvalidOperationException(
            $"Started the API but it never became reachable at {baseUrl}. " +
            "Check that the URL matches launchSettings.json (or pass --url).");
    }

    public static void StopApi(Process? process)
    {
        if (process is null)
        {
            return;
        }
        try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
    }

    public static bool IsApiReachable(string baseUrl)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            using var response = client.GetAsync($"{baseUrl}/openapi/v1.json").GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public static IReadOnlyList<string> ListTests()
    {
        var testsPath = FindPesterTests();
        var script =
            $"$c = New-PesterConfiguration; " +
            $"$c.Run.Path = '{testsPath}'; " +
            $"$c.Run.SkipRun = $true; " +
            $"$c.Run.PassThru = $true; " +
            $"$c.Output.Verbosity = 'None'; " +
            $"(Invoke-Pester -Configuration $c).Tests | ForEach-Object {{ $_.ExpandedPath }}";

        var startInfo = new ProcessStartInfo("pwsh", $"-NoProfile -Command \"{script}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();

        return stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static IReadOnlyList<TestResult> Run(string baseUrl, string? fullNameFilter = null)
    {
        var testsPath = FindPesterTests();
        var resultFile = Path.Combine(Path.GetTempPath(), $"wafd-pester-{Guid.NewGuid():N}.xml");
        try
        {
            var script =
                $"$c = New-PesterConfiguration; " +
                $"$c.Run.Path = '{testsPath}'; " +
                $"$c.Output.Verbosity = 'None'; " +
                $"$c.TestResult.Enabled = $true; " +
                $"$c.TestResult.OutputFormat = 'NUnitXml'; " +
                $"$c.TestResult.OutputPath = '{resultFile}'; " +
                (fullNameFilter is null ? "" : $"$c.Filter.FullName = '{fullNameFilter.Replace("'", "''")}'; ") +
                $"Invoke-Pester -Configuration $c";

            var startInfo = new ProcessStartInfo("pwsh", $"-NoProfile -Command \"{script}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.Environment["TACOBELL_API_URL"] = baseUrl;

            using var process = Process.Start(startInfo)!;
            process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!File.Exists(resultFile))
            {
                throw new InvalidOperationException(
                    $"Pester produced no result file. Is Pester 5 installed? " +
                    $"(Install-Module Pester -Force -Scope CurrentUser -SkipPublisherCheck){Environment.NewLine}{stderr}");
            }
            return ParseNUnitXml(resultFile);
        }
        finally
        {
            File.Delete(resultFile);
        }
    }

    private static List<TestResult> ParseNUnitXml(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Descendants("test-case")
            .Select(tc => new TestResult(
                tc.Attribute("description")?.Value ?? tc.Attribute("name")?.Value ?? "(unknown)",
                tc.Attribute("result")?.Value switch
                {
                    "Success" => "Passed",
                    "Failure" => "Failed",
                    var other => other ?? "Unknown",
                },
                double.TryParse(tc.Attribute("time")?.Value, out var seconds)
                    ? $"{seconds * 1000:F0} ms"
                    : "?",
                tc.Descendants("message").FirstOrDefault()?.Value.Split('\n')[0].Trim()))
            .ToList();
    }
}
