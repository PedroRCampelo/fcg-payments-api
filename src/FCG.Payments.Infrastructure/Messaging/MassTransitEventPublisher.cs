using FCG.Payments.Application.Interfaces;
using MassTransit;

namespace FCG.Payments.Infrastructure.Messaging;

// Dentro do consumidor, o IPublishEndpoint é o do contexto de consumo e, com o outbox ligado no endpoint
// (UseEntityFrameworkOutbox), grava a mensagem na tabela OutboxMessage junto com o SaveChanges.
public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublicarAsync<TEvento>(TEvento evento, CancellationToken cancellationToken = default)
        where TEvento : class =>
        _publishEndpoint.Publish(evento, cancellationToken);
}
