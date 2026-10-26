namespace FCG.Payments.Application.Interfaces;

public interface IUnitOfWork
{
    Task SalvarAsync(CancellationToken cancellationToken = default);
}
