using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebapplicationFactoryDemo.Api.Models;
using WebapplicationFactoryDemo.Api.Services;

namespace WebapplicationFactoryDemo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/items")]
public class ItemsController(IMenuItemService menuItems) : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        if (page is null && pageSize is null)
        {
            return Ok(menuItems.GetAll());
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

        return Ok(menuItems.GetPaged(currentPage, size));
    }

    [HttpGet("{id:long}")]
    public IActionResult GetById(long id)
    {
        var item = menuItems.GetById(id);
        return item is null ? NotFoundProblem(id) : Ok(item);
    }

    [HttpPost]
    public IActionResult Create(MenuItemRequest request)
    {
        var created = menuItems.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public IActionResult Update(long id, MenuItemRequest request) =>
        menuItems.Update(id, request) ? NoContent() : NotFoundProblem(id);

    [HttpDelete("{id:long}")]
    public IActionResult Delete(long id) =>
        menuItems.Delete(id) ? NoContent() : NotFoundProblem(id);

    private ObjectResult NotFoundProblem(long id) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Menu item not found",
            detail: $"No menu item with id {id} exists.");
}
