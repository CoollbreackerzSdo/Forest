namespace Forest.Minimal.Account.Services.Bearer.Models;

public sealed record BearerTransport(string Token, string RefreshToken, DateTimeOffset Exp, DateTimeOffset RefreshExp);

