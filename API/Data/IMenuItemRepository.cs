using WebapplicationFactoryDemo.Api.Models;

namespace WebapplicationFactoryDemo.Api.Data;

public interface IMenuItemRepository
{
    IReadOnlyList<MenuItem> GetAll();
    IReadOnlyList<MenuItem> GetPage(int limit, int offset);
    int Count();
    MenuItem? GetById(long id);
    MenuItem Create(MenuItemRequest request);
    bool Update(long id, MenuItemRequest request);
    bool Delete(long id);
}
