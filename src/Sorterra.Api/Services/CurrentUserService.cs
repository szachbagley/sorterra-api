using System.Security.Claims;
using Sorterra.Core.Interfaces;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CognitoSub =>
        _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

    public string? Email =>
        _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;
}
