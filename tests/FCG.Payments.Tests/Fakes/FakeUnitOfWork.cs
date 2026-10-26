using FCG.Payments.Application.Interfaces;

namespace FCG.Payments.Tests.Fakes;

public class FakeUnitOfWork : IUnitOfWork
{
    private readonly List<string> _chamadas;

    public FakeUnitOfWork(List<string>? chamadas = null)
    {
        _chamadas = chamadas ?? new List<string>();
    }

    public int Salvamentos { get; private set; }

    /// <summary>Quando definida, o SalvarAsync falha com ela (simula a corrida perdida no banco).</summary>
    public Exception? FalharAoSalvarCom { get; set; }

    public Task SalvarAsync(CancellationToken cancellationToken = default)
    {
        if (FalharAoSalvarCom is not null)
            throw FalharAoSalvarCom;

        Salvamentos++;
        _chamadas.Add("salvar");
        return Task.CompletedTask;
    }
}
