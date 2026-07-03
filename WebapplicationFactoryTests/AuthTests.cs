using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebapplicationFactoryDemo.Api.Models;
using WebapplicationFactoryTests.Infrastructure;

namespace WebapplicationFactoryTests;

[TestFixture]
public class AuthTests : ApiTestBase
{
    [Test]
    public async Task WhoAmI_WithAuthBypass_ReturnsTestClientClaims()
    {
        var response = await Client.GetAsync("/whoami");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("test-client"));
    }

    [Test]
    public async Task Endpoints_WithoutAuthBypass_Return401()
    {
        using var realAuthFactory = new TacoBellApiFactory(DatabasePath + "-noauth", bypassAuth: false);
        using var client = realAuthFactory.CreateClient();

        var items = await client.GetAsync("/api/items");
        var whoami = await client.GetAsync("/whoami");

        Assert.Multiple(() =>
        {
            Assert.That(items.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(whoami.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task Token_WithValidCredentials_IssuesJwtThatWorks()
    {
        using var realAuthFactory = new TacoBellApiFactory(DatabasePath + "-token", bypassAuth: false);
        using var client = realAuthFactory.CreateClient();

        var tokenResponse = await client.PostAsJsonAsync("/token",
            new TokenRequest { ClientId = "demo-client", ClientSecret = "demo-secret" });
        Assert.That(tokenResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.That(token!.TokenType, Is.EqualTo("Bearer"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var whoami = await client.GetAsync("/whoami");

        Assert.That(whoami.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await whoami.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("demo-client"));
    }

    [Test]
    public async Task Token_WithBadCredentials_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/token",
            new TokenRequest { ClientId = "demo-client", ClientSecret = "wrong" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
