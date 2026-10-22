using Microsoft.JSInterop;

namespace Client.Services;

/// <summary>
/// Thin bridge over <c>wwwroot/js/ocean.js</c>. The module owns the canvas,
/// the scene that is currently painted on it and the palette derived from it.
/// </summary>
public sealed class OceanService : IAsyncDisposable
{
    // Bump the query string whenever ocean.js changes so the browser does not
    // keep serving a stale copy of the module.
    private const string ModulePath = "./js/ocean.js?v=5";

    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public OceanService(IJSRuntime js) => _js = js;

    /// <summary>Identifier of the scene painted on the canvas (Ocean_1, Ocean_2…).</summary>
    public string SceneId { get; private set; } = string.Empty;

    /// <summary>Relative url of the full resolution picture of the current scene.</summary>
    public string SceneImage { get; private set; } = string.Empty;

    public event Action? Changed;

    public async Task InitializeAsync(string canvasId)
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await _module.InvokeVoidAsync("initOcean", canvasId);

        SceneId = await _module.InvokeAsync<string>("getSceneId");
        SceneImage = await _module.InvokeAsync<string>("getSceneImage");
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
