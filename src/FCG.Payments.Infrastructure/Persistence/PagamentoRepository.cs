using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCG.Payments.Infrastructure.Persistence;

public class PagamentoRepository : IPagamentoRepository
{
    private readonly PaymentsDbContext _context;

    public PagamentoRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task<Pagamento?> ObterPorPedidoAsync(Guid pedidoId, CancellationToken cancellationToken = default) =>
        await _context.Pagamentos.AsNoTracking().FirstOrDefaultAsync(p => p.PedidoId == pedidoId, cancellationToken);

    public async Task<IReadOnlyList<Pagamento>> ListarAsync(CancellationToken cancellationToken = default) =>
        await _context.Pagamentos.AsNoTracking().OrderByDescending(p => p.ProcessadoEm).ToListAsync(cancellationToken);

    public async Task AdicionarAsync(Pagamento pagamento, CancellationToken cancellationToken = default) =>
        await _context.Pagamentos.AddAsync(pagamento, cancellationToken);
}
