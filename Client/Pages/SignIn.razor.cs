using Client.Models;
using Microsoft.AspNetCore.Components;

namespace Client.Pages;

/// <summary>
/// Code-behind de <see cref="SignIn"/>: formulario de acceso y bloqueo del
/// envío mientras la petición está en vuelo.
/// </summary>
public partial class SignIn
{
    private SignInRequest model = new();
    private string? error;
    private bool loading;
    private bool reveal;

    private string SceneLabel => string.IsNullOrEmpty(Ocean.SceneId) ? "…" : Ocean.SceneId.Replace("_", " ");

    protected override void OnInitialized()
    {
        Ocean.Changed += OnChanged;
    }

    /// <summary>
    /// Con sesión abierta no tiene sentido quedarse en el formulario de
    /// acceso: se va al perfil. La redirección se hace tras el primer render
    /// porque antes el router todavía está montando la página.
    /// </summary>
    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) return;

        if (Auth.IsAuthenticated) Nav.NavigateTo("/", replace: true);
    }

    private async Task Submit()
    {
        // Evita el spam: el botón se deshabilita mientras dura la petición y
        // aquí se descarta cualquier envío que llegue mientras tanto.
        if (loading) return;

        loading = true;
        error = null;
        var (ok, message) = await Auth.SignInAsync(model.EmailOrUserName, model.Password);
        loading = false;

        if (ok)
        {
            Nav.NavigateTo("/");
        }
        else
        {
            error = message;
        }
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Ocean.Changed -= OnChanged;
}
