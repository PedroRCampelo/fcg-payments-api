namespace FCG.Contracts;

public sealed record OrderPlacedEvent(Guid OrderId, Guid UserId, Guid GameId, string Titulo, decimal Price);
