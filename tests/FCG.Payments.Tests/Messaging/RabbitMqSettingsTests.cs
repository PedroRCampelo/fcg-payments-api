using FCG.Payments.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FCG.Payments.Tests.Messaging;

public class RabbitMqSettingsTests
{
    private static IConfiguration Configuracao(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    private static Dictionary<string, string?> Completa() => new()
    {
        ["RabbitMq:Host"] = "rabbitmq",
        ["RabbitMq:Username"] = "fcg",
        ["RabbitMq:Password"] = "segredo",
        ["RabbitMq:Queues:PaymentsOrderPlaced"] = "payments.order-placed",
        ["RabbitMq:Queues:CatalogPaymentProcessed"] = "catalog.payment-processed",
        ["RabbitMq:Queues:NotificationsPaymentProcessed"] = "notifications.payment-processed"
    };

    [Fact]
    public void Carregar_Deve_Ler_Host_Credenciais_E_Os_Nomes_Das_Tres_Filas()
    {
        var settings = RabbitMqSettings.Carregar(Configuracao(Completa()));

        Assert.Equal("rabbitmq", settings.Host);
        Assert.Equal(5672, settings.Port);
        Assert.Equal("payments.order-placed", settings.Queues.PaymentsOrderPlaced);
        Assert.Equal("catalog.payment-processed", settings.Queues.CatalogPaymentProcessed);
        Assert.Equal("notifications.payment-processed", settings.Queues.NotificationsPaymentProcessed);
    }

    [Theory]
    [InlineData("RabbitMq:Username", "RabbitMq__Username")]
    [InlineData("RabbitMq:Password", "RabbitMq__Password")]
    [InlineData("RabbitMq:Queues:PaymentsOrderPlaced", "RabbitMq__Queues__PaymentsOrderPlaced")]
    [InlineData("RabbitMq:Queues:CatalogPaymentProcessed", "RabbitMq__Queues__CatalogPaymentProcessed")]
    [InlineData("RabbitMq:Queues:NotificationsPaymentProcessed", "RabbitMq__Queues__NotificationsPaymentProcessed")]
    public void Carregar_Sem_Um_Valor_Obrigatorio_Deve_Falhar_Apontando_A_Variavel_De_Ambiente(string chave, string variavel)
    {
        var valores = Completa();
        valores.Remove(chave);

        var erro = Assert.Throws<InvalidOperationException>(() => RabbitMqSettings.Carregar(Configuracao(valores)));

        Assert.Contains(variavel, erro.Message);
    }
}
