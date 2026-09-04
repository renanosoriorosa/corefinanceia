using CoreFinance.Application.Auth.Interfaces;
using CoreFinance.Application.Auth.Services;
using CoreFinance.Application.Dashboard.Interfaces;
using CoreFinance.Application.Dashboard.Services;
using CoreFinance.Application.FixedAccounts.Interfaces;
using CoreFinance.Application.FixedAccounts.Services;
using CoreFinance.Application.Painel.Interfaces;
using CoreFinance.Application.Painel.Services;
using CoreFinance.Application.Payments.Interfaces;
using CoreFinance.Application.Payments.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CoreFinance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IFixedAccountService, FixedAccountService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPainelService, PainelService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
