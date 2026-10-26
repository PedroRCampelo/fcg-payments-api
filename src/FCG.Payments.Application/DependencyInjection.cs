using FCG.Payments.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Payments.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PagamentoService>();

        return services;
    }
}
