using FluentValidation;
using Forest.Minimal.Common.Services.Validation;
using Forest.Minimal.Storage.Handlers;
using Microsoft.Extensions.Options;

namespace Forest.Minimal.Storage.Services.Validation;

public sealed class UpdateImgRequestValidation : AbstractValidator<UploadImgRequest>
{
    public UpdateImgRequestValidation(IOptionsMonitor<ValidationConfiguration> configuration)
    {
        RuleFor(x => x.ImgId).NotEmpty();

        var maxFileSize = configuration.CurrentValue.ValidFileMBSize;

        RuleFor(x => x.NewImg).Custom((value, context) =>
        {
            if (value is null) return;
            if (!configuration.CurrentValue.ValidateFile(value) || value.Length > maxFileSize)
                context.AddFailure("Archivo no valido");
        });
    }
}