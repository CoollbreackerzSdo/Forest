using Forest.Minimal.Account.Models;
using Forest.Minimal.Account.Services.Bearer;
using Forest.Minimal.Common.Services.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Forest.Test.Support;

/// <summary>
/// Datos compartidos por los tests unitarios del servidor.
/// </summary>
public static class TestData
{
    /// <summary>Clave simetrica valida para HS256 (>= 256 bits).</summary>
    public const string BearerKey = "forest-test-bearer-key-0123456789";
    public const string Audience = "com.coollbreackerz";
    public const string Issuer = "Ralber";
    public const int ExpirationDays = 10;
    public const int RefreshExpirationDays = 2;

    public static ValidationConfiguration Validation { get; } = new()
    {
        PasswordLength = 8,
        ValidFileMBSize = 1,
        FileTypes = [".png", ".jpg", ".jpeg"]
    };

    public static BearerConfiguration BearerConfiguration { get; } = new()
    {
        HashKey = BearerKey,
        Audience = Audience,
        Issuer = Issuer,
        Exp = ExpirationDays,
        RefreshExp = RefreshExpirationDays
    };

    /// <summary>Contraseña que cumple el regex de <c>SignUpValidator</c>: 2 mayusculas, 1 minuscula, 1 digito, 8+ caracteres.</summary>
    public const string ValidPassword = "ForestCAFE123";

    public static IPasswordHasher<UserModel> PasswordHasher { get; } = new PasswordHasher<UserModel>();

    public static IFormFile File(string name = "photo.png", string contentType = "image/png", int length = 1024)
    {
        var content = new byte[length];
        Random.Shared.NextBytes(content);
        return new FormFile(new MemoryStream(content), 0, length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    public static void SeedUser(ForestContext context, string userName, string email, string password = ValidPassword)
    {
        context.Users.Add(new UserModel
        {
            UserName = userName,
            Details = new Details { FirstName = "Test", LastName = "Usuario" },
            Security = new Security(email, PasswordHasher.HashPassword(default!, password))
        });
        context.SaveChanges();
    }
}