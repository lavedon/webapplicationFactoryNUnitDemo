using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using WebapplicationFactoryDemo.Api.Models;

namespace WebapplicationFactoryDemo.Api.Controllers;

[ApiController]
public class AuthController(IConfiguration configuration) : ControllerBase
{
    private const int TokenLifetimeSeconds = 3600;

    [HttpPost("token")]
    [AllowAnonymous]
    public IActionResult Token(TokenRequest request)
    {
        var auth = configuration.GetSection("Auth");
        if (request.ClientId != auth["ClientId"] || request.ClientSecret != auth["ClientSecret"])
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid client credentials");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth["SigningKey"]!));
        var token = new JwtSecurityToken(
            issuer: auth["Issuer"],
            audience: auth["Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, request.ClientId),
                new Claim("client_id", request.ClientId),
            ],
            expires: DateTime.UtcNow.AddSeconds(TokenLifetimeSeconds),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return Ok(new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            TokenLifetimeSeconds));
    }

    [HttpGet("whoami")]
    [Authorize]
    public IActionResult WhoAmI() =>
        Ok(User.Claims.Select(c => new { c.Type, c.Value }));
}
