using FCG.Contracts;
using FCG.Payments.Application.Services;
using MassTransit;

namespace FCG.Payments.Infrastructure.Messaging;

// Só traduz a mensagem em chamada: toda a regra (decisão e idempotência) está no PagamentoService.
public class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly PagamentoService _pagamentos;

    public OrderPlacedConsumer(PagamentoService pagamentos)
    {
        _pagamentos = pagamentos;
    }

    public Task Consume(ConsumeContext<OrderPlacedEvent> context) =>
        _pagamentos.ProcessarPedidoAsync(context.Message, context.CancellationToken);
}
