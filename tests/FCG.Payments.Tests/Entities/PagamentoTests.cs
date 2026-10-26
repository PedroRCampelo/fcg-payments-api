using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Exceptions;
using FCG.Payments.Domain.Policies;
using Xunit;

namespace FCG.Payments.Tests.Entities;

public class PagamentoTests
{
    private static readonly PoliticaDeAprovacao Politica = new(200m);

    private static Pagamento Registrar(
        Guid? pedidoId = null, Guid? usuarioId = null, Guid? jogoId = null,
        string titulo = "Hades", decimal preco = 100m) =>
        Pagamento.Registrar(pedidoId ?? Guid.NewGuid(), usuarioId ?? Guid.NewGuid(), jogoId ?? Guid.NewGuid(), titulo, preco, Politica);

    [Fact]
    public void Deve_Registrar_Pagamento_Aprovado_Quando_O_Preco_Estiver_Dentro_Do_Limite()
    {
        var pedidoId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var jogoId = Guid.NewGuid();

        var pagamento = Pagamento.Registrar(pedidoId, usuarioId, jogoId, "Hades", 100m, Politica);

        Assert.NotEqual(Guid.Empty, pagamento.Id);
        Assert.Equal(pedidoId, pagamento.PedidoId);
        Assert.Equal(usuarioId, pagamento.UsuarioId);
        Assert.Equal(jogoId, pagamento.JogoId);
        Assert.Equal("Hades", pagamento.TituloJogo);
        Assert.Equal(100m, pagamento.Preco);
        Assert.Equal(StatusPagamento.Aprovado, pagamento.Status);
        Assert.InRange(pagamento.ProcessadoEm, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(DateTimeKind.Utc, pagamento.ProcessadoEm.Kind);
    }

    [Fact]
    public void Deve_Registrar_Pagamento_Recusado_Quando_O_Preco_Passar_Do_Limite()
    {
        Assert.Equal(StatusPagamento.Recusado, Registrar(preco: 250m).Status);
    }

    [Fact]
    public void A_Fronteira_Do_Limite_Deve_Ser_Aprovada_E_O_Centavo_Seguinte_Recusado()
    {
        Assert.Equal(StatusPagamento.Aprovado, Registrar(preco: 200.00m).Status);
        Assert.Equal(StatusPagamento.Recusado, Registrar(preco: 200.01m).Status);
    }

    [Fact]
    public void Deve_Remover_Espacos_Do_Titulo()
    {
        Assert.Equal("Hades", Registrar(titulo: "  Hades  ").TituloJogo);
    }

    [Fact]
    public void Dois_Pagamentos_Devem_Ter_Ids_Diferentes_Mesmo_Para_O_Mesmo_Pedido()
    {
        var pedidoId = Guid.NewGuid();

        Assert.NotEqual(Registrar(pedidoId: pedidoId).Id, Registrar(pedidoId: pedidoId).Id);
    }

    [Fact]
    public void Pedido_Sem_Identificador_Deve_Lancar_Excecao()
    {
        Assert.Throws<PedidoInvalidoException>(() => Registrar(pedidoId: Guid.Empty));
    }

    [Fact]
    public void Usuario_Ou_Jogo_Ausente_Deve_Lancar_Excecao()
    {
        Assert.Throws<PedidoInvalidoException>(() => Registrar(usuarioId: Guid.Empty));
        Assert.Throws<PedidoInvalidoException>(() => Registrar(jogoId: Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Titulo_Ausente_Deve_Lancar_Excecao(string? titulo)
    {
        Assert.Throws<PedidoInvalidoException>(() => Registrar(titulo: titulo!));
    }

    [Fact]
    public void Titulo_Acima_Do_Limite_Do_Banco_Deve_Lancar_Excecao()
    {
        Assert.Throws<PedidoInvalidoException>(() => Registrar(titulo: new string('a', Pagamento.TamanhoMaximoDoTitulo + 1)));
        Assert.Equal(Pagamento.TamanhoMaximoDoTitulo, Registrar(titulo: new string('a', Pagamento.TamanhoMaximoDoTitulo)).TituloJogo.Length);
    }

    [Fact]
    public void Preco_Negativo_Deve_Lancar_Excecao()
    {
        Assert.Throws<PedidoInvalidoException>(() => Registrar(preco: -0.01m));
    }

    [Fact]
    public void Jogo_Gratuito_Deve_Ser_Aprovado()
    {
        Assert.Equal(StatusPagamento.Aprovado, Registrar(preco: 0m).Status);
    }
}
