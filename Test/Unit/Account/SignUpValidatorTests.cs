using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Validator;
using Forest.Minimal.Common.Services.Validation;
using Forest.Test.Support;

namespace Forest.Test.Unit.Account;

public sealed class SignUpValidatorTests
{
    private readonly SignUpValidator _validator = new(new StaticOptionsMonitor<ValidationConfiguration>(TestData.Validation));

    private static SignUpCommand Command(string email = "usuario@forest.dev", string userName = "usuario", string password = TestData.ValidPassword)
        => new(email, userName, password);

    [Fact]
    public void Validate_DeberiaAceptarUnRegistroValido()
    {
        var result = _validator.Validate(Command());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-email")]
    public void Validate_DeberiaRechazarCorreosInvalidos(string email)
    {
        var result = _validator.Validate(Command(email));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignUpCommand.Email));
    }

    [Fact]
    public void Validate_DeberiaRechazarUserNameVacio()
    {
        var result = _validator.Validate(Command(userName: ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignUpCommand.UserName));
    }

    [Fact]
    public void Validate_DeberiaRechazarUserNameDemasiadoLargo()
    {
        var result = _validator.Validate(Command(userName: new string('a', 51)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignUpCommand.UserName));
    }

    [Theory]
    [InlineData("corta1A", "sin minuscula ni dos mayusculas")]
    [InlineData("Forest1", "sin mayuscula suficiente")]
    [InlineData("forest1234", "sin mayusculas")]
    [InlineData("FOREST1234", "sin minusculas")]
    public void Validate_DeberiaRechazarContrasenasQueNoCumplenElFormato(string password, string _)
    {
        var result = _validator.Validate(Command(password: password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignUpCommand.Password));
    }

    [Fact]
    public void Validate_DeberiaAceptarArchivosDeTipoPermitido()
    {
        var command = Command() with { Photo = TestData.File("foto.png"), Banner = TestData.File("portada.jpeg", "image/jpeg") };

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DeberiaRechazarArchivosDeTipoNoPermitido()
    {
        var command = Command() with { Photo = TestData.File("malicioso.exe", "application/octet-stream") };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignUpCommand.Photo));
    }

    [Fact]
    public void Validate_DeberiaRechazarArchivosQueSuperanElTamanoMaximo()
    {
        var command = Command() with { Banner = TestData.File("portada.png", length: (int)TestData.Validation.ValidImgFileMBSize + 1) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignUpCommand.Banner));
    }

    [Fact]
    public void Validate_DeberiaRechazarNombresDemasiadoLargos()
    {
        var command = Command() with { FirstName = new string('a', 101), LastName = new string('b', 101) };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignUpCommand.FirstName));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignUpCommand.LastName));
    }

    [Fact]
    public void Validate_DeberiaAceptarNombresOpcionalesAusentes()
    {
        var result = _validator.Validate(Command());

        Assert.True(result.IsValid);
    }
}