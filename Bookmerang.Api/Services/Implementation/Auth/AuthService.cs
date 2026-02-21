using Bookmerang.Api.Services.Interfaces.Auth;

namespace Bookmerang.Api.Services.Implementation.Auth;

public class AuthService : IAuthService
{
    public AuthService()
    {
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        // TODO: Implementar lógica de login
        throw new NotImplementedException();
    }

    public async Task<string> RegisterAsync(string email, string password, string name)
    {
        // TODO: Implementar lógica de registro
        throw new NotImplementedException();
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        // TODO: Implementar validación de token
        throw new NotImplementedException();
    }
}
