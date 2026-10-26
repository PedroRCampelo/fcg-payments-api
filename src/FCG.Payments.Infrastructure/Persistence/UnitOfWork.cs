using FCG.Payments.Application.Exceptions;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FCG.Payments.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly PaymentsDbContext _context;

    public UnitOfWork(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task SalvarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        // Duas entregas do mesmo pedido passam juntas pela checagem de existência. O índice único é quem
        // decide: a perdedora falha aqui, é retentada e, na segunda vez, é descartada por já haver pagamento.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
                                          && pg.ConstraintName == PagamentoConfiguration.IndiceDePedidoUnico)
        {
            throw new ConflitoException("Já existe pagamento registrado para este pedido.");
        }
    }
}
