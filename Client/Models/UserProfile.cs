namespace Client.Models;

/// <summary>
/// Perfil devuelto por <c>GET users</c>. Es la única forma de saber si hay
/// sesión: si la cookie no es válida el API responde con un 401.
/// <para>
/// Los identificadores de la foto y del banner son los que usa el servidor
/// para guardarlas, así que el cliente puede pintarlas sin adivinar nada.
/// </para>
/// </summary>
public sealed class UserProfile
{
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhotoId { get; set; }
    public string? BannerId { get; set; }
}
