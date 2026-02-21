namespace Bookmerang.Api.Services.Interfaces.Auth;

public interface IAuthService
{
    Task<string> LoginAsync(string email, string password);
    Task<string> RegisterAsync(string email, string password, string name);
    Task<bool> ValidateTokenAsync(string token);
}
