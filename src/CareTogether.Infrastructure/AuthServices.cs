using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CareTogether.Application;
using CareTogether.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CareTogether.Infrastructure;

public sealed class PasswordService
{
    public string Hash(User user, string password) { var salt=RandomNumberGenerator.GetBytes(16); var derived=Rfc2898DeriveBytes.Pbkdf2(password,salt,100_000,HashAlgorithmName.SHA256,32); return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(derived)}"; }
    public bool Verify(User user, string hash, string password) { var parts=hash.Split('.'); if(parts.Length!=2)return false; var salt=Convert.FromBase64String(parts[0]); var expected=Convert.FromBase64String(parts[1]); return CryptographicOperations.FixedTimeEquals(expected,Rfc2898DeriveBytes.Pbkdf2(password,salt,100_000,HashAlgorithmName.SHA256,32)); }
}

public sealed class TokenService(IConfiguration config) : ITokenService
{
    public AuthResponse Issue(User user)
    {
        var expiry = DateTimeOffset.UtcNow.AddMinutes(int.TryParse(config["Jwt:AccessMinutes"], out var minutes) ? minutes : 30);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required")));
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName), new(ClaimTypes.Role, user.Role.ToString())
        ], expires: expiry.UtcDateTime, signingCredentials: new(key, SecurityAlgorithms.HmacSha256));
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, expiry, user.Id, user.DisplayName, user.Role);
    }
    public string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
