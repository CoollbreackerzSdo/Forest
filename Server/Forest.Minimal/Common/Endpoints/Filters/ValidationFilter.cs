using FluentValidation;

namespace Forest.Minimal.Common.Endpoints.Filters;

public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var value = context.GetArgument<T>(0);
        if (value is null)
            return new Problem(StatusCodes.Status400BadRequest, "Cuerpo de la solicitud sin definir", "El cuerpo de la solicitud no se pudo enlazar con los datos esperados.");

        var result = validator.Validate(value);
        return result.IsValid
            ? await next(context)
            : new Problem(StatusCodes.Status400BadRequest, "Solicitud inválida", "Uno o más campos no pasaron la validación.", Errors: result.ToDictionary());
    }
}