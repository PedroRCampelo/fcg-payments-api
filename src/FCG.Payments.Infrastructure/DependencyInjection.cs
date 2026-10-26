using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Exceptions;
using FCG.Payments.Infrastructure.Configuration;
using FCG.Payments.Infrastructure.Messaging;
using FCG.Payments.Infrastructure.Persistence;
using FCG.Payments.Infrastructure.Security;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Payments.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string ausente. Defina ConnectionStrings__Default.");

        services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton(JwtValidationSettings.Carregar(configuration));
        services.AddSingleton(PoliticaDeAprovacaoSettings.Carregar(configuration));

        var rabbit = RabbitMqSettings.Carregar(configuration);
        services.AddSingleton(rabbit);
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        AdicionarMensageria(services, rabbit);

        return services;
    }

    private static void AdicionarMensageria(IServiceCollection services, RabbitMqSettings rabbit)
    {
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<OrderPlacedConsumer>();

            bus.AddEntityFrameworkOutbox<PaymentsDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbit.Host, (ushort)rabbit.Port, rabbit.VirtualHost, host =>
                {
                    host.Username(rabbit.Username);
                    host.Password(rabbit.Password);
                });

                // Fila com nome fixo e vindo de configuração: o CatalogAPI, que publica o evento,
                // precisa conhecer o mesmo nome para declará-la antes de este serviço subir.
                cfg.ReceiveEndpoint(rabbit.Queues.PaymentsOrderPlaced, endpoint =>
                {
                    endpoint.UseMessageRetry(retry =>
                    {
                        // Dado de pedido inválido não melhora ao repetir: vai direto para a fila de erro.
                        // Já uma colisão de concorrência (duas entregas do mesmo pedido, ConflitoException)
                        // é retentada, e na segunda vez o pagamento já existe e a mensagem é descartada.
                        retry.Ignore<DomainException>();
                        retry.Interval(3, TimeSpan.FromSeconds(2));
                    });

                    // Outbox no nível do consumidor: o PaymentProcessedEvent publicado aqui dentro é gravado
                    // na MESMA transação do pagamento. Sem isto, o Publish iria direto ao broker antes do
                    // commit, e a entrega que perde uma corrida já teria publicado um evento duplicado.
                    endpoint.UseEntityFrameworkOutbox<PaymentsDbContext>(context);

                    endpoint.ConfigureConsumer<OrderPlacedConsumer>(context);
                });
            });
        });
    }
}
