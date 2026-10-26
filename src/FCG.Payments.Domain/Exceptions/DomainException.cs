namespace FCG.Payments.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string mensagem) : base(mensagem)
    {
    }
}
