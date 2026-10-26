using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Exceptions;
using FCG.Payments.Domain.Policies;

namespace FCG.Payments.Domain.Entities;

// Um pagamento por pedido. O PedidoId é o OrderId dos eventos e é único: é ele que garante que o
// mesmo pedido nunca é cobrado (nem anunciado) duas vezes.
public sealed class Pagamento
{
    public const int TamanhoMaximoDoTitulo = 200;

    public Guid Id { get; private set; }
    public Guid PedidoId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Guid JogoId { get; private set; }
    public string TituloJogo { get; private set; } = null!;
    public decimal Preco { get; private set; }
    public StatusPagamento Status { get; private set; }
    public DateTime ProcessadoEm { get; private set; }

    private Pagamento()
    {
    }

    public static Pagamento Registrar(
        Guid pedidoId,
        Guid usuarioId,
        Guid jogoId,
        string tituloJogo,
        decimal preco,
        PoliticaDeAprovacao politica)
    {
        if (pedidoId == Guid.Empty)
            throw new PedidoInvalidoException("O pedido não tem identificador.");

        if (usuarioId == Guid.Empty || jogoId == Guid.Empty)
            throw new PedidoInvalidoException($"O pedido {pedidoId} não identifica o usuário ou o jogo.");

        if (string.IsNullOrWhiteSpace(tituloJogo))
            throw new PedidoInvalidoException($"O pedido {pedidoId} não tem o título do jogo.");

        if (tituloJogo.Trim().Length > TamanhoMaximoDoTitulo)
            throw new PedidoInvalidoException($"O título do pedido {pedidoId} passa de {TamanhoMaximoDoTitulo} caracteres.");

        if (preco < 0)
            throw new PedidoInvalidoException($"O pedido {pedidoId} tem preço negativo ({preco}).");

        return new Pagamento
        {
            Id = Guid.NewGuid(),
            PedidoId = pedidoId,
            UsuarioId = usuarioId,
            JogoId = jogoId,
            TituloJogo = tituloJogo.Trim(),
            Preco = preco,
            Status = politica.Decidir(preco),
            ProcessadoEm = DateTime.UtcNow
        };
    }
}
