using Microsoft.OpenApi;

namespace HAMBOX.API.Extensions;

/// <summary>
/// Swagger / OpenAPI UI registration for the API host.
/// </summary>
internal static class SwaggerExtensions
{
    /// <summary>
    /// Registers Swagger generation for interactive API documentation.
    /// </summary>
    public static IServiceCollection AddHamboxSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "HAMBOX API",
                Version = "v1",
                Description = "HAMBOX modular monolith API — Identity and Catalog modules."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        return services;
    }

    /// <summary>
    /// Enables the Swagger JSON endpoint and Swagger UI (development only).
    /// </summary>
    public static WebApplication UseHamboxSwagger(this WebApplication app)
    {
        app.UseSwagger(options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        });

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "HAMBOX API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "HAMBOX API";
        });

        return app;
    }
}
