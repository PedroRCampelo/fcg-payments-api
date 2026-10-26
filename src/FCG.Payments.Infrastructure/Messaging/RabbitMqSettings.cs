using Microsoft.Extensions.Configuration;

namespace FCG.Payments.Infrastructure.Messaging;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public QueuesSettings Queues { get; set; } = new();

    public static RabbitMqSettings Carregar(IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName).Get<RabbitMqSettings>() ?? new RabbitMqSettings();

        if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("Credenciais do RabbitMQ ausentes. Defina RabbitMq__Username e RabbitMq__Password.");

        if (string.IsNullOrWhiteSpace(settings.Queues.PaymentsOrderPlaced))
            throw new InvalidOperationException("Nome da fila do Payments ausente. Defina RabbitMq__Queues__PaymentsOrderPlaced.");

        if (string.IsNullOrWhiteSpace(settings.Queues.CatalogPaymentProcessed))
            throw new InvalidOperationException("Nome da fila do Catalog ausente. Defina RabbitMq__Queues__CatalogPaymentProcessed.");

        if (string.IsNullOrWhiteSpace(settings.Queues.NotificationsPaymentProcessed))
            throw new InvalidOperationException("Nome da fila do Notifications ausente. Defina RabbitMq__Queues__NotificationsPaymentProcessed.");

        return settings;
    }
}

public class QueuesSettings
{
    // Fila deste serviço para o OrderPlacedEvent. É o CatalogAPI quem a declara no startup dele.
    public string PaymentsOrderPlaced { get; set; } = string.Empty;

    // Filas dos consumidores do PaymentProcessedEvent. Este serviço, que publica o evento, as declara no
    // startup, então precisa ler os mesmos nomes que os consumidores (no Kubernetes, do mesmo ConfigMap).
    public string CatalogPaymentProcessed { get; set; } = string.Empty;
    public string NotificationsPaymentProcessed { get; set; } = string.Empty;
}
