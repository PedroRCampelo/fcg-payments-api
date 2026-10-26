using FCG.Payments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Payments.Infrastructure.Persistence.Configurations;

public class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public const string IndiceDePedidoUnico = "UX_Pagamentos_PedidoId";

    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("Pagamentos");

        builder.HasKey(p => p.Id);

        // Um pagamento por pedido. É a palavra final sobre a idempotência: mesmo que duas entregas do
        // mesmo OrderPlacedEvent passem juntas pela checagem da aplicação, só uma consegue gravar.
        builder.HasIndex(p => p.PedidoId)
            .IsUnique()
            .HasDatabaseName(IndiceDePedidoUnico);

        builder.Property(p => p.TituloJogo)
            .IsRequired()
            .HasMaxLength(Pagamento.TamanhoMaximoDoTitulo);

        builder.Property(p => p.Preco)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.ProcessadoEm).IsRequired();

        builder.HasIndex(p => p.ProcessadoEm);
    }
}
