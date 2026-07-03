using Microsoft.Data.Sqlite;

namespace WebapplicationFactoryDemo.Api.Data;

public class SqliteConnectionFactory(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Default") ?? "Data Source=tacobell.db";

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
