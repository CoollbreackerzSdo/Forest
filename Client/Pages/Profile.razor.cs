using Client.Models;
using Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Client.Pages;

/// <summary>
/// Code-behind de <see cref="Profile"/>: perfil, editor de datos y subida de
/// foto y banner.
/// </summary>
public partial class Profile
{
    private static readonly string[] Tabs = ["Publicaciones", "Guardados", "Acerca de"];

    private UpdateProfileModel model = new();
    private string tab = "Publicaciones";
    private bool editing;
    private bool saving;
    private bool saved;
    private string? error;
    private IBrowserFile? photo;
    private IBrowserFile? banner;

    private string Initial => string.IsNullOrWhiteSpace(Auth.UserName) ? "?" : Auth.UserName![0].ToString().ToUpperInvariant();

    /// <summary>
    /// La vista previa local manda mientras se elige el archivo (es lo último
    /// que se ha visto); cuando no la hay, se usa la foto que tiene el
    /// servidor.
    /// </summary>
    private string? AvatarUrl => Auth.PhotoPreviewUrl ?? Auth.PhotoUrl;

    /// <summary>El banner, con la misma prioridad que la foto.</summary>
    private string? CoverUrl => Auth.BannerPreviewUrl ?? Auth.BannerUrl;

    private string SceneLabel => string.IsNullOrEmpty(Ocean.SceneId) ? "…" : Ocean.SceneId.Replace("_", " ");

    protected override void OnInitialized()
    {
        Auth.Changed += OnChanged;
        Ocean.Changed += OnChanged;
        model.UserName = Auth.UserName;
    }

    private void ToggleEditor()
    {
        editing = !editing;
        saved = false;
        error = null;

        if (editing)
            model.UserName = Auth.UserName;
    }

    private void PickPhoto(InputFileChangeEventArgs args)
    {
        photo = Accept(args.File, "La foto");
        if (photo is not null)
            _ = Auth.SetPhotoPreviewAsync(photo);
    }

    private void PickBanner(InputFileChangeEventArgs args)
    {
        banner = Accept(args.File, "El banner");
        if (banner is not null)
            _ = Auth.SetBannerPreviewAsync(banner);
    }

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

    private async Task Save()
    {
        // Evita el spam: el formulario se deshabilita mientras dura la
        // petición y aquí se descarta cualquier envío que llegue mientras tanto.
        if (saving) return;

        saving = true;
        saved = false;
        error = null;

        var (ok, message) = await Auth.UpdateProfileAsync(model.UserName, model.FirstName, model.LastName, photo, banner);

        saving = false;

        if (ok)
        {
            saved = true;
            photo = null;
            banner = null;

            // Ya no hace falta la vista previa local: se pinta lo que hay
            // guardado en el servidor.
            Auth.ClearPreviews();
        }
        else
        {
            error = message ?? "No se pudo guardar el perfil.";
        }
    }

    private async Task Logout()
    {
        await Auth.SignOutAsync();
        Nav.NavigateTo("/");
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Auth.Changed -= OnChanged;
        Ocean.Changed -= OnChanged;
    }
}
