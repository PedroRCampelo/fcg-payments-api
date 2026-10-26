using FCG.Contracts;
using FCG.Payments.Application.Dtos;
using FCG.Payments.Application.Interfaces;
using FCG.Payments.Domain.Entities;
using FCG.Payments.Domain.Enums;
using FCG.Payments.Domain.Policies;
using Microsoft.Extensions.Logging;

namespace FCG.Payments.Application.Services;

public class PagamentoService
{
    private readonly IPagamentoRepository _pagamentos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _eventos;
    private readonly PoliticaDeAprovacao _politica;
    private readonly ILogger<PagamentoService> _logger;

    public PagamentoService(
        IPagamentoRepository pagamentos,
        IUnitOfWork unitOfWork,
        IEventPublisher eventos,
        PoliticaDeAprovacao politica,
        ILogger<PagamentoService> logger)
    {
        _pagamentos = pagamentos;
        _unitOfWork = unitOfWork;
        _eventos = eventos;
        _politica = politica;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PagamentoResponse>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var pagamentos = await _pagamentos.ListarAsync(cancellationToken);
        return pagamentos.Select(ParaResponse).ToList();
    }

    // Idempotente pelo OrderId: o broker entrega "pelo menos uma vez", então o mesmo pedido pode chegar
    // de novo. Se já existe pagamento registrado para ele, a mensagem é descartada: não reprocessa
    // (o resultado já decidido não muda) e, principalmente, não republica o PaymentProcessedEvent.
    public async Task ProcessarPedidoAsync(OrderPlacedEvent evento, CancellationToken cancellationToken = default)
    {
        var existente = await _pagamentos.ObterPorPedidoAsync(evento.OrderId, cancellationToken);

        if (existente is not null)
        {
            _logger.LogWarning(
                "OrderPlacedEvent descartado sem efeito: o pedido {OrderId} já tem pagamento registrado ({StatusDoPagamento}, processado em {ProcessadoEm:O}). " +
                "Nada foi reprocessado nem republicado. Provável reentrega de uma mensagem já processada.",
                existente.PedidoId, existente.Status, existente.ProcessadoEm);
            return;
        }

        var pagamento = Pagamento.Registrar(
            evento.OrderId, evento.UserId, evento.GameId, evento.Titulo, evento.Price, _politica);

        await _pagamentos.AdicionarAsync(pagamento, cancellationToken);

        // O evento é registrado antes do SalvarAsync de propósito: a infraestrutura grava pagamento e
        // evento na mesma transação (outbox). Se duas entregas do mesmo pedido correm juntas, a que perde
        // a corrida falha no índice único e leva o evento dela junto no rollback: nada é publicado duas vezes.
        await _eventos.PublicarAsync(
            new PaymentProcessedEvent(pagamento.PedidoId, pagamento.UsuarioId, pagamento.JogoId, pagamento.TituloJogo, ParaContrato(pagamento.Status)),
            cancellationToken);
        await _unitOfWork.SalvarAsync(cancellationToken);

        // Logado só depois de confirmado no banco.
        _logger.LogInformation(
            "Pagamento do pedido {OrderId} {StatusDoPagamento}: preço {Price} para o limite de {Limite} (jogo {GameId}, usuário {UserId})",
            pagamento.PedidoId, pagamento.Status, pagamento.Preco, _politica.LimiteDeAprovacao, pagamento.JogoId, pagamento.UsuarioId);
    }

    private static PaymentStatus ParaContrato(StatusPagamento status) => status switch
    {
        StatusPagamento.Aprovado => PaymentStatus.Approved,
        StatusPagamento.Recusado => PaymentStatus.Rejected,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Status de pagamento sem equivalente no contrato.")
    };

    private static PagamentoResponse ParaResponse(Pagamento pagamento) =>
        new(pagamento.Id, pagamento.PedidoId, pagamento.UsuarioId, pagamento.JogoId, pagamento.TituloJogo,
            pagamento.Preco, pagamento.Status.ToString(), pagamento.ProcessadoEm);
}
