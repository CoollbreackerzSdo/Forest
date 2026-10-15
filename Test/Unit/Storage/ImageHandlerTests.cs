using Forest.Communication.Models.Storage;
using Forest.Minimal.Storage.Handlers;
using Forest.Test.Support;

namespace Forest.Test.Unit.Storage;

public sealed class ImageHandlerTests : IDisposable
{
    private readonly FakeRustfsObjectStorageClient _storage = new();
    private readonly ImageHandler _handler;
    private const string Key = "0199c0de-0000-7000-8000-000000000001.png";

    public ImageHandlerTests()
    {
        _handler = new ImageHandler(_storage);
        _storage.UploadAsync(StorageHelper.BucketName, Key, new MemoryStream([1, 2, 3, 4]), "image/png").GetAwaiter().GetResult();
    }

    private Task UploadAsync(string key) => _storage.UploadAsync(StorageHelper.BucketName, key, new MemoryStream([1]));

    [Fact]
    public async Task HandlerAsync_DeberiaDescargarElContenidoDeLaImagen()
    {
        var result = await _handler.HandlerAsync(new ImageQuery(Key), CancellationToken.None);

        Assert.True(result.IsSuccess);
        using var buffer = new MemoryStream();
        result.Value.Content.Position = 0;
        await result.Value.Content.CopyToAsync(buffer);
        Assert.Equal([1, 2, 3, 4], buffer.ToArray());
    }

    [Theory]
    [InlineData("imagen.png", "image/png")]
    [InlineData("imagen.jpg", "image/jpeg")]
    [InlineData("imagen.jpeg", "image/jpeg")]
    public async Task HandlerAsync_DeberiaResolverElContentTypePorExtension(string fileName, string expected)
    {
        var key = $"0199c0de-0000-7000-8000-00000000000c-{fileName}";
        await UploadAsync(key);

        var result = await _handler.HandlerAsync(new ImageQuery(key), CancellationToken.None);

        Assert.Equal(expected, result.Value.ContentType);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaUsarOctetStreamParaExtensionesDesconocidas()
    {
        const string key = "0199c0de-0000-7000-8000-0000000000ff.xyz";
        await UploadAsync(key);

        var result = await _handler.HandlerAsync(new ImageQuery(key), CancellationToken.None);

        Assert.Equal("application/octet-stream", result.Value.ContentType);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaDevolverNotFoundSiLaImagenNoExiste()
    {
        var result = await _handler.HandlerAsync(new ImageQuery("no-existe.png"), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task HandlerAsync_DeberiaDevolverNotFoundTrasBorrarLaImagen()
    {
        await _storage.DeleteAsync(StorageHelper.BucketName, Key);

        var result = await _handler.HandlerAsync(new ImageQuery(Key), CancellationToken.None);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    /// <summary>
    /// Defecto conocido: <c>ImageHandler</c> devuelve el <see cref="MemoryStream"/> con la posicion
    /// al final (tras la descarga). <c>TypedResults.File</c> copia desde la posicion actual,
    /// por lo que <c>GET /img/{id}</c> responde con 0 bytes.
    /// </summary>
    [Fact]
    public async Task HandlerAsync_DevuelveElStreamEnSuPosicionFinal()
    {
        var result = await _handler.HandlerAsync(new ImageQuery(Key), CancellationToken.None);

        Assert.Equal(4, result.Value.Content.Length);
        Assert.Equal(4, result.Value.Content.Position);
    }

    [Fact]
    public void StorageHelper_DeberiaUsarElBucketForest()
    {
        Assert.Equal("forest", StorageHelper.BucketName);
    }

    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }
}