using Forest.Minimal.Common.Handlers;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Account.Handlers;

public sealed class UpdateUserHandler(ForestContext context, IRustfsObjectStorageClient client) : IRequestHandler<UpdateUserQuery>
{
    public async Task<Result> HandlerAsync(UpdateUserQuery request, CancellationToken token)
    {
        string? bannerId = null, photoId = null;

        if (request.Request.Photo is not null)
            photoId = await client.UploadAsync(request.Request.Photo, token);

        if (request.Request.Banner is not null)
            bannerId = await client.UploadAsync(request.Request.Banner, token);

        await context.Users.ExecuteUpdateAsync((query) =>
        {
            if (!string.IsNullOrWhiteSpace(request.Request.UserName))
                query.SetProperty(x => x.UserName, request.Request.UserName);
            if (!string.IsNullOrWhiteSpace(request.Request.FirstName))
                query.SetProperty(x => x.Details!.FirstName, request.Request.FirstName);
            if (!string.IsNullOrWhiteSpace(request.Request.LastName))
                query.SetProperty(x => x.Details!.LastName, request.Request.LastName);
            if (!string.IsNullOrWhiteSpace(photoId))
                query.SetProperty(x => x.Details!.PhotoId, photoId);
            if (!string.IsNullOrWhiteSpace(bannerId))
                query.SetProperty(x => x.Details!.BannerId, bannerId);
        }, token);

        return Result.Success();
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