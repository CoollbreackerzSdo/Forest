using Forest.Minimal.Account.Helpers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Forest.Minimal.Common.OpenApi;

public sealed class BearerTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Security ??= [];
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        var requirement = new OpenApiSecurityRequirement
        {
            {
                new(AuthHelper.AuthenticationScheme,document)
                {
                    Description = "Encabezado de autorización de JWT utilizando el esquema de portador.",
                    Reference = new()
                    {
                        Type = ReferenceType.SecurityScheme
                    }
                },
                []
            }
        };
        document.Security.Add(requirement);
        document.Components.SecuritySchemes[AuthHelper.AuthenticationScheme] = new OpenApiSecurityScheme
        {
            Name = "Jwt Authentication",
            In = ParameterLocation.Header,
            Description = "Encabezado de autorización de JWT utilizando el esquema de portador.",
            Scheme = AuthHelper.AuthenticationScheme,
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT"
        };
        return Task.CompletedTask;
    }
}