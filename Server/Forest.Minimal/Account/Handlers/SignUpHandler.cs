using Forest.Minimal.Account.Mappers;
using Forest.Minimal.Account.Models;
using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Common.Handlers;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Account.Handlers;

public sealed class SignUpHandler(IPasswordHasher<UserModel> hasher, ForestContext context, IBearerGenerator generator, IRustfsObjectStorageClient storage) : IHandler<SignUpCommand, BearerTransport>
{
    public async Task<Result<BearerTransport>> HandlerAsync(SignUpCommand request, CancellationToken token)
    {
        var user = UserMapper.ToUserModel(request with { Password = hasher.HashPassword(default!, request.Password) });

        var bearerId = Guid.CreateVersion7();
        var refreshId = Guid.CreateVersion7();

        var bearer = generator.GenerateAsync(new Dictionary<string, object>()
        {
            {JwtRegisteredClaimNames.Jti , bearerId.ToString() },
            {JwtRegisteredClaimNames.Sub, user.Key.ToString()! },
            {JwtRegisteredClaimNames.Email, user.Security.Email},
            {JwtRegisteredClaimNames.UniqueName, user.UserName}
        }, new Dictionary<string, object>()
        {
            {JwtRegisteredClaimNames.Jti, refreshId.ToString()}
        }, token);

        context.Users.Add(user);

        var data = await bearer;

        context.Tokens.Add(new(bearerId, refreshId, data.Exp, data.RefreshExp, user.Key));

        user.Details?.BannerId = request.Banner is null ? null : await storage.UploadAsync(request.Banner, token);
        user.Details?.PhotoId = request.Photo is null ? null : await storage.UploadAsync(request.Photo, token);

        await context.SaveChangesAsync(token);

        return data;
    }
}

public sealed record SignUpCommand(string Email, string UserName, string Password)
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public IFormFile? Photo { get; set; }
    public IFormFile? Banner { get; set; }
}