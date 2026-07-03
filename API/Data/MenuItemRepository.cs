using Dapper;
using WebapplicationFactoryDemo.Api.Models;

namespace WebapplicationFactoryDemo.Api.Data;

public class MenuItemRepository(SqliteConnectionFactory connectionFactory) : IMenuItemRepository
{
    public IReadOnlyList<MenuItem> GetAll()
    {
        using var connection = connectionFactory.Open();
        return connection.Query<MenuItem>("SELECT * FROM MenuItems ORDER BY Id").ToList();
    }

    public IReadOnlyList<MenuItem> GetPage(int limit, int offset)
    {
        using var connection = connectionFactory.Open();
        return connection.Query<MenuItem>(
            "SELECT * FROM MenuItems ORDER BY Id LIMIT @limit OFFSET @offset",
            new { limit, offset }).ToList();
    }

    public int Count()
    {
        using var connection = connectionFactory.Open();
        return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM MenuItems");
    }

    public MenuItem? GetById(long id)
    {
        using var connection = connectionFactory.Open();
        return connection.QuerySingleOrDefault<MenuItem>(
            "SELECT * FROM MenuItems WHERE Id = @id", new { id });
    }

    public MenuItem Create(MenuItemRequest request)
    {
        using var connection = connectionFactory.Open();
        var id = connection.ExecuteScalar<long>(
            """
            INSERT INTO MenuItems (Name, Calories, Price, ProteinGrams, IsBreakfast)
            VALUES (@Name, @Calories, @Price, @ProteinGrams, @IsBreakfast);
            SELECT last_insert_rowid();
            """,
            request);

        return new MenuItem
        {
            Id = id,
            Name = request.Name,
            Calories = request.Calories,
            Price = request.Price,
            ProteinGrams = request.ProteinGrams,
            IsBreakfast = request.IsBreakfast,
        };
    }

    public bool Update(long id, MenuItemRequest request)
    {
        using var connection = connectionFactory.Open();
        return connection.Execute(
            """
            UPDATE MenuItems
            SET Name = @Name, Calories = @Calories, Price = @Price,
                ProteinGrams = @ProteinGrams, IsBreakfast = @IsBreakfast
            WHERE Id = @Id
            """,
            new { request.Name, request.Calories, request.Price, request.ProteinGrams, request.IsBreakfast, Id = id }) > 0;
    }

    public bool Delete(long id)
    {
        using var connection = connectionFactory.Open();
        return connection.Execute("DELETE FROM MenuItems WHERE Id = @id", new { id }) > 0;
    }
}
