using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Sorterra.Api.Services;

public class ConsentStateService
{
    private readonly SymmetricSecurityKey _signingKey;

    public ConsentStateService(IConfiguration config)
    {
        var keyStr = config["Encryption:TokenEncryptionKey"]
            ?? throw new InvalidOperationException("Encryption:TokenEncryptionKey is not configured");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
    }

    public string CreateState(Guid connectionId)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("connectionId", connectionId.ToString()),
            new Claim("nonce", Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (Guid ConnectionId, bool IsValid) ValidateState(string stateToken)
    {
        var handler = new JwtSecurityTokenHandler();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = _signingKey
        };

        try
        {
            var principal = handler.ValidateToken(stateToken, validationParameters, out _);
            var connectionIdClaim = principal.FindFirst("connectionId")?.Value;

            if (connectionIdClaim != null && Guid.TryParse(connectionIdClaim, out var connectionId))
            {
                return (connectionId, true);
            }

            return (Guid.Empty, false);
        }
        catch
        {
            return (Guid.Empty, false);
        }
    }
}
