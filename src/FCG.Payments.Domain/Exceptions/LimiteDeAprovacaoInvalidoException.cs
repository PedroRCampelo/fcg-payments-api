namespace FCG.Payments.Domain.Exceptions;

public sealed class LimiteDeAprovacaoInvalidoException : DomainException
{
    public LimiteDeAprovacaoInvalidoException(string mensagem) : base(mensagem)
    {
    }
}
