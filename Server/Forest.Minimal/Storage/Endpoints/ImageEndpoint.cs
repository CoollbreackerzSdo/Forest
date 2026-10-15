using Forest.Communication.Models.Storage;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Common.Endpoints.Abstractions;
using Forest.Minimal.Common.Endpoints.Filters;
using Forest.Minimal.Common.Handlers;
using Forest.Minimal.Storage.Handlers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Forest.Minimal.Account.Endpoints;

public sealed class ImageEndpoint : IEndpoint
{
    public IEndpointRouteBuilder Map(IEndpointRouteBuilder builder)
    {
        var endp = builder.MapGroup("img").WithTags("Images");

        endp.MapGet("{imageId}", Download)
            .Produces<FileStreamHttpResult>()
            .Produces<Problem>(StatusCodes.Status404NotFound, Problem.ContentType);

        endp.MapPut("", UpdateImage)
            .Accepts<UploadImgRequest>("multipart/form-data")
            .AddEndpointFilter<ValidationFilter<UploadImgRequest>>()
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(AuthHelper.DefaultPolicy);

        return endp;
    }

    private async Task<Results<Ok, Problem>> UpdateImage([FromForm] UploadImgRequest request, IHandler<UploadImgRequest, string> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(request, token);
        return result.IsSuccess
            ? TypedResults.Ok()
            : new Problem(StatusCodes.Status400BadRequest, "Imagen anterior no encontrada", "No existe una imagen con ese identificador.");
    }

    private async Task<Results<FileStreamHttpResult, Problem>> Download(string imageId, IHandler<ImageQuery, ImageResult> handler, CancellationToken token)
    {
        var result = await handler.HandlerAsync(new(imageId), token);
        return result.IsSuccess
            ? TypedResults.File(result.Value.Content, result.Value.ContentType)
            : new Problem(StatusCodes.Status404NotFound, "Imagen no encontrada", "No existe una imagen con ese identificador.");
    }
}