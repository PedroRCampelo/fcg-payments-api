namespace FCG.Contracts;

public sealed record PaymentProcessedEvent(Guid OrderId, Guid UserId, Guid GameId, string Titulo, PaymentStatus Status);
