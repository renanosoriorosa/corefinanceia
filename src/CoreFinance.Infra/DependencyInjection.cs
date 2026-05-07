using CoreFinance.Domain.Interfaces.Repositories;
using CoreFinance.Infra.Data;
using CoreFinance.Infra.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreFinance.Infra;

public static class DependencyInjection
{
    public static IServiceCollection AddInfra(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IFixedAccountRepository, FixedAccountRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        return services;
    }
}
