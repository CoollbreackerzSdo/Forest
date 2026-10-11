using Forest.Minimal.Account.Mappers;
using Forest.Minimal.Account.Services.Bearer.Generator;
using Forest.Minimal.Account.Services.Bearer.Models;
using Forest.Minimal.Common.Handlers;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Account.Handlers;

public sealed class SignUpHandler(IPasswordHasher<SignUpCommand> hasher, ForestContext context, IBearerGenerator generator, IRustfsObjectStorageClient storage) : IHandler<SignUpCommand, BearerTransport>
{
    private const string Bucket = "forest";

    public async Task<Result<BearerTransport>> HandlerAsync(SignUpCommand request, CancellationToken token)
    {
        var user = UserMapper.ToUserModel(request with { Password = hasher.HashPassword(request, request.Password) });

        user.Details ??= new();
        user.Details.PhotoId = request.Photo is null ? null : await UploadAsync(request.Photo, token);
        user.Details.BannerId = request.Banner is null ? null : await UploadAsync(request.Banner, token);

        var bearerId = Guid.CreateVersion7();
        var refreshId = Guid.CreateVersion7();

        var bearer = generator.GenerateAsync([
            new(JwtRegisteredClaimNames.Jti,bearerId.ToString()),
            new(JwtRegisteredClaimNames.Sub,user.Key.ToString()!),
            new(JwtRegisteredClaimNames.Email,user.Security.Email),
            new(JwtRegisteredClaimNames.UniqueName,user.UserName)
        ], [
            new (JwtRegisteredClaimNames.Jti,refreshId.ToString())
        ], token);

        context.Users.Add(user);

        var data = await bearer;

        context.Tokens.Add(new(bearerId, refreshId, data.Exp, data.RefreshExp, user.Key));

        await context.SaveChangesAsync(token);

        return data;
    }

    private async Task<string> UploadAsync(IFormFile file, CancellationToken token)
    {
        await storage.EnsureBucketAsync(Bucket, token);

        await using var stream = new MemoryStream();
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(stream, token);
        }
        stream.Position = 0;

        var key = $"{Guid.CreateVersion7()}{Path.GetExtension(file.FileName)}";
        await storage.UploadAsync(Bucket, key, stream, file.ContentType, token);
        return key;
    }
}

public sealed record SignUpCommand(string UserName, string Email, string Password, IFormFile? Photo = null, IFormFile? Banner = null, string? FirstName = null, string? LastName = null);