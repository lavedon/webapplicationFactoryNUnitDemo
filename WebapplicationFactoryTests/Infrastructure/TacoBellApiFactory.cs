using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace WebapplicationFactoryTests.Infrastructure;

/// <summary>
/// WebApplicationFactory over the real Program, pointed at a fresh temp-file SQLite
/// database. When <paramref name="bypassAuth"/> is true the Test scheme replaces JWT
/// bearer as the default, so requests authenticate without a token.
/// </summary>
public class TacoBellApiFactory(string databasePath, bool bypassAuth = true)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={databasePath}");

        if (bypassAuth)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });
            });
        }
    }
}
