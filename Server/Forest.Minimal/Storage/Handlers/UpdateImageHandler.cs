using Forest.Minimal.Common.Handlers;
using RustfsClient.Abstractions;

namespace Forest.Minimal.Storage.Handlers;

public sealed class UpdateImageHandler(IRustfsObjectStorageClient client) : IHandler<UploadImgRequest, string>
{
    public async Task<Result<string>> HandlerAsync(UploadImgRequest request, CancellationToken token) => Result.Success();
}

public sealed record UploadImgRequest(string ImgId, IFormFile NewImg);