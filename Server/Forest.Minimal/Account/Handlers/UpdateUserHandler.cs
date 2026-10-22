using System.Threading.Channels;
using Forest.Minimal.Account.Mappers;
using Forest.Minimal.Account.Models;
using Forest.Minimal.Common.Handlers;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Account.Handlers;

public sealed class UpdateUserHandler(
    ForestContext context,
    IRustfsObjectStorageClient client,
    [FromKeyedServices(StorageHelper.CleanerChannel)] Channel<string> cleaner) : IHandler<UpdateUserQuery, Profile>
{
    public async Task<Result<Profile>> HandlerAsync(UpdateUserQuery request, CancellationToken token)
    {
        var user = context.Users.Where(x => x.Key == request.UserId).SingleOrDefault();
        if (user is null) return Result.NoContent();

        if (!string.IsNullOrWhiteSpace(request.Request.UserName))
            user.UserName = request.Request.UserName;
        if (!string.IsNullOrWhiteSpace(request.Request.FirstName))
            user.Details?.FirstName = request.Request.FirstName;
        if (!string.IsNullOrWhiteSpace(request.Request.LastName))
            user.Details?.LastName = request.Request.LastName;
        string? bannerId = null, photoId = null;

        if (request.Request.Photo is not null)
        {
            photoId = await client.UploadAsync(request.Request.Photo, token);
            (photoId, user.Details!.PhotoId) = (user.Details!.PhotoId, photoId);
        }

        if (request.Request.Banner is not null)
        {
            bannerId = await client.UploadAsync(request.Request.Banner, token);
            (bannerId, user.Details!.BannerId) = (user.Details!.BannerId, bannerId);
        }

        await context.SaveChangesAsync(token);

        _ = Queue(photoId);
        _ = Queue(bannerId);

        return Result.Success(UserMapper.ToProfile(user));
    }

    private Task Queue(string? key)
    {
        if (!string.IsNullOrEmpty(key)) cleaner.Writer.TryWrite(key);
        return Task.CompletedTask;
    }
}
public record UpdateUserQuery(EntityId UserId, UpdateUserRequest Request);
public class UpdateUserRequest
{
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public IFormFile? Photo { get; set; }
    public IFormFile? Banner { get; set; }
}