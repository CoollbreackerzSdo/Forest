using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Forest.Minimal.Account.Services.Bearer.Events;

public sealed class MapCookieEvent
{
    internal static async Task OnMessageReceived(MessageReceivedContext context)
    {
        ReadOnlySpan<char> authorizationHeader = context.Request.Headers.Authorization;
        if (!authorizationHeader.IsEmpty && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return;

        if (context.Request.Cookies.TryGetValue("auth_token", out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
            context.Token = cookieToken;

        return;
    }
}