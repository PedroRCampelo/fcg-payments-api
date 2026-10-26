using FCG.Payments.Application.Dtos;
using FCG.Payments.Application.Services;

namespace FCG.Payments.Api.Endpoints;

public static class PagamentoEndpoints
{
    public static IEndpointRouteBuilder MapPagamentoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/pagamentos")
            .WithTags("Pagamentos")
            .RequireAuthorization(Policies.Administrador);

        grupo.MapGet("/", async (PagamentoService pagamentoService, CancellationToken cancellationToken) =>
                Results.Ok(await pagamentoService.ListarAsync(cancellationToken)))
            .WithName("ListarPagamentos")
            .WithSummary("Lista os pagamentos processados")
            .WithDescription("Requer role Administrador. Cada pedido tem no máximo um pagamento. " +
                             "Status: Aprovado (preço dentro do limite configurado) ou Recusado (preço acima do limite).")
            .Produces<IReadOnlyList<PagamentoResponse>>(StatusCodes.Status200OK);

        return app;
    }
}
