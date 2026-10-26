using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FCG.Payments.Infrastructure.Persistence;

// Usada apenas pelo dotnet-ef em tempo de design. Gerar uma migration não abre conexão,
// então a string abaixo é só para o provider saber que o destino é PostgreSQL.
public class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql("Host=localhost;Database=fcg_payments")
            .Options;

        return new PaymentsDbContext(options);
    }
}
