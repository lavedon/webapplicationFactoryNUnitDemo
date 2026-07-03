using System.Net;
using System.Net.Http.Json;
using WebapplicationFactoryDemo.Api.Models;
using WebapplicationFactoryTests.Infrastructure;

namespace WebapplicationFactoryTests;

[TestFixture]
public class ItemsEndpointTests : ApiTestBase
{
    [Test]
    public async Task GetAll_ReturnsAllSeededItems()
    {
        var items = await Client.GetFromJsonAsync<List<MenuItem>>("/api/items");

        Assert.That(items, Has.Count.EqualTo(83));
        Assert.That(items![0].Name, Is.EqualTo("Beefy Melt Burrito"));
    }

    [Test]
    public async Task GetAll_Paged_ReturnsEnvelope()
    {
        var page = await Client.GetFromJsonAsync<PagedResponse<MenuItem>>(
            "/api/items?page=2&pageSize=10");

        Assert.Multiple(() =>
        {
            Assert.That(page!.Items, Has.Count.EqualTo(10));
            Assert.That(page.Page, Is.EqualTo(2));
            Assert.That(page.PageSize, Is.EqualTo(10));
            Assert.That(page.TotalCount, Is.EqualTo(83));
            Assert.That(page.TotalPages, Is.EqualTo(9));
        });
    }

    [TestCase("/api/items?page=0&pageSize=10")]
    [TestCase("/api/items?page=1&pageSize=0")]
    [TestCase("/api/items?page=1&pageSize=101")]
    public async Task GetAll_Paged_InvalidParams_Returns400(string url)
    {
        var response = await Client.GetAsync(url);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetById_ReturnsItem()
    {
        var item = await Client.GetFromJsonAsync<MenuItem>("/api/items/1");

        Assert.Multiple(() =>
        {
            Assert.That(item!.Name, Is.EqualTo("Beefy Melt Burrito"));
            Assert.That(item.Calories, Is.EqualTo(620));
            Assert.That(item.IsBreakfast, Is.False);
        });
    }

    [Test]
    public async Task GetById_Unknown_Returns404ProblemDetails()
    {
        var response = await Client.GetAsync("/api/items/99999");

        Assert.Multiple(async () =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(response.Content.Headers.ContentType!.MediaType,
                Is.EqualTo("application/problem+json"));
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Does.Contain("Menu item not found"));
        });
    }

    [Test]
    public async Task Create_Returns201WithLocation_AndPersists()
    {
        var request = new MenuItemRequest
        {
            Name = "Enchirito",
            Calories = 360,
            Price = 3.69,
            ProteinGrams = 17,
            IsBreakfast = false,
        };

        var response = await Client.PostAsJsonAsync("/api/items", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);

        var fetched = await Client.GetFromJsonAsync<MenuItem>(response.Headers.Location);
        Assert.That(fetched!.Name, Is.EqualTo("Enchirito"));
    }

    [Test]
    public async Task Create_InvalidBody_Returns400ValidationProblem()
    {
        var response = await Client.PostAsJsonAsync("/api/items",
            new { name = "", calories = -5, price = -1.0, proteinGrams = -2 });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("Name"));
    }

    [Test]
    public async Task Update_Returns204_AndPersists()
    {
        var request = new MenuItemRequest
        {
            Name = "Beefy Melt Burrito XL",
            Calories = 700,
            Price = 2.99,
            ProteinGrams = 24,
            IsBreakfast = false,
        };

        var response = await Client.PutAsJsonAsync("/api/items/1", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var updated = await Client.GetFromJsonAsync<MenuItem>("/api/items/1");
        Assert.That(updated!.Name, Is.EqualTo("Beefy Melt Burrito XL"));
    }

    [Test]
    public async Task Update_Unknown_Returns404()
    {
        var request = new MenuItemRequest { Name = "Ghost Item", Calories = 1, Price = 1, ProteinGrams = 1 };

        var response = await Client.PutAsJsonAsync("/api/items/99999", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Returns204_AndRemovesItem()
    {
        var response = await Client.DeleteAsync("/api/items/1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var followUp = await Client.GetAsync("/api/items/1");
        Assert.That(followUp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Unknown_Returns404()
    {
        var response = await Client.DeleteAsync("/api/items/99999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
