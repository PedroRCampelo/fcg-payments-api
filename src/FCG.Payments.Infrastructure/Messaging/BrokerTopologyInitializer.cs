using FCG.Contracts;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.Payments.Infrastructure.Messaging;

// O publicador declara as filas dos consumidores. Sem isso, um PaymentProcessedEvent publicado antes de o
// Catalog ou o Notifications subirem pela primeira vez cairia numa exchange sem fila ligada e seria
// descartado. Com as filas já existindo, a mensagem fica retida até alguém consumir.
//
// Este serviço também CONSOME (OrderPlacedEvent), então não dá para usar DeployTopologyOnly no bus
// principal: a flag vale para o bus inteiro e desligaria o consumo. A declaração roda num bus de
// implantação separado, que sobe, cria a topologia (sem consumir nada) e para, antes do bus principal.
public static class BrokerTopologyInitializer
{
    private const int MaximoDeTentativas = 15;
    private static readonly TimeSpan IntervaloEntreTentativas = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LimiteDeCadaTentativa = TimeSpan.FromSeconds(10);

    public static async Task DeclararFilasDosConsumidoresAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var rabbit = services.GetRequiredService<RabbitMqSettings>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(BrokerTopologyInitializer));

        for (var tentativa = 1; ; tentativa++)
        {
            var bus = CriarBusDeImplantacao(rabbit);
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(LimiteDeCadaTentativa);

            try
            {
                await bus.StartAsync(limite.Token);
                await bus.StopAsync(cancellationToken);

                logger.LogInformation("Filas {FilaDoCatalog} e {FilaDoNotifications} declaradas no RabbitMQ, ligadas ao {Evento}",
                    rabbit.Queues.CatalogPaymentProcessed, rabbit.Queues.NotificationsPaymentProcessed, nameof(PaymentProcessedEvent));
                return;
            }
            catch (Exception ex) when (tentativa < MaximoDeTentativas && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("RabbitMQ indisponível (tentativa {Tentativa} de {Maximo}), nova tentativa em {Segundos}s: {Motivo}",
                    tentativa, MaximoDeTentativas, IntervaloEntreTentativas.TotalSeconds, ex.Message);

                await TentarPararAsync(bus);
                await Task.Delay(IntervaloEntreTentativas, cancellationToken);
            }
        }
    }

    private static IBusControl CriarBusDeImplantacao(RabbitMqSettings rabbit) =>
        Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(rabbit.Host, (ushort)rabbit.Port, rabbit.VirtualHost, host =>
            {
                host.Username(rabbit.Username);
                host.Password(rabbit.Password);
            });

            cfg.DeployTopologyOnly = true;

            cfg.ReceiveEndpoint(rabbit.Queues.CatalogPaymentProcessed, endpoint => endpoint.Bind<PaymentProcessedEvent>());
            cfg.ReceiveEndpoint(rabbit.Queues.NotificationsPaymentProcessed, endpoint => endpoint.Bind<PaymentProcessedEvent>());
        });

    private static async Task TentarPararAsync(IBusControl bus)
    {
        try
        {
            await bus.StopAsync(CancellationToken.None);
        }
        catch
        {
            // O bus falhou ao subir; parar é só limpeza e o erro que importa já foi registrado.
        }
    }
}
