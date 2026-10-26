using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Entities;

namespace FCG.Payments.Tests.Fakes;

public class FakePagamentoRepository : IPagamentoRepository
{
    public List<Pagamento> Pagamentos { get; } = new();

    public Task<Pagamento?> ObterPorPedidoAsync(Guid pedidoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pagamentos.FirstOrDefault(p => p.PedidoId == pedidoId));

    public Task<IReadOnlyList<Pagamento>> ListarAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Pagamento>>(Pagamentos.OrderByDescending(p => p.ProcessadoEm).ToList());

    public Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default)
    {
        Pagamentos.Add(pagamento);
        return Task.CompletedTask;
    }
}
