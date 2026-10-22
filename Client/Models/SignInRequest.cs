namespace Client.Models;

/// <summary>Credenciales de <c>auth/sign-in</c>.</summary>
public sealed class SignInRequest
{
    /// <summary>
    /// El API lo llama así, pero su validador exige un correo: por eso el
    /// formulario solo pide el email.
    /// </summary>
    public string EmailOrUserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Datos de <c>auth/sign-up-web</c>. El cliente solo manda lo que usa: el
/// servidor ya se encarga del resto del perfil.
/// </summary>
public sealed class SignUpCommand
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
