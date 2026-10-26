using FCG.Payments.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FCG.Payments.Tests.Security;

public class JwtValidationSettingsTests
{
    private const string ChaveValida = "chave-de-testes-com-tamanho-suficiente-para-hmac-sha256";

    private static IConfiguration Configuracao(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    [Fact]
    public void Carregar_Deve_Ler_Chave_Issuer_E_Audience()
    {
        var settings = JwtValidationSettings.Carregar(Configuracao(new()
        {
            ["Jwt:SigningKey"] = ChaveValida,
            ["Jwt:Issuer"] = "fcg-users-api",
            ["Jwt:Audience"] = "fcg"
        }));

        Assert.Equal(ChaveValida, settings.SigningKey);
        Assert.Equal("fcg-users-api", settings.Issuer);
        Assert.Equal("fcg", settings.Audience);
    }

    [Fact]
    public void Carregar_Sem_Chave_Deve_Falhar_Apontando_A_Variavel_De_Ambiente()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => JwtValidationSettings.Carregar(Configuracao(new()
        {
            ["Jwt:Issuer"] = "fcg-users-api",
            ["Jwt:Audience"] = "fcg"
        })));

        Assert.Contains("JWT__SIGNINGKEY", erro.Message);
    }

    [Fact]
    public void Carregar_Com_Chave_Curta_Demais_Deve_Falhar()
    {
        Assert.Throws<InvalidOperationException>(() => JwtValidationSettings.Carregar(Configuracao(new()
        {
            ["Jwt:SigningKey"] = "curta",
            ["Jwt:Issuer"] = "fcg-users-api",
            ["Jwt:Audience"] = "fcg"
        })));
    }

    [Fact]
    public void Carregar_Sem_Issuer_Ou_Audience_Deve_Falhar()
    {
        Assert.Throws<InvalidOperationException>(() => JwtValidationSettings.Carregar(Configuracao(new()
        {
            ["Jwt:SigningKey"] = ChaveValida
        })));
    }
}
