using FCG.Contracts;
using FCG.Payments.Application.Exceptions;
using FCG.Payments.Application.Services;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Exceptions;
using FCG.Payments.Domain.Policies;
using FCG.Payments.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FCG.Payments.Tests.Services;

public class PagamentoServiceTests
{
    private readonly List<string> _chamadas = new();
    private readonly FakePagamentoRepository _pagamentos = new();
    private readonly FakeEventPublisher _eventos;
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly ListLogger<PagamentoService> _logger = new();

    public PagamentoServiceTests()
    {
        _eventos = new FakeEventPublisher(_chamadas);
        _unitOfWork = new FakeUnitOfWork(_chamadas);
    }

    private PagamentoService CriarServico(decimal limite = 200m) =>
        new(_pagamentos, _unitOfWork, _eventos, new PoliticaDeAprovacao(limite), _logger);

    private static OrderPlacedEvent Pedido(decimal preco, Guid? orderId = null, string titulo = "Hades") =>
        new(orderId ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), titulo, preco);

    [Fact]
    public async Task Pedido_Dentro_Do_Limite_Deve_Gerar_Pagamento_Aprovado_E_Publicar_Approved()
    {
        var pedido = Pedido(100m);

        await CriarServico().ProcessarPedidoAsync(pedido);

        var pagamento = Assert.Single(_pagamentos.Pagamentos);
        Assert.Equal(StatusPagamento.Aprovado, pagamento.Status);
        var evento = Assert.IsType<PaymentProcessedEvent>(Assert.Single(_eventos.Publicados));
        Assert.Equal(PaymentStatus.Approved, evento.Status);
    }

    [Fact]
    public async Task Pedido_Acima_Do_Limite_Deve_Gerar_Pagamento_Recusado_E_Publicar_Rejected()
    {
        await CriarServico().ProcessarPedidoAsync(Pedido(250m));

        Assert.Equal(StatusPagamento.Recusado, Assert.Single(_pagamentos.Pagamentos).Status);
        Assert.Equal(PaymentStatus.Rejected, Assert.IsType<PaymentProcessedEvent>(Assert.Single(_eventos.Publicados)).Status);
    }

    [Fact]
    public async Task O_Evento_Publicado_Deve_Repetir_OrderId_Usuario_Jogo_E_Titulo_Do_Pedido()
    {
        var pedido = Pedido(100m, titulo: "Cyberpunk 2077");

        await CriarServico().ProcessarPedidoAsync(pedido);

        var evento = Assert.IsType<PaymentProcessedEvent>(Assert.Single(_eventos.Publicados));
        Assert.Equal(pedido.OrderId, evento.OrderId);
        Assert.Equal(pedido.UserId, evento.UserId);
        Assert.Equal(pedido.GameId, evento.GameId);
        Assert.Equal("Cyberpunk 2077", evento.Titulo);
    }

    [Theory]
    [InlineData("200.00", PaymentStatus.Approved)]
    [InlineData("199.99", PaymentStatus.Approved)]
    [InlineData("200.01", PaymentStatus.Rejected)]
    public async Task A_Fronteira_Do_Limite_Deve_Valer_Tambem_Pelo_Servico(string preco, PaymentStatus esperado)
    {
        await CriarServico().ProcessarPedidoAsync(Pedido(decimal.Parse(preco, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(esperado, Assert.IsType<PaymentProcessedEvent>(Assert.Single(_eventos.Publicados)).Status);
    }

    [Fact]
    public async Task O_Limite_Configurado_Deve_Mudar_O_Resultado_Do_Mesmo_Pedido()
    {
        await CriarServico(limite: 50m).ProcessarPedidoAsync(Pedido(100m));

        Assert.Equal(PaymentStatus.Rejected, Assert.IsType<PaymentProcessedEvent>(Assert.Single(_eventos.Publicados)).Status);
    }

    [Fact]
    public async Task Deve_Registrar_O_Evento_Antes_De_Salvar_Para_Entrar_Na_Mesma_Transacao()
    {
        await CriarServico().ProcessarPedidoAsync(Pedido(100m));

        Assert.Equal(new[] { "publicar", "salvar" }, _chamadas);
        Assert.Equal(1, _unitOfWork.Salvamentos);
    }

    [Fact]
    public async Task Pedido_Repetido_Deve_Ser_Descartado_Sem_Reprocessar_Sem_Republicar_E_Sem_Salvar()
    {
        var pedido = Pedido(100m);
        var servico = CriarServico();
        await servico.ProcessarPedidoAsync(pedido);
        _eventos.Publicados.Clear();
        _chamadas.Clear();

        await servico.ProcessarPedidoAsync(pedido);

        Assert.Single(_pagamentos.Pagamentos);
        Assert.Empty(_eventos.Publicados);
        Assert.Empty(_chamadas);
        Assert.Equal(1, _unitOfWork.Salvamentos);
    }

    [Fact]
    public async Task O_Descarte_Deve_Ser_Logado_Com_O_Motivo_E_O_Resultado_Ja_Registrado()
    {
        var pedido = Pedido(250m);
        var servico = CriarServico();
        await servico.ProcessarPedidoAsync(pedido);
        _logger.Registros.Clear();

        await servico.ProcessarPedidoAsync(pedido);

        var (nivel, mensagem) = Assert.Single(_logger.Registros);
        Assert.Equal(LogLevel.Warning, nivel);
        Assert.Contains("descartado sem efeito", mensagem);
        Assert.Contains(pedido.OrderId.ToString(), mensagem);
        Assert.Contains("Recusado", mensagem);
        Assert.Contains("nem republicado", mensagem);
    }

    [Fact]
    public async Task Pedido_Repetido_Com_Preco_Diferente_Nao_Deve_Mudar_O_Resultado_Ja_Decidido()
    {
        var orderId = Guid.NewGuid();
        var servico = CriarServico();
        await servico.ProcessarPedidoAsync(Pedido(100m, orderId));

        await servico.ProcessarPedidoAsync(Pedido(999m, orderId));

        var pagamento = Assert.Single(_pagamentos.Pagamentos);
        Assert.Equal(StatusPagamento.Aprovado, pagamento.Status);
        Assert.Equal(100m, pagamento.Preco);
        Assert.Single(_eventos.Publicados);
    }

    [Fact]
    public async Task Pedidos_Diferentes_Do_Mesmo_Usuario_E_Jogo_Sao_Pagamentos_Independentes()
    {
        var servico = CriarServico();
        var usuario = Guid.NewGuid();
        var jogo = Guid.NewGuid();

        await servico.ProcessarPedidoAsync(new OrderPlacedEvent(Guid.NewGuid(), usuario, jogo, "Hades", 100m));
        await servico.ProcessarPedidoAsync(new OrderPlacedEvent(Guid.NewGuid(), usuario, jogo, "Hades", 100m));

        Assert.Equal(2, _pagamentos.Pagamentos.Count);
        Assert.Equal(2, _eventos.Publicados.Count);
    }

    [Fact]
    public async Task Pedido_Invalido_Deve_Lancar_Excecao_De_Dominio_Sem_Persistir_Nem_Publicar()
    {
        await Assert.ThrowsAsync<PedidoInvalidoException>(() => CriarServico().ProcessarPedidoAsync(Pedido(-5m)));
        await Assert.ThrowsAsync<PedidoInvalidoException>(() => CriarServico().ProcessarPedidoAsync(Pedido(10m, titulo: " ")));

        Assert.Empty(_pagamentos.Pagamentos);
        Assert.Empty(_eventos.Publicados);
        Assert.Empty(_chamadas);
    }

    [Fact]
    public async Task Resultado_Nao_Deve_Ser_Logado_Quando_O_Salvamento_Falha()
    {
        _unitOfWork.FalharAoSalvarCom = new ConflitoException("Já existe pagamento para este pedido.");

        await Assert.ThrowsAsync<ConflitoException>(() => CriarServico().ProcessarPedidoAsync(Pedido(100m)));

        Assert.DoesNotContain(_logger.Registros, r => r.Mensagem.Contains("Aprovado") || r.Mensagem.Contains("Recusado"));
    }

    [Fact]
    public async Task Sucesso_Deve_Logar_O_Resultado_Uma_Unica_Vez_Depois_De_Salvar()
    {
        await CriarServico().ProcessarPedidoAsync(Pedido(100m));

        var (nivel, mensagem) = Assert.Single(_logger.Registros);
        Assert.Equal(LogLevel.Information, nivel);
        Assert.Contains("Aprovado", mensagem);
        Assert.Contains("200", mensagem);
    }

    [Fact]
    public async Task Listar_Deve_Retornar_Os_Pagamentos_Com_O_Status_Por_Extenso()
    {
        var servico = CriarServico();
        await servico.ProcessarPedidoAsync(Pedido(100m));
        await servico.ProcessarPedidoAsync(Pedido(250m));

        var resposta = await servico.ListarAsync();

        Assert.Equal(2, resposta.Count);
        Assert.Contains(resposta, p => p.Status == "Aprovado" && p.Preco == 100m);
        Assert.Contains(resposta, p => p.Status == "Recusado" && p.Preco == 250m);
    }
}
