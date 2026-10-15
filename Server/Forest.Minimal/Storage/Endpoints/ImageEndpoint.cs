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
            .Produces<FileStreamHttpResult>()
            .Produces<Problem>(StatusCodes.Status404NotFound, Problem.ContentType);

        return endp;
    }

    private async Task<Results<FileStreamHttpResult, Problem>> Download(string imageId, IHandler<ImageQuery, ImageResult> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(new(imageId), token);
        return result.IsSuccess
            ? TypedResults.File(result.Value.Content, result.Value.ContentType)
            : new Problem(StatusCodes.Status404NotFound, "Imagen no encontrada", "No existe una imagen con ese identificador.");
    }
}