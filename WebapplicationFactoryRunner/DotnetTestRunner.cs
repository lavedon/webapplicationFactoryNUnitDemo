using System.Diagnostics;
using System.Xml.Linq;

namespace WebapplicationFactoryRunner;

public static class DotnetTestRunner
{
    private static readonly XNamespace TrxNs =
        "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static string FindTestProject()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "WebapplicationFactoryTests", "WebapplicationFactoryTests.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate WebapplicationFactoryTests.csproj. Run from inside the repository.");
    }

    public static IReadOnlyList<TestResult> Run(string? filter)
    {
        var project = FindTestProject();
        var resultsDir = Path.Combine(Path.GetTempPath(), $"wafd-trx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(resultsDir);
        try
        {
            var args = $"test \"{project}\" --nologo --logger trx --results-directory \"{resultsDir}\"";
            if (!string.IsNullOrWhiteSpace(filter))
            {
                args += $" --filter \"{filter}\"";
            }
            Execute(args);

            var trx = Directory.GetFiles(resultsDir, "*.trx").SingleOrDefault()
                ?? throw new InvalidOperationException("dotnet test produced no TRX file.");
            return ParseTrx(trx);
        }
        finally
        {
            Directory.Delete(resultsDir, recursive: true);
        }
    }

    public static IReadOnlyList<string> ListTests()
    {
        var project = FindTestProject();
        var output = Execute($"test \"{project}\" --nologo --list-tests");

        var tests = new List<string>();
        var collecting = false;
        foreach (var line in output.Split('\n'))
        {
            if (line.Contains("The following Tests are available:"))
            {
                collecting = true;
                continue;
            }
            if (collecting && line.StartsWith("    ", StringComparison.Ordinal))
            {
                tests.Add(line.Trim());
            }
        }
        return tests;
    }

    private static string Execute(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return stdout;
    }

    private static List<TestResult> ParseTrx(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Descendants(TrxNs + "UnitTestResult")
            .Select(r => new TestResult(
                r.Attribute("testName")?.Value ?? "(unknown)",
                r.Attribute("outcome")?.Value ?? "Unknown",
                TimeSpan.TryParse(r.Attribute("duration")?.Value, out var d)
                    ? $"{d.TotalMilliseconds:F0} ms"
                    : "?",
                r.Descendants(TrxNs + "Message").FirstOrDefault()?.Value.Split('\n')[0].Trim()))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
    }
}
