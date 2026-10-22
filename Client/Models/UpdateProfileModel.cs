namespace Client.Models;

/// <summary>
/// Campos que acepta <c>PUT users</c> (multipart/form-data) para actualizar la
/// cuenta: el nombre de usuario, el nombre, los apellidos y, opcionalmente,
/// la foto y el banner.
/// </summary>
public sealed class UpdateProfileModel
{
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
