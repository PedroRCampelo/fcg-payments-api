using FCG.Payments.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FCG.Payments.Infrastructure.Persistence;

public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
    }

    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);

        // Tabelas do outbox/inbox transacional do MassTransit: eventos pendentes de entrega ao RabbitMQ
        // e mensagens já consumidas (para não processar a mesma mensagem duas vezes).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
