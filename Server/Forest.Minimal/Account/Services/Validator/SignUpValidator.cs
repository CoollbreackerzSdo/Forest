using FluentValidation;
using Forest.Minimal.Account.Handlers;

namespace Forest.Minimal.Account.Services.Validator;

public sealed class SignUpValidator : AbstractValidator<SignUpCommand>
{
    public SignUpValidator(IConfiguration configuration)
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Password)
            .Matches(@"^(?=(?:\S*?[A-Z]){2})(?=\S*?[a-z])(?=\S*?[0-9]).{8,}\S$");

        RuleFor(x => x.FirstName).Custom((value, context) =>
        {
            if (value is { Length: > 100 })
                context.AddFailure("Primer nombre demasiado largo");
        });

        RuleFor(x => x.LastName).Custom((value, context) =>
        {
            if (value is { Length: > 100 })
                context.AddFailure("Segundo nombre demasiado largo");
        });

        var validImgFormat = configuration.GetValue<string[]>("ValidationConfiguration:FileTypes");
        var maxFileSize = configuration.GetValue<long>("ValidationConfiguration:");

        RuleFor(x => x.Banner).Custom((value, context) =>
        {
            if (value is null) return;
            if (!ValidateFileExtension(Path.GetExtension(value.FileName)) || value.Length > maxFileSize)
                context.AddFailure("Archivo no valido");
        });

        RuleFor(x => x.Photo).Custom((value, context) =>
        {
            if (value is null) return;
            if (!ValidateFileExtension(Path.GetExtension(value.FileName)) || value.Length > maxFileSize)
                context.AddFailure("Archivo no valido");
        });

        bool ValidateFileExtension(string extension) => validImgFormat.Contains(extension);
    }
}