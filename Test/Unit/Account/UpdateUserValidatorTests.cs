using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Account.Services.Validator;
using Forest.Minimal.Common.Services.Validation;
using Forest.Test.Support;

namespace Forest.Test.Unit.Account;

public sealed class UpdateUserValidatorTests
{
    private readonly UpdateUserValidator _validator = new(new StaticOptionsMonitor<ValidationConfiguration>(TestData.Validation));

    [Fact]
    public void Validate_DeberiaAceptarUnaPeticiónVacía()
    {
        var result = _validator.Validate(new UpdateUserRequest());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_DeberiaRechazarUserNameDemasiadoLargo()
    {
        var result = _validator.Validate(new UpdateUserRequest { UserName = new string('a', 51) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(UpdateUserRequest.UserName));
    }

    [Fact]
    public void Validate_DeberiaRechazarNombresDemasiadoLargos()
    {
        var result = _validator.Validate(new UpdateUserRequest
        {
            FirstName = new string('a', 101),
            LastName = new string('b', 101)
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(UpdateUserRequest.FirstName));
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(UpdateUserRequest.LastName));
    }

    [Fact]
    public void Validate_DeberiaRechazarArchivosDeTipoNoPermitido()
    {
        var result = _validator.Validate(new UpdateUserRequest { Banner = TestData.File("documento.pdf", "application/pdf") });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(UpdateUserRequest.Banner));
    }

    [Fact]
    public void Validate_DeberiaRechazarArchivosQueSuperanElTamañoMáximo()
    {
        var result = _validator.Validate(new UpdateUserRequest { Photo = TestData.File("foto.png", length: (int)TestData.Validation.ValidImgMBSize + 1) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(UpdateUserRequest.Photo));
    }

    [Fact]
    public void Validate_DeberiaAceptarArchivosValidos()
    {
        var result = _validator.Validate(new UpdateUserRequest
        {
            UserName = "usuario",
            FirstName = "Forest",
            LastName = "Test",
            Photo = TestData.File("foto.jpg", "image/jpeg"),
            Banner = TestData.File("portada.png")
        });

        Assert.True(result.IsValid);
    }
}