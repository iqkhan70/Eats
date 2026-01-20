namespace TraditionEats.Contracts.Events;

public record PaymentAuthorizedEvent(
    Guid PaymentIntentId,
    Guid OrderId,
    decimal Amount,
    string Provider,
    string ProviderTransactionId,
    DateTime AuthorizedAt
);

public record PaymentFailedEvent(
    Guid PaymentIntentId,
    Guid OrderId,
    decimal Amount,
    string Provider,
    string FailureReason,
    DateTime FailedAt
);
