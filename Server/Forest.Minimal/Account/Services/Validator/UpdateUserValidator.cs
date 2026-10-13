using FluentValidation;
using Forest.Minimal.Account.Handlers;
using Forest.Minimal.Common.Services.Validation;
using Microsoft.Extensions.Options;

namespace Forest.Minimal.Account.Services.Validator;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator(IOptionsMonitor<ValidationConfiguration> configuration)
    {
        RuleFor(x => x.UserName).Custom((value, context) =>
        {
            if (value is { Length: > 50 })
                context.AddFailure("Nombre de usuario nombre demasiado largo");
        });

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