using CoreFinance.API.Extensions;
using CoreFinance.API.Middlewares;
using CoreFinance.API.Observability;
using CoreFinance.API.Services;
using CoreFinance.Application;
using CoreFinance.Domain.Interfaces;
using CoreFinance.Infra;

var builder = WebApplication.CreateBuilder(args);

var demoHabilitado = builder.Configuration.GetValue<bool>("Observability:Demo:Enabled");

builder.Services.AddControllers(options =>
    options.Conventions.Add(new DemoControllerConvention(demoHabilitado)));
builder.Services.AddSwaggerConfig();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfra(builder.Configuration);
builder.Services.AddHealthChecksConfig(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSwaggerConfig();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecksConfig();
app.MapControllers();

app.Run();
