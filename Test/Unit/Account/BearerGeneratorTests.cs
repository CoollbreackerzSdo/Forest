using Forest.Minimal.Account.Services.Bearer;
using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Test.Support;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Forest.Test.Unit.Account;

public sealed class BearerGeneratorTests
{
    private readonly BearerGenerator _generator = new(TestData.BearerConfiguration);

    private static Dictionary<string, object> Claims => new()
    {
        { JwtRegisteredClaimNames.Sub, Guid.CreateVersion7().ToString() },
        { JwtRegisteredClaimNames.Email, "usuario@forest.dev" },
        { JwtRegisteredClaimNames.UniqueName, "usuario" }
    };

    [Fact]
    public async Task GenerateAsync_DeberiaEmitirTokenDeAccesoYDeRefresco()
    {
        var result = await _generator.GenerateAsync(Claims, new Dictionary<string, object>());

        Assert.NotEmpty(result.Token);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotEqual(result.Token, result.RefreshToken);
    }

    [Fact]
    public async Task GenerateAsync_DeberiaIncluirLosClaimsDelUsuario()
    {
        var claims = Claims;

        var result = await _generator.GenerateAsync(claims, new Dictionary<string, object>());

        var token = new JsonWebToken(result.Token);
        Assert.Equal(claims[JwtRegisteredClaimNames.Sub], token.Subject);
        Assert.Equal(claims[JwtRegisteredClaimNames.Email], token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(claims[JwtRegisteredClaimNames.UniqueName], token.GetClaim(JwtRegisteredClaimNames.UniqueName).Value);
    }

    [Fact]
    public async Task GenerateAsync_DeberiaFirmarConLaAudienceYElIssuerConfigurados()
    {
        var result = await _generator.GenerateAsync(Claims, new Dictionary<string, object>());

        var token = new JsonWebToken(result.Token);
        Assert.Equal(TestData.Audience, Assert.Single(token.Audiences));
        Assert.Equal(TestData.Issuer, token.Issuer);
    }

    [Fact]
    public async Task GenerateAsync_DeberiaRespetarLaCaducidadConfigurada()
    {
        var result = await _generator.GenerateAsync(Claims, new Dictionary<string, object>());

        Assert.Equal(DateTimeOffset.UtcNow.AddDays(TestData.ExpirationDays).Date, result.Exp.Date);
        Assert.Equal(result.Exp.AddDays(TestData.RefreshExpirationDays), result.RefreshExp);
    }

    [Fact]
    public async Task GenerateAsync_DeberiaProducirUnTokenValidableConLaClaveDeLaAplicacion()
    {
        var result = await _generator.GenerateAsync(Claims, new Dictionary<string, object>());

        var parameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidAudience = TestData.Audience,
            ValidIssuer = TestData.Issuer,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(TestData.BearerKey))
        };

        // MapInboundClaims = false replica la configuracion de AddAuth() en Forest.Minimal.
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(result.Token, parameters, out _);

        Assert.Contains(principal.Claims, x => x.Type == "email" && x.Value == "usuario@forest.dev");
        Assert.Contains(principal.Claims, x => x.Type == "unique_name" && x.Value == "usuario");
    }
}