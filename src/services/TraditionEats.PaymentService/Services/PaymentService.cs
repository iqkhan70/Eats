using Microsoft.EntityFrameworkCore;
using Stripe;
using TraditionEats.BuildingBlocks.Messaging;
using TraditionEats.Contracts.Events;
using TraditionEats.PaymentService.Data;
using TraditionEats.PaymentService.Entities;

namespace TraditionEats.PaymentService.Services;

public interface IPaymentService
{
    Task<Guid> CreatePaymentIntentAsync(Guid orderId, decimal amount, string currency = "USD");
    Task<bool> AuthorizePaymentAsync(Guid paymentIntentId, string paymentMethodId);
    Task<bool> CapturePaymentAsync(Guid paymentIntentId);
    Task<Guid> CreateRefundAsync(Guid paymentIntentId, Guid orderId, decimal amount, string reason);
}

public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _context;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<PaymentService> _logger;
    private readonly IConfiguration _configuration;

    public PaymentService(
        PaymentDbContext context,
        IMessagePublisher messagePublisher,
        ILogger<PaymentService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _messagePublisher = messagePublisher;
        _logger = logger;
        _configuration = configuration;

        // Initialize Stripe
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    public async Task<Guid> CreatePaymentIntentAsync(Guid orderId, decimal amount, string currency = "USD")
    {
        var paymentIntentId = Guid.NewGuid();

        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Convert to cents
                Currency = currency.ToLower(),
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
                Metadata = new Dictionary<string, string>
                {
                    { "OrderId", orderId.ToString() },
                    { "PaymentIntentId", paymentIntentId.ToString() }
                }
            };

            var service = new PaymentIntentService();
            var stripeIntent = await service.CreateAsync(options);

            var paymentIntent = new Entities.PaymentIntent
            {
                PaymentIntentId = paymentIntentId,
                OrderId = orderId,
                Amount = amount,
                Currency = currency,
                Status = "Pending",
                Provider = "Stripe",
                ProviderIntentId = stripeIntent.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentIntents.Add(paymentIntent);
            await _context.SaveChangesAsync();

            return paymentIntentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment intent for order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<bool> AuthorizePaymentAsync(Guid paymentIntentId, string paymentMethodId)
    {
        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(pi => pi.PaymentIntentId == paymentIntentId);

        if (paymentIntent == null || string.IsNullOrEmpty(paymentIntent.ProviderIntentId))
            return false;

        try
        {
            var service = new PaymentIntentService();
            var options = new PaymentIntentConfirmOptions
            {
                PaymentMethod = paymentMethodId
            };

            var stripeIntent = await service.ConfirmAsync(paymentIntent.ProviderIntentId, options);

            paymentIntent.Status = stripeIntent.Status == "succeeded" ? "Authorized" : "Failed";
            paymentIntent.ProviderTransactionId = stripeIntent.LatestChargeId;
            paymentIntent.AuthorizedAt = DateTime.UtcNow;

            if (paymentIntent.Status == "Failed")
            {
                paymentIntent.FailureReason = stripeIntent.LastPaymentError?.Message;
            }

            await _context.SaveChangesAsync();

            // Publish event
            if (paymentIntent.Status == "Authorized")
            {
                var @event = new PaymentAuthorizedEvent(
                    paymentIntentId,
                    paymentIntent.OrderId,
                    paymentIntent.Amount,
                    paymentIntent.Provider,
                    paymentIntent.ProviderTransactionId ?? string.Empty,
                    paymentIntent.AuthorizedAt.Value
                );
                await _messagePublisher.PublishAsync("tradition-eats", "payment.authorized", @event);
            }
            else
            {
                var @event = new PaymentFailedEvent(
                    paymentIntentId,
                    paymentIntent.OrderId,
                    paymentIntent.Amount,
                    paymentIntent.Provider,
                    paymentIntent.FailureReason ?? "Unknown error",
                    DateTime.UtcNow
                );
                await _messagePublisher.PublishAsync("tradition-eats", "payment.failed", @event);
            }

            return paymentIntent.Status == "Authorized";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authorize payment {PaymentIntentId}", paymentIntentId);
            paymentIntent.Status = "Failed";
            paymentIntent.FailureReason = ex.Message;
            await _context.SaveChangesAsync();
            return false;
        }
    }

    public async Task<bool> CapturePaymentAsync(Guid paymentIntentId)
    {
        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(pi => pi.PaymentIntentId == paymentIntentId);

        if (paymentIntent == null || string.IsNullOrEmpty(paymentIntent.ProviderIntentId))
            return false;

        try
        {
            var service = new PaymentIntentService();
            var stripeIntent = await service.CaptureAsync(paymentIntent.ProviderIntentId);

            paymentIntent.Status = "Captured";
            paymentIntent.CapturedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture payment {PaymentIntentId}", paymentIntentId);
            return false;
        }
    }

    public async Task<Guid> CreateRefundAsync(Guid paymentIntentId, Guid orderId, decimal amount, string reason)
    {
        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(pi => pi.PaymentIntentId == paymentIntentId);

        if (paymentIntent == null || string.IsNullOrEmpty(paymentIntent.ProviderTransactionId))
            throw new InvalidOperationException("Payment intent not found or not authorized");

        var refundId = Guid.NewGuid();

        try
        {
            var service = new RefundService();
            var options = new RefundCreateOptions
            {
                Charge = paymentIntent.ProviderTransactionId,
                Amount = (long)(amount * 100), // Convert to cents
                Reason = reason
            };

            var stripeRefund = await service.CreateAsync(options);

            var refund = new Entities.Refund
            {
                RefundId = refundId,
                PaymentIntentId = paymentIntentId,
                OrderId = orderId,
                Amount = amount,
                Reason = reason,
                Status = stripeRefund.Status == "succeeded" ? "Completed" : "Pending",
                ProviderRefundId = stripeRefund.Id,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = stripeRefund.Status == "succeeded" ? DateTime.UtcNow : null
            };

            _context.Refunds.Add(refund);

            if (refund.Status == "Completed")
            {
                paymentIntent.Status = "Refunded";
            }

            await _context.SaveChangesAsync();

            // Publish event
            var @event = new RefundIssuedEvent(
                refundId,
                orderId,
                paymentIntentId,
                amount,
                reason,
                DateTime.UtcNow
            );
            await _messagePublisher.PublishAsync("tradition-eats", "refund.issued", @event);

            return refundId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create refund for payment {PaymentIntentId}", paymentIntentId);
            throw;
        }
    }
}
