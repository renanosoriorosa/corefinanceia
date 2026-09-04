using System.Reflection;
using Microsoft.OpenApi;

namespace CoreFinance.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerConfig(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "CoreFinance API",
                Version = "v1",
                Description = "API para controle financeiro pessoal"
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe apenas o token JWT retornado no login."
            });

            options.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", documento),
                    new List<string>()
                }
            });

            options.TagActionsBy(api => [api.GroupName ?? api.ActionDescriptor.RouteValues["controller"]!]);
            options.DocInclusionPredicate((_, _) => true);
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerConfig(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "CoreFinance API v1");
            options.RoutePrefix = string.Empty;
            options.DisplayRequestDuration();
            options.DefaultModelsExpandDepth(-1);
        });

        return app;
    }
}
