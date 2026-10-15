using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Forest.Minimal.Account.Services.Bearer.Events;

public sealed class ValidateLifeBearerEvent
{
    internal static async Task OnTokenValidated(TokenValidatedContext context)
    {
        var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!Guid.TryParse(jti, out var id))
        {
            context.Fail("Token de session alterado");
            return;
        }
        var key = new EntityId(id);

        var tokenData = (
            from x in context.HttpContext.RequestServices.GetRequiredService<ForestContext>().Tokens
            where x.Key == key || x.RefreshId == key
            select new { x.Exp, x.RefreshExp }
            ).FirstOrDefault(); ;
        var currentTime = DateTimeOffset.UtcNow;
        if (tokenData is null || tokenData.Exp < currentTime || tokenData.RefreshExp < currentTime)
        {
            context.Fail("Token expirado");
            return;
        }
    }
}