using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Exceptions;

namespace FCG.Payments.Domain.Policies;

// Simulação de gateway de pagamento: aprova se o preço não passa do limite, recusa se passa.
// O limite vem de configuração (200 por padrão), para dar para demonstrar os dois caminhos.
public sealed class PoliticaDeAprovacao
{
    public decimal LimiteDeAprovacao { get; }

    public PoliticaDeAprovacao(decimal limiteDeAprovacao)
    {
        if (limiteDeAprovacao < 0)
            throw new LimiteDeAprovacaoInvalidoException("O limite de aprovação não pode ser negativo.");

        LimiteDeAprovacao = limiteDeAprovacao;
    }

    public StatusPagamento Decidir(decimal preco) =>
        preco <= LimiteDeAprovacao ? StatusPagamento.Aprovado : StatusPagamento.Recusado;
}
