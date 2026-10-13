using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Forest.Minimal.Account.Services.Bearer.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Forest.Minimal.Account.Services.Bearer.Generator;

public sealed class BearerGenerator(BearerConfiguration configuration) : IBearerGenerator
{
    public Task<BearerTransport> GenerateAsync(IDictionary<string, object> claims, IDictionary<string, object> refreshClaims, CancellationToken token = default)
    {
        var exp = DateTime.UtcNow.AddDays(configuration.Exp);
        var refreshExp = exp.AddDays(configuration.RefreshExp);
        var accessToken = Generate(claims, exp);
        var refreshToken = Generate(refreshClaims, refreshExp);
        return Task.FromResult<BearerTransport>(new(accessToken, refreshToken, exp, refreshExp));
    }
    private string Generate(IDictionary<string, object> claims, DateTime exp)
        => _handler.CreateToken(new SecurityTokenDescriptor()
        {
            Claims = claims,
            Audience = configuration.Audience,
            Issuer = configuration.Issuer,
            Expires = exp,
            SigningCredentials = _signingCredentials
        });
    private readonly SigningCredentials _signingCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.HashKey)), SecurityAlgorithms.HmacSha256Signature);
    private readonly JsonWebTokenHandler _handler = new();
}