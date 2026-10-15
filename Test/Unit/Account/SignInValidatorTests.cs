using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Validator;
using Forest.Test.Support;

namespace Forest.Test.Unit.Account;

public sealed class SignInValidatorTests
{
    private readonly SignInValidator _validator = new();

    [Fact]
    public void Validate_DeberiaAceptarCredencialesValidas()
    {
        var result = _validator.Validate(new SignInRequest("usuario@forest.dev", TestData.ValidPassword));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-email")]
    public void Validate_DeberiaRechazarIdentificadorInvalido(string value)
    {
        var result = _validator.Validate(new SignInRequest(value, TestData.ValidPassword));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignInRequest.EmailOrUserName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_DeberiaRechazarContrasenaVacia(string password)
    {
        var result = _validator.Validate(new SignInRequest("usuario@forest.dev", password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(SignInRequest.Password));
    }

    [Fact]
    public void Validate_DeberiaExigirQueElCorreoSeaValidoAunSiElHandlerAdmiteUserName()
    {
        // El handler busca por correo o por nombre de usuario, pero el validator exige formato de correo.
        var result = _validator.Validate(new SignInRequest("usuario", TestData.ValidPassword));

        Assert.False(result.IsValid);
    }
}