using Microsoft.Data.Sqlite;

namespace WebapplicationFactoryTests.Infrastructure;

/// <summary>
/// Each test gets a brand-new temp-file SQLite database, seeded by the app's own
/// startup initializer, and an HttpClient with auth bypassed.
/// </summary>
public abstract class ApiTestBase
{
    private TacoBellApiFactory _factory = null!;
    protected string DatabasePath { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    [SetUp]
    public void SetUpFactory()
    {
        DatabasePath = Path.Combine(Path.GetTempPath(), $"tacobell-test-{Guid.NewGuid():N}.db");
        _factory = new TacoBellApiFactory(DatabasePath);
        Client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDownFactory()
    {
        Client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        // Tests may spin up extra factories on suffixed paths (e.g. "-noauth"); pools are
        // cleared above, so everything sharing the temp-file prefix is safe to delete.
        foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(DatabasePath) + "*"))
        {
            File.Delete(file);
        }
    }
}
