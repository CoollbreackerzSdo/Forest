using Microsoft.AspNetCore.Components;

namespace Client.Pages;

/// <summary>
/// Code-behind de <see cref="Home"/>: la portada y las capacidades.
/// </summary>
public partial class Home
{
    private string SceneLabel => string.IsNullOrEmpty(Ocean.SceneId) ? "…" : Ocean.SceneId.Replace("_", " ");

    protected override void OnInitialized()
    {
        Auth.Changed += OnChanged;
        Ocean.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Auth.Changed -= OnChanged;
        Ocean.Changed -= OnChanged;
    }
}
