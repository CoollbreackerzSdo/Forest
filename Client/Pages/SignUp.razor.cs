using Client.Models;
using Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Client.Pages;

/// <summary>
/// Code-behind de <see cref="SignUp"/>: alta de cuenta con foto y banner
/// opcionales.
/// </summary>
public partial class SignUp
{
    private SignUpCommand model = new();
    private string? error;
    private bool loading;
    private bool reveal;
    private IBrowserFile? photo;
    private IBrowserFile? banner;

    private string SceneLabel => string.IsNullOrEmpty(Ocean.SceneId) ? "…" : Ocean.SceneId.Replace("_", " ");

    private List<(string Text, bool Ok)> Rules
    {
        get
        {
            var value = model.Password ?? string.Empty;
            return new List<(string, bool)>
            {
                ("Mínimo 8 caracteres", value.Length >= 8),
                ("Dos letras mayúsculas", CountUpper(value) >= 2),
                ("Una letra minúscula", value.Any(char.IsLower)),
                ("Un número", value.Any(char.IsDigit)),
            };
        }
    }

    private static int CountUpper(string value)
    {
        var count = 0;
        foreach (var c in value)
        {
            if (char.IsUpper(c)) count++;
        }
        return count;
    }

    protected override void OnInitialized()
    {
        Ocean.Changed += OnChanged;
    }

    /// <summary>
    /// Con sesión abierta no tiene sentido quedarse en el formulario de alta:
    /// se va al perfil. La redirección se hace tras el primer render porque
    /// antes el router todavía está montando la página.
    /// </summary>
    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) return;

        if (Auth.IsAuthenticated) Nav.NavigateTo("/", replace: true);
    }

    private void PickPhoto(InputFileChangeEventArgs args) => photo = Accept(args.File, "La foto");

    private void PickBanner(InputFileChangeEventArgs args) => banner = Accept(args.File, "El banner");

    /// <summary>Comprueba el archivo antes de subirlo (mismas reglas que el servidor).</summary>
    private IBrowserFile? Accept(IBrowserFile file, string what)
    {
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();

        if (!AuthService.AllowedFileTypes.Contains(extension))
        {
            error = $"{what} debe estar en PNG o JPG.";
            return null;
        }

        if (file.Size > AuthService.MaxFileSize)
        {
            error = $"{what} no puede pasar de 20 MB.";
            return null;
        }

        error = null;
        return file;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1000 => $"{bytes} B",
        < 1_000_000 => $"{bytes / 1000.0:0.#} KB",
        _ => $"{bytes / 1_000_000.0:0.#} MB",
    };

    private async Task Submit()
    {
        // Evita el spam: el formulario se deshabilita mientras dura la
        // petición y aquí se descarta cualquier envío que llegue mientras tanto.
        if (loading) return;

        loading = true;
        error = null;
        var (ok, message) = await Auth.SignUpAsync(model.Email, model.UserName, model.Password, photo, banner);
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
