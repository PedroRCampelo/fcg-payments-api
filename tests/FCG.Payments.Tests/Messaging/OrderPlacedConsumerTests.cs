using FCG.Contracts;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Application.Services;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Policies;
using FCG.Payments.Infrastructure.Messaging;
using FCG.Payments.Tests.Fakes;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FCG.Payments.Tests.Messaging;

// O harness do MassTransit serializa a mensagem de verdade, então estes testes passam pelo contrato
// (records + enum PaymentStatus) e pelo caminho real de consumo e publicação, com o broker em memória.
public class OrderPlacedConsumerTests
{
    private sealed class Cenario : IAsyncDisposable
    {
        public ServiceProvider Provider { get; init; } = null!;
        public ITestHarness Harness { get; init; } = null!;
        public FakePagamentoRepository Pagamentos { get; init; } = null!;
        public ListLogger<PagamentoService> Logger { get; init; } = null!;

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private static async Task<Cenario> IniciarAsync(decimal limite = 200m)
    {
        var pagamentos = new FakePagamentoRepository();
        var logger = new ListLogger<PagamentoService>();

        var provider = new ServiceCollection()
            .AddSingleton<IPagamentoRepository>(pagamentos)
            .AddSingleton<IUnitOfWork>(new FakeUnitOfWork())
            .AddSingleton(new PoliticaDeAprovacao(limite))
            .AddSingleton<ILogger<PagamentoService>>(logger)
            .AddScoped<IEventPublisher, MassTransitEventPublisher>()
            .AddScoped<PagamentoService>()
            .AddMassTransitTestHarness(bus => bus.AddConsumer<OrderPlacedConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return new Cenario { Provider = provider, Harness = harness, Pagamentos = pagamentos, Logger = logger };
    }

    private static async Task<bool> EsperarAsync(Func<bool> condicao, int limiteEmMilissegundos = 5000)
    {
        var espera = System.Diagnostics.Stopwatch.StartNew();

        while (espera.ElapsedMilliseconds < limiteEmMilissegundos)
        {
            if (condicao()) return true;
            await Task.Delay(10);
        }

        return condicao();
    }

    private static OrderPlacedEvent Pedido(decimal preco, string titulo = "Hades") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), titulo, preco);

    [Fact]
    public async Task Pedido_Dentro_Do_Limite_Deve_Publicar_PaymentProcessedEvent_Approved()
    {
        await using var c = await IniciarAsync();
        var pedido = Pedido(100m);

        await c.Harness.Bus.Publish(pedido);

        Assert.True(await c.Harness.Consumed.Any<OrderPlacedEvent>());
        var publicado = (await c.Harness.Published.SelectAsync<PaymentProcessedEvent>().First()).Context.Message;
        Assert.Equal(PaymentStatus.Approved, publicado.Status);
        Assert.Equal(pedido.OrderId, publicado.OrderId);
        Assert.Equal(pedido.UserId, publicado.UserId);
        Assert.Equal(pedido.GameId, publicado.GameId);
        Assert.Equal("Hades", publicado.Titulo);
    }

    [Fact]
    public async Task Pedido_Acima_Do_Limite_Deve_Publicar_PaymentProcessedEvent_Rejected()
    {
        await using var c = await IniciarAsync();

        await c.Harness.Bus.Publish(Pedido(250m));

        Assert.True(await c.Harness.Consumed.Any<OrderPlacedEvent>());
        var publicado = (await c.Harness.Published.SelectAsync<PaymentProcessedEvent>().First()).Context.Message;
        Assert.Equal(PaymentStatus.Rejected, publicado.Status);
    }

    [Fact]
    public async Task O_Mesmo_Pedido_Entregue_Varias_Vezes_Deve_Gerar_Um_Unico_Pagamento_E_Um_Unico_Evento()
    {
        await using var c = await IniciarAsync();
        var pedido = Pedido(100m);

        // Uma entrega por vez, esperando o consumo terminar: este teste prova o DESCARTE da duplicata.
        // A disputa de entregas simultâneas é decidida pelo índice único do banco, que um fake em memória
        // não tem, e é verificada contra o PostgreSQL de verdade.
        for (var i = 1; i <= 5; i++)
        {
            await c.Harness.Bus.Publish(pedido);
            Assert.True(await EsperarAsync(() => c.Harness.Consumed.Select<OrderPlacedEvent>().Count() >= i));
        }

        Assert.Single(c.Pagamentos.Pagamentos);
        Assert.Single(c.Harness.Published.Select<PaymentProcessedEvent>());
        Assert.Equal(4, c.Logger.Registros.Count(r => r.Mensagem.Contains("descartado sem efeito")));
    }

    [Fact]
    public async Task Pedido_Com_Preco_Negativo_Deve_Falhar_Sem_Registrar_Pagamento_Nem_Publicar()
    {
        await using var c = await IniciarAsync();

        await c.Harness.Bus.Publish(Pedido(-1m));

        Assert.True(await c.Harness.Published.Any<Fault<OrderPlacedEvent>>());
        Assert.Empty(c.Pagamentos.Pagamentos);
        Assert.False(c.Harness.Published.Select<PaymentProcessedEvent>().Any());
    }
}
