using FluentValidation;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Common.Services.Validation;
using Microsoft.Extensions.Options;

namespace Forest.Minimal.Account.Services.Validator;

public sealed class SignUpValidator : AbstractValidator<SignUpCommand>
{
    public SignUpValidator(IOptionsMonitor<ValidationConfiguration> configuration)
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .EmailAddress();

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

        RuleFor(x => x.Banner).Custom((value, context) =>
        {
            if (value is null) return;
            if (!configuration.CurrentValue.ValidateFile(Path.GetExtension(value.FileName)) || value.Length > configuration.CurrentValue.ValidFileMBSize)
                context.AddFailure("Archivo no valido");
        });

        RuleFor(x => x.Photo).Custom((value, context) =>
        {
            if (value is null) return;
            if (!configuration.CurrentValue.ValidateFile(Path.GetExtension(value.FileName)) || value.Length > configuration.CurrentValue.ValidFileMBSize)
                context.AddFailure("Archivo no valido");
        });
    }
}