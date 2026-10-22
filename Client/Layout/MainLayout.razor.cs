using Microsoft.AspNetCore.Components;

namespace Client.Layout;

/// <summary>
/// Code-behind de <see cref="MainLayout"/>: cabecera, menú y cierre de sesión.
/// </summary>
public partial class MainLayout
{
    private bool menuOpen;

    private string SceneLabel => string.IsNullOrEmpty(Ocean.SceneId) ? "…" : Ocean.SceneId.Replace("_", " ");

    protected override void OnInitialized()
    {
        Auth.Changed += OnAuthChanged;
        Ocean.Changed += OnOceanChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await Ocean.InitializeAsync("ocean-canvas");
    }

    private void OnAuthChanged() => InvokeAsync(StateHasChanged);

    private void OnOceanChanged() => InvokeAsync(StateHasChanged);

    private void CloseMenu() => menuOpen = false;

    private async Task SignOutAsync()
    {
        CloseMenu();
        await Auth.SignOutAsync();
        Nav.NavigateTo("/");
    }

    public void Dispose()
    {
        Auth.Changed -= OnAuthChanged;
        Ocean.Changed -= OnOceanChanged;
    }
}
