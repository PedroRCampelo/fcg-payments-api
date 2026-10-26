using FCG.Payments.Domain.Enums;
using FCG.Payments.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FCG.Payments.Tests.Configuration;

public class PoliticaDeAprovacaoSettingsTests
{
    private static IConfiguration Configuracao(string? limite)
    {
        var valores = new Dictionary<string, string?>();
        if (limite is not null) valores["Payments:LimiteDeAprovacao"] = limite;
        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [Theory]
    [InlineData("200", 200)]
    [InlineData("199.90", 199.90)]
    [InlineData("0", 0)]
    [InlineData("1000", 1000)]
    public void Carregar_Deve_Ler_O_Limite_Com_Ponto_Decimal_Independente_Da_Cultura(string texto, double esperado)
    {
        var politica = PoliticaDeAprovacaoSettings.Carregar(Configuracao(texto));

        Assert.Equal((decimal)esperado, politica.LimiteDeAprovacao);
    }

    [Fact]
    public void O_Limite_Carregado_Deve_Decidir_Como_Esperado()
    {
        var politica = PoliticaDeAprovacaoSettings.Carregar(Configuracao("200"));

        Assert.Equal(StatusPagamento.Aprovado, politica.Decidir(200m));
        Assert.Equal(StatusPagamento.Recusado, politica.Decidir(200.01m));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Carregar_Sem_Limite_Deve_Falhar_Apontando_A_Variavel_De_Ambiente(string? texto)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => PoliticaDeAprovacaoSettings.Carregar(Configuracao(texto)));

        Assert.Contains("Payments__LimiteDeAprovacao", erro.Message);
    }

    [Theory]
    [InlineData("duzentos")]
    [InlineData("200,50")]
    [InlineData("1,000")]
    [InlineData("1e")]
    [InlineData("1e3")]
    public void Carregar_Com_Valor_Que_Nao_E_Um_Decimal_Com_Ponto_Deve_Falhar_Em_Vez_De_Interpretar_Errado(string texto)
    {
        Assert.Throws<InvalidOperationException>(() => PoliticaDeAprovacaoSettings.Carregar(Configuracao(texto)));
    }

    [Fact]
    public void Carregar_Com_Limite_Negativo_Deve_Falhar()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => PoliticaDeAprovacaoSettings.Carregar(Configuracao("-10")));

        Assert.Contains("negativo", erro.Message);
    }
}
