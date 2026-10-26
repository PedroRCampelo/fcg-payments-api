namespace FCG.Payments.Application.Dtos;

public record PagamentoResponse(
    Guid Id,
    Guid PedidoId,
    Guid UsuarioId,
    Guid JogoId,
    string TituloJogo,
    decimal Preco,
    string Status,
    DateTime ProcessadoEm);
