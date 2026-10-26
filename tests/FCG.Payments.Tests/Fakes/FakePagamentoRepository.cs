using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Entities;

namespace FCG.Payments.Tests.Fakes;

public class FakePagamentoRepository : IPagamentoRepository
{
    private readonly object _trava = new();
    private readonly List<Pagamento> _pagamentos = new();

    /// <summary>Cópia dos pagamentos no momento da leitura (seguro para enumerar).</summary>
    public IReadOnlyList<Pagamento> Pagamentos
    {
        get { lock (_trava) return _pagamentos.ToList(); }
    }

    public Task<Pagamento?> ObterPorPedidoAsync(Guid pedidoId, CancellationToken cancellationToken = default)
    {
        lock (_trava) return Task.FromResult(_pagamentos.FirstOrDefault(p => p.PedidoId == pedidoId));
    }

    public Task<IReadOnlyList<Pagamento>> ListarAsync(CancellationToken cancellationToken = default)
    {
        lock (_trava) return Task.FromResult<IReadOnlyList<Pagamento>>(_pagamentos.OrderByDescending(p => p.ProcessadoEm).ToList());
    }

    public Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default)
    {
        lock (_trava) _pagamentos.Add(pagamento);
        return Task.CompletedTask;
    }
}
