using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Application.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "currency-converter";
    public string Audience { get; set; } = "currency-converter";
    public string Key { get; set; } = "bf3ad3452f57c3212b7f65dd1c2286b2";
    public int AccessTokenMinutes { get; set; } = 60;
    public TokenValidationParameters ToTokenValidationParameters() => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        ClockSkew = TimeSpan.FromSeconds(5)
    };
}
