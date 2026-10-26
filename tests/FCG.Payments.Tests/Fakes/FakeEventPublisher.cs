using FCG.Payments.Application.Interfaces;

namespace FCG.Payments.Tests.Fakes;

public class FakeEventPublisher : IEventPublisher
{
    private readonly List<string> _chamadas;

    public FakeEventPublisher(List<string>? chamadas = null)
    {
        _chamadas = chamadas ?? new List<string>();
    }

    public List<object> Publicados { get; } = new();

    public Task PublicarAsync<TEvento>(TEvento evento, CancellationToken cancellationToken = default)
        where TEvento : class
    {
        Publicados.Add(evento);
        _chamadas.Add("publicar");
        return Task.CompletedTask;
    }
}
