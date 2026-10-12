using Forest.Communication.Models.Storage;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Handlers;
using Forest.Minimal.Storage.Handlers;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Forest.Minimal.Account.Endpoints;

public sealed class ImageEndpoint : IEndpoint
{
    public IEndpointRouteBuilder Map(IEndpointRouteBuilder builder)
    {
        var endp = builder.MapGroup("img").WithTags("Images");

        endp.MapGet("{imageId}", Download)
            .Produces<FileStreamHttpResult>();

        return endp;
    }

    private async Task<Results<FileStreamHttpResult, NotFound>> Download(string imageId, IHandler<ImageQuery, ImageResult> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(new(imageId), token);
        return result.IsSuccess ? TypedResults.File(result.Value.Content, result.Value.ContentType) : TypedResults.NotFound();
    }
}