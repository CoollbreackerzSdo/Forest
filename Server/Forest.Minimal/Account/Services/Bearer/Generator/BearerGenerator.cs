using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Forest.Minimal.Account.Services.Bearer.Models;
using Microsoft.IdentityModel.Tokens;

namespace Forest.Minimal.Account.Services.Bearer.Generator;

public sealed class BearerGenerator(BearerConfiguration configuration) : IBearerGenerator
{
    public async Task<BearerTransport> GenerateAsync(IEnumerable<Claim> claims, IEnumerable<Claim>? refreshClaims = null, CancellationToken token = default)
    {
        var exp = DateTimeOffset.UtcNow.AddDays(configuration.Exp);
        var refreshExp = exp.AddDays(configuration.RefreshExp);
        var accessToken = GenerateAsync(claims, exp.DateTime);
        var refreshToken = GenerateAsync(refreshClaims, refreshExp.DateTime);
        await Task.WhenAll(accessToken, refreshToken);
        return new(accessToken.Result, refreshToken.Result, exp, refreshExp);
    }

    private Task<string> GenerateAsync(IEnumerable<Claim>? claims, DateTime exp)
        => Task.FromResult(new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: configuration.Issuer,
            audience: configuration.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: exp,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.HashKey)),
                SecurityAlgorithms.HmacSha256Signature
            )
        )));
}