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

    public static IReadOnlyList<TestResult> Run(string baseUrl)
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
