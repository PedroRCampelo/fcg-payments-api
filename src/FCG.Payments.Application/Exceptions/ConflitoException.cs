namespace FCG.Payments.Application.Exceptions;

public sealed class ConflitoException : Exception
{
    public ConflitoException(string mensagem) : base(mensagem)
    {
    }
}
