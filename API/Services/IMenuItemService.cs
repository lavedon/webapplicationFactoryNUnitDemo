using WebapplicationFactoryDemo.Api.Models;

namespace WebapplicationFactoryDemo.Api.Services;

public interface IMenuItemService
{
    IReadOnlyList<MenuItem> GetAll();
    PagedResponse<MenuItem> GetPaged(int page, int pageSize);
    MenuItem? GetById(long id);
    MenuItem Create(MenuItemRequest request);
    bool Update(long id, MenuItemRequest request);
    bool Delete(long id);
}
