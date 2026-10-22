using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Client.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace Client.Services;

/// <summary>
/// Sesión del cliente sobre el flujo de cookies del API.
///
/// El servidor guarda el token en la cookie <c>auth_token</c> (HttpOnly) y la
/// borra en <c>auth/sign-out-web</c>. El navegador la envía sola en cada
/// petición, así que aquí no se almacena ningún token: ni en memoria, ni en
/// localStorage, ni en cabeceras <c>Authorization</c>. El estado de la sesión
/// se deduce de <c>GET users</c>, que responde con el perfil o con un 401.
/// </summary>
public class AuthService
{
    private const string SessionModule = "./js/session.js?v=1";
    private const string PreviewModule = "./js/preview.js?v=3";

    /// <summary>Tope de tamaño de la foto y el banner (el mismo del servidor).</summary>
    public const long MaxFileSize = 20_000_000;

    /// <summary>Extensiones que acepta el servidor para la foto y el banner.</summary>
    public static readonly string[] AllowedFileTypes = [".png", ".jpg", ".jpeg"];

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private IJSObjectReference? _session;
    private IJSObjectReference? _preview;
    private string? _photoPreview;
    private string? _bannerPreview;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public string? UserName { get; private set; }
    public string? Email { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// Identificador de la foto de perfil guardado en el servidor. Con él se
    /// puede pedir la imagen a <c>img/{id}</c>.
    /// </summary>
    public string? PhotoId { get; private set; }

    /// <summary>Identificador del banner de portada guardado en el servidor.</summary>
    public string? BannerId { get; private set; }

    /// <summary>
    /// Dirección de la foto de perfil. Es una ruta del propio origen, así que
    /// el gateway se encarga de llevarla al servidor sin credenciales.
    /// </summary>
    public string? PhotoUrl => string.IsNullOrWhiteSpace(PhotoId) ? null : $"/img/{PhotoId}";

    /// <summary>Dirección del banner de portada.</summary>
    public string? BannerUrl => string.IsNullOrWhiteSpace(BannerId) ? null : $"/img/{BannerId}";

    /// <summary>
    /// Vista previa local de la foto de perfil, para el instante entre que se
    /// elige el archivo y el servidor lo guarda.
    /// </summary>
    public string? PhotoPreviewUrl => _photoPreview;

    /// <summary>Vista previa local del banner de portada.</summary>
    public string? BannerPreviewUrl => _bannerPreview;

    public event Action? Changed;

    /// <summary>Recupera la sesión que ya tiene el navegador (cookie).</summary>
    public async Task InitializeAsync() => await RefreshAsync();

    /// <summary>
    /// Inicia sesión. El API no tiene <c>auth/sign-in-web</c>, así que el token
    /// que devuelve <c>auth/sign-in</c> se deja en la misma cookie que usa el
    /// servidor; a partir de ahí el navegador lo envía por su cuenta.
    /// </summary>
    public async Task<(bool Success, string? Error)> SignInAsync(string email, string password)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/sign-in")
            {
                Content = JsonContent.Create(new SignInRequest { EmailOrUserName = email, Password = password }),
            };
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return (false, await ReadProblemAsync(response));

            var token = await response.Content.ReadFromJsonAsync<AuthToken>();
            if (token is null || string.IsNullOrWhiteSpace(token.Token))
                return (false, "Respuesta inválida del servidor.");

            var module = await GetSessionModuleAsync();
            await module.InvokeVoidAsync("setAuthCookie", token.Token);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        return await RefreshAsync();
    }

    /// <summary>
    /// Crea la cuenta. <c>auth/sign-up-web</c> deja el token en la cookie
    /// <c>auth_token</c> y no devuelve nada, así que el cliente no ve el token.
    /// La foto y el banner son opcionales y viajan en el mismo multipart.
    /// </summary>
    public async Task<(bool Success, string? Error)> SignUpAsync(
        string email,
        string userName,
        string password,
        IBrowserFile? photo = null,
        IBrowserFile? banner = null)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(email), "Email");
            content.Add(new StringContent(userName), "UserName");
            content.Add(new StringContent(password), "Password");

            if (photo is not null)
                content.Add(await ToFileContentAsync(photo), "Photo", photo.Name);

            if (banner is not null)
                content.Add(await ToFileContentAsync(banner), "Banner", banner.Name);

            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/sign-up-web")
            {
                Content = content,
            };
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return (false, await ReadProblemAsync(response));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        return await RefreshAsync();
    }

    /// <summary>
    /// Actualiza la cuenta con <c>PUT users</c> (multipart). El servidor
    /// guarda el usuario, el nombre y los apellidos, sube la foto y el banner
    /// al bucket y devuelve el perfil con los identificadores de las imágenes.
    /// </summary>
    public async Task<(bool Success, string? Error)> UpdateProfileAsync(
        string? userName = null,
        string? firstName = null,
        string? lastName = null,
        IBrowserFile? photo = null,
        IBrowserFile? banner = null)
    {
        UserProfile? profile;

        try
        {
            using var content = new MultipartFormDataContent();

            if (!string.IsNullOrWhiteSpace(userName))
                content.Add(new StringContent(userName.Trim()), "UserName");

            if (!string.IsNullOrWhiteSpace(firstName))
                content.Add(new StringContent(firstName.Trim()), "FirstName");

            if (!string.IsNullOrWhiteSpace(lastName))
                content.Add(new StringContent(lastName.Trim()), "LastName");

            if (photo is not null)
                content.Add(await ToFileContentAsync(photo), "Photo", photo.Name);

            if (banner is not null)
                content.Add(await ToFileContentAsync(banner), "Banner", banner.Name);

            using var request = new HttpRequestMessage(HttpMethod.Put, "users")
            {
                Content = content,
            };
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return (false, await ReadProblemAsync(response));

            profile = await response.Content.ReadFromJsonAsync<UserProfile>();
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        // El nombre de usuario sigue siendo el del token hasta que se inicie
        // sesión de nuevo, pero las imágenes ya tienen su sitio.
        if (profile is not null)
        {
            FirstName = profile.FirstName;
            LastName = profile.LastName;
            PhotoId = profile.PhotoId;
            BannerId = profile.BannerId;
            NotifyChanged();
        }

        return (true, null);
    }

    /// <summary>Prepara un archivo del navegador para el multipart.</summary>
    private static async Task<StreamContent> ToFileContentAsync(IBrowserFile file)
    {
        var stream = file.OpenReadStream(MaxFileSize);
        var content = new StreamContent(stream);

        if (!string.IsNullOrWhiteSpace(file.ContentType))
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

        return await Task.FromResult(content);
    }

    /// <summary>
    /// Cierra sesión: <c>auth/sign-out-web</c> revoca el token y borra la
    /// cookie en el servidor.
    /// </summary>
    public async Task SignOutAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "auth/sign-out-web");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await _http.SendAsync(request);
        }
        catch
        {
            /* si el servidor no responde, la cookie se borra igual */
        }

        try
        {
            var module = await GetSessionModuleAsync();
            await module.InvokeVoidAsync("clearAuthCookie");
        }
        catch
        {
            /* sin JS no hay nada que limpiar */
        }

        SetSession(null);
    }

    /// <summary>
    /// Pregunta al API quién está conectado. Responde con el perfil o con un
    /// 401 cuando no hay cookie válida.
    /// </summary>
    public async Task<(bool Success, string? Error)> RefreshAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "users");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            using var response = await _http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                SetSession(null);
                return (false, null);
            }

            if (!response.IsSuccessStatusCode)
                return (false, await ReadProblemAsync(response));

            var profile = await response.Content.ReadFromJsonAsync<UserProfile>();
            if (profile is null)
            {
                SetSession(null);
                return (false, "Respuesta inválida del servidor.");
            }

            SetSession(profile);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private void SetSession(UserProfile? profile)
    {
        UserName = profile?.UserName;
        Email = profile?.Email;
        FirstName = profile?.FirstName;
        LastName = profile?.LastName;
        PhotoId = profile?.PhotoId;
        BannerId = profile?.BannerId;
        IsAuthenticated = !string.IsNullOrWhiteSpace(UserName);
        Changed?.Invoke();
    }

    private async Task<IJSObjectReference> GetSessionModuleAsync()
        => _session ??= await _js.InvokeAsync<IJSObjectReference>("import", SessionModule);

    /// <summary>
    /// Crea la vista previa del archivo elegido en el navegador. El servidor
    /// guarda la foto y el banner, pero el API no expone sus identificadores,
    /// así que el objeto local es la única forma de verlos mientras dura la
    /// sesión.
    /// </summary>
    public async Task SetPhotoPreviewAsync(IBrowserFile file)
    {
        var url = await CreatePreviewAsync("photo", file);
        if (url is null) return;

        // El valor se guarda antes de avisar: si se avisa primero, el render se
        // encola y se pinta antes de que el campo tenga la URL nueva.
        _photoPreview = url;
        NotifyChanged();
    }

    /// <inheritdoc cref="SetPhotoPreviewAsync(IBrowserFile)"/>
    public async Task SetBannerPreviewAsync(IBrowserFile file)
    {
        var url = await CreatePreviewAsync("banner", file);
        if (url is null) return;

        _bannerPreview = url;
        NotifyChanged();
    }

    /// <summary>
    /// Olvida las vistas previas locales: a partir de aquí se pinta lo que
    /// tiene guardado el servidor.
    /// </summary>
    public void ClearPreviews()
    {
        _photoPreview = null;
        _bannerPreview = null;
        NotifyChanged();
    }

    /// <summary>
    /// Avisa a la interfaz sin dejar que un suscriptor rompa la operación.
    /// </summary>
    private void NotifyChanged()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Marea] No se pudo refrescar la vista previa: {ex.Message}");
        }
    }

    private async Task<string?> CreatePreviewAsync(string inputId, IBrowserFile file)
    {
        try
        {
            var module = _preview ??= await _js.InvokeAsync<IJSObjectReference>("import", PreviewModule);
            return await module.InvokeAsync<string?>("objectUrl", inputId);
        }
        catch (Exception ex)
        {
            /* sin JS no hay vista previa, pero el archivo se sigue subiendo */
            Console.Error.WriteLine($"[Marea] Vista previa no disponible: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// The API answers with a RFC 7807 <c>Problem</c> payload
    /// ({ status, title, detail, fields, errors }). Turns it into a message
    /// that can be shown straight away in the UI.
    /// </summary>
    private static async Task<string?> ReadProblemAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return "No se pudo completar la operación.";

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            foreach (var name in new[] { "detail", "title" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }

            foreach (var container in new[] { "errors", "fields" })
            {
                if (!root.TryGetProperty(container, out var map) || map.ValueKind != JsonValueKind.Object) continue;

                var messages = new List<string>();
                foreach (var property in map.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        messages.AddRange(property.Value.EnumerateArray()
                            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null)
                            .Where(x => !string.IsNullOrWhiteSpace(x))!);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        var text = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(text)) messages.Add(text);
                    }
                }

                if (messages.Count > 0) return string.Join(" ", messages);
            }
        }
        catch
        {
            /* not a problem document — fall through */
        }

        return body.Length > 240 ? body[..240] : body;
    }
}
