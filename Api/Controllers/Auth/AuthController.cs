
using Application.Contracts.Auth;
using Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace Api.Controllers;

public record LoginRequest(string Username, string Password);
public record TokenResponse(string AccessToken, int ExpiresInSeconds);

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController(ITokenService tokens) : ControllerBase
{
    
    private static readonly Dictionary<string, (string pwd, string[] roles, string clientId)> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hasan"] = ("currency@123", new[] { "User" }, "client-hasan"),
        ["admin"] = ("currency@123", new[] { "Admin", "User" }, "client-admin")
    };

    [HttpPost("token")]
    public ActionResult<TokenResponse> Token([FromBody] LoginRequest req, [FromServices] Microsoft.Extensions.Options.IOptions<JwtOptions> jwt)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Username and password are required.");

        if (!Users.TryGetValue(req.Username, out var entry) || entry.pwd != req.Password)
            return Unauthorized("Invalid credentials.");

        var jwtStr = tokens.CreateToken(userId: req.Username, clientId: entry.clientId, roles: entry.roles);
        return Ok(new TokenResponse(jwtStr, jwt.Value.AccessTokenMinutes * 60));
    }
}


