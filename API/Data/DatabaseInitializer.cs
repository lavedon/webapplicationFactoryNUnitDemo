using Dapper;

namespace WebapplicationFactoryDemo.Api.Data;

public class DatabaseInitializer(SqliteConnectionFactory connectionFactory)
{
    public void EnsureCreatedAndSeeded()
    {
        using var connection = connectionFactory.Open();
        var tableExists = connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'MenuItems'") > 0;
        if (tableExists)
        {
            return;
        }

        connection.Execute(ReadSeedScript());
    }

    private static string ReadSeedScript()
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream("seed.sql")
            ?? throw new InvalidOperationException("Embedded resource 'seed.sql' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
