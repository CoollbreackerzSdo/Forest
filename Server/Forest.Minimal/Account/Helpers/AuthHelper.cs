using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Forest.Minimal.Account.Helpers;

public sealed class AuthHelper
{
    public const string AuthenticationScheme = JwtBearerDefaults.AuthenticationScheme;
    public const string AuthorizationDefaultPolicy = "default";
}