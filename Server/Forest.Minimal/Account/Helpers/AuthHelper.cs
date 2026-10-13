using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Forest.Minimal.Account.Helpers;

public sealed class AuthHelper
{
    public const string DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    public const string DefaultPolicy = "default";
    public const string TokenCookieParameter = "auth_token";
}