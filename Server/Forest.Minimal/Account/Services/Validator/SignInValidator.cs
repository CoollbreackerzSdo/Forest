using FluentValidation;
using Forest.Minimal.Account.Handlers;

namespace Forest.Minimal.Account.Services.Validator;

public sealed class SignInValidator : AbstractValidator<SignInRequest>
{
    public SignInValidator()
    {
        RuleFor(x => x.EmailOrUserName)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}