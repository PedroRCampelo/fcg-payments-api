using System.Collections.Concurrent;
using System.Text.Json;
using FCG.Contracts;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FCG.Payments.Tests.Contracts;

// O PaymentStatus é o único enum que trafega nas mensagens. Estes testes travam o formato do contrato:
// valores numéricos fixos (o 0 fica de fora de propósito, assim um campo ausente nunca vira "Approved")
// e texto no JSON, para que reordenar ou inserir um valor no enum não troque o significado em silêncio.
public class PaymentStatusContratoTests
{
    private sealed class Capturados
    {
        public ConcurrentBag<string> Corpos { get; } = new();
        public ConcurrentBag<PaymentProcessedEvent> Eventos { get; } = new();
    }

    private sealed class CapturaConsumer(Capturados capturados) : IConsumer<PaymentProcessedEvent>
    {
        public Task Consume(ConsumeContext<PaymentProcessedEvent> context)
        {
            capturados.Corpos.Add(context.ReceiveContext.Body.GetString());
            capturados.Eventos.Add(context.Message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Os_Valores_Do_Enum_Sao_Fixos()
    {
        Assert.Equal(1, (int)PaymentStatus.Approved);
        Assert.Equal(2, (int)PaymentStatus.Rejected);
    }

    [Fact]
    public void O_Zero_Nao_E_Um_Status_Valido()
    {
        Assert.False(Enum.IsDefined(typeof(PaymentStatus), 0));
    }

    [Theory]
    [InlineData(PaymentStatus.Approved, "Approved")]
    [InlineData(PaymentStatus.Rejected, "Rejected")]
    public async Task O_Status_Vai_Como_Texto_No_Json_E_Volta_Igual(PaymentStatus status, string textoEsperado)
    {
        var capturados = new Capturados();
        await using var provider = new ServiceCollection()
            .AddSingleton(capturados)
            .AddMassTransitTestHarness(bus => bus.AddConsumer<CapturaConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var evento = new PaymentProcessedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hades", status);
        await harness.Bus.Publish(evento);
        Assert.True(await harness.Consumed.Any<PaymentProcessedEvent>());

        using var corpo = JsonDocument.Parse(Assert.Single(capturados.Corpos));
        var campo = corpo.RootElement.GetProperty("message").GetProperty("status");

        Assert.Equal(JsonValueKind.String, campo.ValueKind);
        Assert.Equal(textoEsperado, campo.GetString());
        Assert.Equal(status, Assert.Single(capturados.Eventos).Status);
    }
}
