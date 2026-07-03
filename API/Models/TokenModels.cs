using System.ComponentModel.DataAnnotations;

namespace WebapplicationFactoryDemo.Api.Models;

public class TokenRequest
{
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;
}

public record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
