

namespace Application.Contracts.Auth
{
    public interface ITokenService
    {
        string CreateToken(string userId, string clientId, IEnumerable<string> roles);
    }
}
