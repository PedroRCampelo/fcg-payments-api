using Microsoft.Extensions.Logging;

namespace FCG.Payments.Tests.Fakes;

// Thread-safe: o harness do MassTransit consome mensagens em paralelo, e uma List comum quebraria
// ("Collection was modified") quando o teste lê os registros enquanto os consumidores ainda escrevem.
public class ListLogger<T> : ILogger<T>
{
    private readonly object _trava = new();
    private readonly List<(LogLevel Nivel, string Mensagem)> _registros = new();

    /// <summary>Cópia dos registros no momento da leitura (seguro para enumerar).</summary>
    public IReadOnlyList<(LogLevel Nivel, string Mensagem)> Registros
    {
        get { lock (_trava) return _registros.ToList(); }
    }

    public void Limpar()
    {
        lock (_trava) _registros.Clear();
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_trava) _registros.Add((logLevel, formatter(state, exception)));
    }
}
