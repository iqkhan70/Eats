using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using TraditionalEats.BuildingBlocks.Messaging;
using TraditionalEats.Contracts.Events;
using TraditionalEats.PaymentService.Data;
using TraditionalEats.PaymentService.Entities;

namespace TraditionalEats.PaymentService.Services;

public interface IPaymentService
{
    // Stripe Connect (vendor onboarding)
    Task<string> CreateVendorConnectLinkAsync(Guid userId);
    Task<(string? StripeAccountId, string Status)> GetVendorOnboardingStatusAsync(Guid userId);
    Task UpdateVendorOnboardingFromStripeAsync(string stripeAccountId);
    Task<bool> IsVendorPaymentReadyAsync(Guid restaurantId);

    Task<Guid> CreatePaymentIntentAsync(Guid orderId, decimal amount, string currency = "USD");
    Task<bool> AuthorizePaymentAsync(Guid paymentIntentId, string paymentMethodId);
    Task<bool> CapturePaymentAsync(Guid paymentIntentId);
    Task<bool> CapturePaymentByOrderIdAsync(Guid orderId);
    /// <summary>Void authorization (cancel Stripe PaymentIntent) by order id. For customer cancellations before capture.</summary>
    Task<bool> CancelPaymentByOrderIdAsync(Guid orderId);
    Task<Guid> CreateRefundAsync(Guid paymentIntentId, Guid orderId, decimal amount, string reason);
    /// <summary>
    /// Refund by order id. If the payment was never captured, this will void the authorization instead (Stripe PaymentIntent cancel).
    /// </summary>
    Task<RefundByOrderResult> RefundOrVoidPaymentByOrderIdAsync(Guid orderId, string? reason = null);
    /// <summary>Called from Stripe webhook: update our PaymentIntent by Stripe PI id.</summary>
    Task UpdatePaymentIntentStatusFromStripeAsync(string stripePaymentIntentId, string status, string? failureReason);
    /// <summary>Create Stripe Checkout Session (destination charge + app fee + manual capture). Returns checkout URL.</summary>
    Task<string> CreateCheckoutSessionAsync(Guid orderId, decimal amount, decimal serviceFee, Guid restaurantId, string successUrl, string cancelUrl);
}

public record RefundByOrderResult(string Action, Guid? RefundId, string Message);

public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _context;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<PaymentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public PaymentService(
        PaymentDbContext context,
        IMessagePublisher messagePublisher,
        ILogger<PaymentService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _messagePublisher = messagePublisher;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;

        // Initialize Stripe
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    public async Task<string> CreateVendorConnectLinkAsync(Guid userId)
    {
        var secretKey = _configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("Stripe:SecretKey is not set in configuration");
            throw new InvalidOperationException("Stripe is not configured. Please set Stripe:SecretKey.");
        }
        StripeConfiguration.ApiKey = secretKey;

        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null)
        {
            vendor = new Vendor
            {
                VendorId = Guid.NewGuid(),
                UserId = userId,
                StripeOnboardingStatus = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Vendors.Add(vendor);
        }

        string? stripeAccountId = vendor.StripeAccountId;
        if (string.IsNullOrEmpty(stripeAccountId))
        {
            var accountService = new AccountService();
            var accountOptions = new AccountCreateOptions
            {
                Type = "standard",
                Country = "US"
            };
            var account = await accountService.CreateAsync(accountOptions);
            stripeAccountId = account.Id;
            vendor.StripeAccountId = stripeAccountId;
            vendor.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created Stripe connected account {StripeAccountId} for vendor UserId={UserId}", stripeAccountId, userId);
        }

        // Get base URL for Stripe Connect return URLs
        var baseUrl = _configuration["Stripe:ConnectReturnUrl"] ?? _configuration["AppBaseUrl"] ?? "https://localhost:5301";

        // Ensure URL has protocol (default to https for production)
        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // If no protocol, assume HTTPS for production (non-localhost domains)
            if (!baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) &&
                !baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "https://" + baseUrl;
            }
            else
            {
                baseUrl = "https://" + baseUrl;
            }
        }

        baseUrl = baseUrl.TrimEnd('/');
        var returnUrl = baseUrl + "/vendor?stripe=return";
        var refreshUrl = baseUrl + "/vendor?stripe=refresh";

        _logger.LogInformation("Creating Stripe Connect link with returnUrl={ReturnUrl}, refreshUrl={RefreshUrl}", returnUrl, refreshUrl);

        var linkService = new AccountLinkService();
        var linkOptions = new AccountLinkCreateOptions
        {
            Account = stripeAccountId,
            Type = "account_onboarding",
            ReturnUrl = returnUrl,
            RefreshUrl = refreshUrl
        };
        var link = await linkService.CreateAsync(linkOptions);
        _logger.LogInformation("Created account link for vendor UserId={UserId}, StripeAccountId={StripeAccountId}", userId, stripeAccountId);
        return link.Url;
    }

    public async Task<(string? StripeAccountId, string Status)> GetVendorOnboardingStatusAsync(Guid userId)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null)
            return (null, "Pending");
        return (vendor.StripeAccountId, vendor.StripeOnboardingStatus);
    }

    public async Task<bool> IsVendorPaymentReadyAsync(Guid restaurantId)
    {
        try
        {
            var restaurantBaseUrl = _configuration["Services:RestaurantService:BaseUrl"] ?? "http://localhost:5007";
            var restaurantUrl = $"{restaurantBaseUrl.TrimEnd('/')}/api/restaurant/{restaurantId}";

            using var http = _httpClientFactory.CreateClient();
            var restaurantResponse = await http.GetAsync(restaurantUrl);
            if (!restaurantResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Restaurant {RestaurantId} not found when checking vendor payment status", restaurantId);
                return false;
            }

            var restaurantJson = await restaurantResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(restaurantJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ownerId", out var ownerIdEl))
                root.TryGetProperty("OwnerId", out ownerIdEl);
            if (!ownerIdEl.TryGetGuid(out Guid ownerId))
            {
                _logger.LogWarning("Restaurant {RestaurantId} has no owner", restaurantId);
                return false;
            }

            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == ownerId);
            if (vendor == null || string.IsNullOrEmpty(vendor.StripeAccountId))
            {
                _logger.LogInformation("Vendor not connected for restaurant {RestaurantId} owner {OwnerId}", restaurantId, ownerId);
                return false;
            }
            if (vendor.StripeOnboardingStatus != "Complete")
            {
                _logger.LogInformation("Vendor Stripe onboarding incomplete for restaurant {RestaurantId}, status={Status}", restaurantId, vendor.StripeOnboardingStatus);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking vendor payment status for restaurant {RestaurantId}", restaurantId);
            return false;
        }
    }

    public async Task UpdateVendorOnboardingFromStripeAsync(string stripeAccountId)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.StripeAccountId == stripeAccountId);
        if (vendor == null)
        {
            _logger.LogWarning("Vendor not found for StripeAccountId={StripeAccountId}", stripeAccountId);
            return;
        }
        var accountService = new AccountService();
        var account = await accountService.GetAsync(stripeAccountId);
        var status = (account.ChargesEnabled == true && account.PayoutsEnabled == true) ? "Complete"
            : (account.DetailsSubmitted == true ? "Restricted" : "Pending");
        vendor.StripeOnboardingStatus = status;
        vendor.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated vendor StripeAccountId={StripeAccountId} onboarding status to {Status}", stripeAccountId, status);
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

    public async Task<bool> CapturePaymentByOrderIdAsync(Guid orderId)
    {
        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(pi => pi.OrderId == orderId && (pi.Status == "Authorized" || pi.Status == "Pending"));

        if (paymentIntent == null)
        {
            _logger.LogWarning("No authorizable payment intent found for OrderId={OrderId}", orderId);
            return false;
        }
        return await CapturePaymentAsync(paymentIntent.PaymentIntentId);
    }

    public async Task UpdatePaymentIntentStatusFromStripeAsync(string stripePaymentIntentId, string status, string? failureReason)
    {
        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(pi => pi.ProviderIntentId == stripePaymentIntentId);
        if (paymentIntent == null)
        {
            _logger.LogWarning("PaymentIntent not found for Stripe PI id={StripeId}", stripePaymentIntentId);
            return;
        }
        paymentIntent.Status = status;
        if (failureReason != null)
            paymentIntent.FailureReason = failureReason;
        if (status == "Authorized")
            paymentIntent.AuthorizedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated PaymentIntent {PaymentIntentId} status to {Status} from webhook", paymentIntent.PaymentIntentId, status);
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid orderId, decimal amount, decimal serviceFee, Guid restaurantId, string successUrl, string cancelUrl)
    {
        var restaurantBaseUrl = _configuration["Services:RestaurantService:BaseUrl"] ?? "http://localhost:5007";
        var restaurantUrl = $"{restaurantBaseUrl.TrimEnd('/')}/api/restaurant/{restaurantId}";
        _logger.LogInformation("CreateCheckoutSessionAsync: Fetching restaurant {RestaurantId} from {RestaurantUrl}", restaurantId, restaurantUrl);

        using var http = _httpClientFactory.CreateClient();
        HttpResponseMessage restaurantResponse;
        try
        {
            restaurantResponse = await http.GetAsync(restaurantUrl);
            if (!restaurantResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Restaurant {RestaurantId} not found when creating checkout. Status={Status}, Url={Url}",
                    restaurantId, restaurantResponse.StatusCode, restaurantUrl);
                throw new InvalidOperationException($"Restaurant not found (HTTP {restaurantResponse.StatusCode})");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to RestaurantService at {RestaurantUrl}. Check that Services:RestaurantService:BaseUrl is configured correctly.", restaurantUrl);
            throw new InvalidOperationException($"Failed to connect to RestaurantService: {ex.Message}", ex);
        }

        var restaurantJson = await restaurantResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(restaurantJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("ownerId", out var ownerIdEl))
            root.TryGetProperty("OwnerId", out ownerIdEl);
        if (!ownerIdEl.TryGetGuid(out Guid ownerId))
            throw new InvalidOperationException("Restaurant has no owner");
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == ownerId);
        if (vendor == null || string.IsNullOrEmpty(vendor.StripeAccountId))
        {
            _logger.LogWarning("Vendor not connected for restaurant {RestaurantId} owner {OwnerId}", restaurantId, ownerId);
            throw new InvalidOperationException("This restaurant is not set up to accept payments yet. Please contact the restaurant directly or try again later.");
        }
        if (vendor.StripeOnboardingStatus != "Complete")
        {
            _logger.LogWarning("Vendor Stripe onboarding incomplete for restaurant {RestaurantId}", restaurantId);
            throw new InvalidOperationException("This restaurant's payment setup is not complete yet. Please contact the restaurant directly or try again later.");
        }
        var amountCents = (long)Math.Round(amount * 100);
        var serviceFeeCents = (long)Math.Round(serviceFee * 100);
        var sessionService = new SessionService();
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = orderId.ToString(),
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = amountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Order {orderId:N}".Substring(0, 24),
                            Description = "TraditionalEats order"
                        }
                    },
                    Quantity = 1
                }
            },
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                //CaptureMethod = "manual",
                ApplicationFeeAmount = serviceFeeCents,
                TransferData = new SessionPaymentIntentDataTransferDataOptions
                {
                    Destination = vendor.StripeAccountId
                },
                Metadata = new Dictionary<string, string> { { "OrderId", orderId.ToString() } }
            }
        };
        options.Expand = new List<string> { "payment_intent" };
        var session = await sessionService.CreateAsync(options);
        var stripePiId = session.PaymentIntentId ?? (session.PaymentIntent as Stripe.PaymentIntent)?.Id;
        if (!string.IsNullOrEmpty(stripePiId))
        {
            var ourPiId = Guid.NewGuid();
            _context.PaymentIntents.Add(new Entities.PaymentIntent
            {
                PaymentIntentId = ourPiId,
                OrderId = orderId,
                Amount = amount,
                ServiceFee = serviceFee,
                Currency = "USD",
                Status = "Pending",
                Provider = "Stripe",
                ProviderIntentId = stripePiId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created PaymentIntent {PaymentIntentId} for OrderId={OrderId}, Stripe PI={StripeId}", ourPiId, orderId, stripePiId);
        }
        if (string.IsNullOrEmpty(session.Url))
            throw new InvalidOperationException("Stripe did not return a checkout URL");
        return session.Url;
    }

    public async Task<Guid> CreateRefundAsync(Guid paymentIntentId, Guid orderId, decimal amount, string reason)
    {
        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(pi => pi.PaymentIntentId == paymentIntentId);

        if (paymentIntent == null)
            throw new InvalidOperationException("Payment intent not found");
        if (string.IsNullOrEmpty(paymentIntent.ProviderTransactionId) && string.IsNullOrEmpty(paymentIntent.ProviderIntentId))
            throw new InvalidOperationException("Payment intent not found or missing provider identifiers");

        var refundId = Guid.NewGuid();

        try
        {
            var service = new RefundService();
            var reasonNormalized = NormalizeStripeRefundReason(reason);
            var options = new RefundCreateOptions();
            if (!string.IsNullOrEmpty(paymentIntent.ProviderTransactionId))
                options.Charge = paymentIntent.ProviderTransactionId;
            else
                options.PaymentIntent = paymentIntent.ProviderIntentId;
            options.Amount = ToCents(amount);
            if (!string.IsNullOrEmpty(reasonNormalized))
                options.Reason = reasonNormalized;

            var stripeRefund = await service.CreateAsync(options, new RequestOptions
            {
                IdempotencyKey = $"refund_{paymentIntent.ProviderIntentId ?? paymentIntent.ProviderTransactionId}_{amount:0.00}"
            });

            var refund = new Entities.Refund
            {
                RefundId = refundId,
                PaymentIntentId = paymentIntentId,
                OrderId = orderId,
                Amount = amount,
                Reason = reasonNormalized ?? "requested_by_customer",
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

    public async Task<RefundByOrderResult> RefundOrVoidPaymentByOrderIdAsync(Guid orderId, string? reason = null)
    {
        var pi = await _context.PaymentIntents
            .Where(p => p.OrderId == orderId && p.Provider == "Stripe" && p.ProviderIntentId != null)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (pi == null || string.IsNullOrWhiteSpace(pi.ProviderIntentId))
            return new RefundByOrderResult("not_found", null, "No payment intent found for this order.");

        // Enforce policy: refund only for Completed orders (best-effort check via OrderService if configured).
        // This prevents accidental voiding/refunding of in-progress orders via direct API calls.
        try
        {
            var orderBaseUrl = _configuration["Services:OrderService:BaseUrl"];
            if (string.IsNullOrWhiteSpace(orderBaseUrl))
                orderBaseUrl = "http://localhost:5002";

            var internalApiKey = _configuration["Services:OrderService:InternalApiKey"];
            using var http = _httpClientFactory.CreateClient();
            var req = new HttpRequestMessage(HttpMethod.Get, $"{orderBaseUrl.TrimEnd('/')}/api/order/internal/{orderId}/status");
            if (!string.IsNullOrWhiteSpace(internalApiKey))
                req.Headers.TryAddWithoutValidation("X-Internal-Api-Key", internalApiKey);
            var res = await http.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
                // Allow refund for Completed (vendor delivered) or Cancelled (customer/vendor cancelled - must refund to avoid orphan payment)
                if (!string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return new RefundByOrderResult("not_refundable", null, "Refunds are allowed only for Completed or Cancelled orders.");
            }
            else
            {
                _logger.LogWarning("OrderService status check failed for OrderId={OrderId} (HTTP {StatusCode}); falling back to payment status policy.",
                    orderId, res.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OrderService status check failed for OrderId={OrderId}; falling back to payment status policy.", orderId);
        }

        // If already refunded, return existing refund (idempotent)
        if (string.Equals(pi.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _context.Refunds
                .Where(r => r.OrderId == orderId && r.PaymentIntentId == pi.PaymentIntentId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
            return new RefundByOrderResult("already_refunded", existing?.RefundId, "Payment is already refunded.");
        }

        // Refunds apply when payment was captured (in Stripe). With auto-capture checkout, we store "Authorized"
        // but Stripe has already captured—so allow refund for both Authorized and Captured.
        if (!string.Equals(pi.Status, "Captured", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(pi.Status, "Authorized", StringComparison.OrdinalIgnoreCase))
        {
            return new RefundByOrderResult("not_refundable", null, $"Refund not applicable because payment is not captured (status '{pi.Status}').");
        }

        // If a refund already exists for this PI + order, return it (idempotent)
        var existingRefund = await _context.Refunds
            .Where(r => r.OrderId == orderId && r.PaymentIntentId == pi.PaymentIntentId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
        if (existingRefund != null)
        {
            var action = string.Equals(existingRefund.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                ? "refunded"
                : "refund_pending";
            return new RefundByOrderResult(action, existingRefund.RefundId, "Refund already exists for this order.");
        }

        var reasonNormalized = NormalizeStripeRefundReason(reason) ?? "requested_by_customer";
        var refundId = Guid.NewGuid();

        try
        {
            var service = new RefundService();
            var options = new RefundCreateOptions
            {
                PaymentIntent = pi.ProviderIntentId,
                Amount = ToCents(pi.Amount),
                Reason = reasonNormalized,
                // For destination charges + application fee: unwind vendor transfer and platform fee on refund.
                ReverseTransfer = true,
                RefundApplicationFee = true
            };

            var stripeRefund = await service.CreateAsync(options, new RequestOptions
            {
                IdempotencyKey = $"refund_by_order_{orderId}_{pi.ProviderIntentId}"
            });

            var refund = new Entities.Refund
            {
                RefundId = refundId,
                PaymentIntentId = pi.PaymentIntentId,
                OrderId = orderId,
                Amount = pi.Amount,
                Reason = reasonNormalized,
                Status = stripeRefund.Status == "succeeded" ? "Completed" : "Pending",
                ProviderRefundId = stripeRefund.Id,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = stripeRefund.Status == "succeeded" ? DateTime.UtcNow : null
            };
            _context.Refunds.Add(refund);

            if (refund.Status == "Completed")
                pi.Status = "Refunded";

            await _context.SaveChangesAsync();

            var @event = new RefundIssuedEvent(
                refundId,
                orderId,
                pi.PaymentIntentId,
                pi.Amount,
                reasonNormalized,
                DateTime.UtcNow
            );
            await _messagePublisher.PublishAsync("tradition-eats", "refund.issued", @event);

            var action = refund.Status == "Completed" ? "refunded" : "refund_pending";
            return new RefundByOrderResult(action, refundId, "Refund created.");
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe refund failed for OrderId={OrderId}, Stripe PI={StripePiId}", orderId, pi.ProviderIntentId);
            pi.FailureReason = ex.Message;
            try { await _context.SaveChangesAsync(); } catch { /* ignore */ }
            return new RefundByOrderResult("refund_failed", null, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefundOrVoidPaymentByOrderIdAsync failed for OrderId={OrderId}", orderId);
            pi.FailureReason = ex.Message;
            try { await _context.SaveChangesAsync(); } catch { /* ignore */ }
            return new RefundByOrderResult("refund_failed", null, ex.Message);
        }
    }

    public async Task<bool> CancelPaymentByOrderIdAsync(Guid orderId)
    {
        // Find latest Stripe PI for this order
        var pi = await _context.PaymentIntents
            .Where(p => p.OrderId == orderId && p.Provider == "Stripe" && p.ProviderIntentId != null)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (pi == null || string.IsNullOrWhiteSpace(pi.ProviderIntentId))
            return false;

        // If already captured/refunded, cancellation isn't applicable
        if (string.Equals(pi.Status, "Captured", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pi.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
            return false;

        // If already cancelled, idempotent success
        if (string.Equals(pi.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pi.Status, "Canceled", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var service = new PaymentIntentService();
            var options = new PaymentIntentCancelOptions
            {
                CancellationReason = "requested_by_customer"
            };

            var stripePi = await service.CancelAsync(pi.ProviderIntentId, options);

            pi.Status = string.Equals(stripePi.Status, "canceled", StringComparison.OrdinalIgnoreCase)
                ? "Cancelled"
                : (stripePi.Status ?? "Cancelled");

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cancelled Stripe PaymentIntent {StripePiId} for OrderId={OrderId} (our PI={PaymentIntentId})",
                pi.ProviderIntentId, orderId, pi.PaymentIntentId);

            return true;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe cancel failed for OrderId={OrderId}, Stripe PI={StripePiId}", orderId, pi.ProviderIntentId);
            pi.FailureReason = ex.Message;
            try { await _context.SaveChangesAsync(); } catch { /* ignore */ }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CancelPaymentByOrderIdAsync failed for OrderId={OrderId}", orderId);
            pi.FailureReason = ex.Message;
            try { await _context.SaveChangesAsync(); } catch { /* ignore */ }
            return false;
        }
    }

    private static string? NormalizeStripeRefundReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;
        var v = reason.Trim().ToLowerInvariant();
        return v switch
        {
            "duplicate" => "duplicate",
            "fraudulent" => "fraudulent",
            "requested_by_customer" => "requested_by_customer",
            _ => null
        };
    }

    private static long ToCents(decimal amount)
    {
        // Stripe expects integer cents. Round to nearest cent.
        var cents = Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        return (long)cents;
    }
}
