namespace FCG.Payments.Domain.Exceptions;

// Dados do pedido que não permitem processar um pagamento (id vazio, preço negativo, título ausente...).
// Reenviar o mesmo evento não conserta, então o consumidor não retenta.
public sealed class PedidoInvalidoException : DomainException
{
    public PedidoInvalidoException(string mensagem) : base(mensagem)
    {
    }
}
