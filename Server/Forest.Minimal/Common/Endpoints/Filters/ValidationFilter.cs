using FluentValidation;

namespace Forest.Minimal.Common.Endpoints.Filters;

public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var value = context.GetArgument<T>(0);
        if (value is null) return TypedResults.BadRequest();
        var result = validator.Validate(value);
        return result.IsValid ? await next(context) : TypedResults.ValidationProblem(result.ToDictionary());
    }
}