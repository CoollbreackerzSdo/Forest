using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Common.Handlers;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Forest.Minimal.Account.Handlers;

public sealed class SignInHandler(IPasswordHasher<SignInCommand> hasher, ForestContext context, IBearerGenerator generator) : IHandler<SignInCommand, BearerTransport>
{
    public async Task<Result<BearerTransport>> HandlerAsync(SignInCommand request, CancellationToken token)
    {
        var user = await (
            from x in context.Users
            where x.Security.Email == request.EmailOrUserName || x.UserName == request.EmailOrUserName
            select new { x.UserName, x.Key, x.Security.Email, x.Security.Password }
        ).FirstOrDefaultAsync(token);

        if (user is null) return Result.Unauthorized();
        var bearerId = Guid.CreateVersion7();
        var refreshId = Guid.CreateVersion7();

        var bearer = generator.GenerateAsync([
            new(JwtRegisteredClaimNames.Jti, bearerId.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Key.ToString()!),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName)
        ], [
            new(JwtRegisteredClaimNames.Jti, refreshId.ToString())
        ], token);

        if (hasher.VerifyHashedPassword(request, user.Password, request.Password) == PasswordVerificationResult.Failed) return Result.Unauthorized();

        var data = await bearer;

        context.Tokens.Add(new(bearerId, refreshId, data.Exp, data.RefreshExp, user.Key));

        await context.SaveChangesAsync(token);

        return data;
    }
}

public sealed record SignInCommand(string EmailOrUserName, string Password);