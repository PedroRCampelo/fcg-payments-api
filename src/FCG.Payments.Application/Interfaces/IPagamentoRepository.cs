using FCG.Payments.Domain.Entities;

namespace FCG.Payments.Application.Interfaces;

public interface IPagamentoRepository
{
    Task<Pagamento?> ObterPorPedidoAsync(Guid pedidoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Pagamento>> ListarAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default);
}
