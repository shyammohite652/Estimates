using System.Security.Claims;
using System.Text;
using ConstructionEstimator.Application.Abstractions;
using ConstructionEstimator.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ConstructionEstimator.API.Security;

internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : ITokenIssuer
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(User user)
    {
        var o = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(o.ExpiryMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(AuthConstants.RoleClaim, user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(o), SecurityAlgorithms.HmacSha256),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions o)
    {
        var bytes = Encoding.UTF8.GetBytes(o.SigningKey);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes. Set it via configuration.");
        return new SymmetricSecurityKey(bytes);
    }
}
