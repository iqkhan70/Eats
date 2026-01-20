namespace TraditionEats.Contracts.Events;

public record RefundIssuedEvent(
    Guid RefundId,
    Guid OrderId,
    Guid PaymentIntentId,
    decimal Amount,
    string Reason,
    DateTime IssuedAt
);
