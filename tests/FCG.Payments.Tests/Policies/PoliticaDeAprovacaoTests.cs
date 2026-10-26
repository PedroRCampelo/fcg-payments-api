using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Exceptions;
using FCG.Payments.Domain.Policies;
using Xunit;

namespace FCG.Payments.Tests.Policies;

public class PoliticaDeAprovacaoTests
{
    private readonly PoliticaDeAprovacao _politica = new(200m);

    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("59.90")]
    [InlineData("199.99")]
    [InlineData("200")]
    [InlineData("200.00")]
    public void Deve_Aprovar_Quando_O_Preco_For_Menor_Ou_Igual_Ao_Limite(string preco)
    {
        Assert.Equal(StatusPagamento.Aprovado, _politica.Decidir(decimal.Parse(preco, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("200.01")]
    [InlineData("250")]
    [InlineData("1000000")]
    public void Deve_Recusar_Quando_O_Preco_Passar_Do_Limite(string preco)
    {
        Assert.Equal(StatusPagamento.Recusado, _politica.Decidir(decimal.Parse(preco, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void O_Limite_Deve_Ser_Configuravel()
    {
        var rigorosa = new PoliticaDeAprovacao(50m);
        var generosa = new PoliticaDeAprovacao(500m);

        Assert.Equal(StatusPagamento.Recusado, rigorosa.Decidir(100m));
        Assert.Equal(StatusPagamento.Aprovado, generosa.Decidir(100m));
    }

    [Fact]
    public void Limite_Zero_Deve_Aprovar_Somente_Jogos_Gratuitos()
    {
        var politica = new PoliticaDeAprovacao(0m);

        Assert.Equal(StatusPagamento.Aprovado, politica.Decidir(0m));
        Assert.Equal(StatusPagamento.Recusado, politica.Decidir(0.01m));
    }

    [Fact]
    public void Limite_Negativo_Deve_Lancar_Excecao()
    {
        Assert.Throws<LimiteDeAprovacaoInvalidoException>(() => new PoliticaDeAprovacao(-1m));
    }
}
