using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FCG.Payments.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private const int MaximoDeTentativas = 15;
    private static readonly TimeSpan IntervaloEntreTentativas = TimeSpan.FromSeconds(3);

    public static async Task InicializarAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        var context = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        // No docker compose e no Kubernetes o banco pode demorar mais que a aplicação para ficar
        // pronto. Falhas de conexão são repetidas; qualquer outro erro de migration falha de imediato.
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await context.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Migrations aplicadas no banco de pagamentos");
                return;
            }
            catch (Exception ex) when (EhFalhaDeConexao(ex) && tentativa < MaximoDeTentativas)
            {
                logger.LogWarning("Banco indisponível (tentativa {Tentativa} de {Maximo}), nova tentativa em {Segundos}s: {Motivo}",
                    tentativa, MaximoDeTentativas, IntervaloEntreTentativas.TotalSeconds, ex.Message);
                await Task.Delay(IntervaloEntreTentativas, cancellationToken);
            }
        }
    }

    private static bool EhFalhaDeConexao(Exception ex) =>
        ex is NpgsqlException { IsTransient: true } or SocketException or TimeoutException
        || ex.InnerException is not null && EhFalhaDeConexao(ex.InnerException);
}
