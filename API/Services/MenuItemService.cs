using WebapplicationFactoryDemo.Api.Data;
using WebapplicationFactoryDemo.Api.Models;

namespace WebapplicationFactoryDemo.Api.Services;

public class MenuItemService(IMenuItemRepository repository) : IMenuItemService
{
    public IReadOnlyList<MenuItem> GetAll() => repository.GetAll();

    public PagedResponse<MenuItem> GetPaged(int page, int pageSize)
    {
        var totalCount = repository.Count();
        var items = repository.GetPage(pageSize, (page - 1) * pageSize);
        return new PagedResponse<MenuItem>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public MenuItem? GetById(long id) => repository.GetById(id);

    public MenuItem Create(MenuItemRequest request) => repository.Create(request);

    public bool Update(long id, MenuItemRequest request) => repository.Update(id, request);

    public bool Delete(long id) => repository.Delete(id);
}
