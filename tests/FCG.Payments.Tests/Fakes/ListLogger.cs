using Microsoft.Extensions.Logging;

namespace FCG.Payments.Tests.Fakes;

public class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Mensagem)> Registros { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Registros.Add((logLevel, formatter(state, exception)));
}
