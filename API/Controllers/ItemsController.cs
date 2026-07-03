using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebapplicationFactoryDemo.Api.Data;
using WebapplicationFactoryDemo.Api.Models;

namespace WebapplicationFactoryDemo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/items")]
public class ItemsController(SqliteConnectionFactory connectionFactory) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        using var connection = connectionFactory.Open();

        if (page is null && pageSize is null)
        {
            return Ok(connection.Query<MenuItem>("SELECT * FROM MenuItems ORDER BY Id"));
        }

        var currentPage = page ?? 1;
        var size = pageSize ?? 20;
        if (currentPage < 1)
        {
            ModelState.AddModelError("page", "page must be >= 1.");
        }
        if (size is < 1 or > 100)
        {
            ModelState.AddModelError("pageSize", "pageSize must be between 1 and 100.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var totalCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM MenuItems");
        var items = connection.Query<MenuItem>(
            "SELECT * FROM MenuItems ORDER BY Id LIMIT @Limit OFFSET @Offset",
            new { Limit = size, Offset = (currentPage - 1) * size }).ToList();

        return Ok(new PagedResponse<MenuItem>(
            items, currentPage, size, totalCount,
            (int)Math.Ceiling(totalCount / (double)size)));
    }

    [HttpGet("{id:long}")]
    public IActionResult GetById(long id)
    {
        using var connection = connectionFactory.Open();
        var item = connection.QuerySingleOrDefault<MenuItem>(
            "SELECT * FROM MenuItems WHERE Id = @id", new { id });
        return item is null ? NotFoundProblem(id) : Ok(item);
    }

    [HttpPost]
    public IActionResult Create(MenuItemRequest request)
    {
        using var connection = connectionFactory.Open();
        var id = connection.ExecuteScalar<long>(
            """
            INSERT INTO MenuItems (Name, Calories, Price, ProteinGrams, IsBreakfast)
            VALUES (@Name, @Calories, @Price, @ProteinGrams, @IsBreakfast);
            SELECT last_insert_rowid();
            """,
            request);

        var created = new MenuItem
        {
            Id = id,
            Name = request.Name,
            Calories = request.Calories,
            Price = request.Price,
            ProteinGrams = request.ProteinGrams,
            IsBreakfast = request.IsBreakfast,
        };
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpPut("{id:long}")]
    public IActionResult Update(long id, MenuItemRequest request)
    {
        using var connection = connectionFactory.Open();
        var rows = connection.Execute(
            """
            UPDATE MenuItems
            SET Name = @Name, Calories = @Calories, Price = @Price,
                ProteinGrams = @ProteinGrams, IsBreakfast = @IsBreakfast
            WHERE Id = @Id
            """,
            new { request.Name, request.Calories, request.Price, request.ProteinGrams, request.IsBreakfast, Id = id });
        return rows == 0 ? NotFoundProblem(id) : NoContent();
    }

    [HttpDelete("{id:long}")]
    public IActionResult Delete(long id)
    {
        using var connection = connectionFactory.Open();
        var rows = connection.Execute("DELETE FROM MenuItems WHERE Id = @id", new { id });
        return rows == 0 ? NotFoundProblem(id) : NoContent();
    }

    private ObjectResult NotFoundProblem(long id) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Menu item not found",
            detail: $"No menu item with id {id} exists.");
}
