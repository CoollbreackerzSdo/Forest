namespace Client.Models;

/// <summary>
/// Respuesta de <c>auth/sign-in</c>. El cliente solo necesita el token para
/// dejarlo en la cookie; el resto (refresh, caducidad) lo lleva el servidor.
/// </summary>
public sealed class AuthToken
{
    public string Token { get; set; } = string.Empty;
}
