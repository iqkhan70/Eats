using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Text;
using System.Text.Json;
using TraditionalEats.PaymentService.Services;

namespace TraditionalEats.PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class WebhooksController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhooksController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhooksController(
        IPaymentService paymentService,
        IConfiguration configuration,
        ILogger<WebhooksController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _paymentService = paymentService;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        var json = await ReadRequestBodyAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogWarning("Stripe:WebhookSecret is not set; skipping signature verification");
            return BadRequest(new { message = "Webhook not configured" });
        }
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Stripe-Signature header missing");
            return BadRequest(new { message = "Missing signature" });
        }

        Event? stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest(new { message = "Invalid signature" });
        }

        _logger.LogInformation("Stripe webhook received: {EventType}, Id={EventId}", stripeEvent.Type, stripeEvent.Id);

        try
        {
            switch (stripeEvent.Type)
            {
                case Events.AccountUpdated:
                    var account = stripeEvent.Data.Object as Account;
                    if (account != null)
                        await _paymentService.UpdateVendorOnboardingFromStripeAsync(account.Id);
                    break;
                case Events.CheckoutSessionCompleted:
                    var sessionCompleted = stripeEvent.Data.Object as Session;
                    if (sessionCompleted != null)
                    {
                        var (orderId, stripePiId) = ExtractOrderAndPaymentIntent(sessionCompleted);
                        if (orderId != null)
                        {
                            if (!string.IsNullOrEmpty(stripePiId))
                                await _paymentService.UpdatePaymentIntentStatusFromStripeAsync(stripePiId, "Authorized", null);
                            await UpdateOrderPaymentAsync(orderId.Value, "Succeeded", stripePiId, null);
                        }
                    }
                    break;
                case Events.CheckoutSessionExpired:
                    var sessionExpired = stripeEvent.Data.Object as Session;
                    if (sessionExpired != null)
                    {
                        var (orderId, stripePiId) = ExtractOrderAndPaymentIntent(sessionExpired);
                        if (orderId != null)
                        {
                            if (!string.IsNullOrEmpty(stripePiId))
                                await _paymentService.UpdatePaymentIntentStatusFromStripeAsync(stripePiId, "Failed", "Checkout session expired");
                            await UpdateOrderPaymentAsync(orderId.Value, "Failed", stripePiId, "Checkout session expired");
                        }
                    }
                    break;
                case Events.PaymentIntentSucceeded:
                    var piSucceeded = stripeEvent.Data.Object as PaymentIntent;
                    if (piSucceeded != null)
                        await _paymentService.UpdatePaymentIntentStatusFromStripeAsync(piSucceeded.Id, "Authorized", null);
                    break;
                case Events.PaymentIntentPaymentFailed:
                    var piFailed = stripeEvent.Data.Object as PaymentIntent;
                    if (piFailed != null)
                        await _paymentService.UpdatePaymentIntentStatusFromStripeAsync(piFailed.Id, "Failed", piFailed.LastPaymentError?.Message);
                    break;
                default:
                    _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook {EventType}", stripeEvent.Type);
            return StatusCode(500, new { message = "Webhook handler failed" });
        }

        return Ok();
    }

    private static (Guid? OrderId, string? StripePaymentIntentId) ExtractOrderAndPaymentIntent(Session session)
    {
        Guid? orderId = null;

        var orderIdRaw = session?.ClientReferenceId;
        if (string.IsNullOrWhiteSpace(orderIdRaw))
        {
            if (session?.Metadata != null && session.Metadata.TryGetValue("OrderId", out var metaOrderId))
                orderIdRaw = metaOrderId;
        }

        if (!string.IsNullOrWhiteSpace(orderIdRaw) && Guid.TryParse(orderIdRaw, out var parsed))
            orderId = parsed;

        var stripePiId = session?.PaymentIntentId;
        return (orderId, stripePiId);
    }

    private async Task UpdateOrderPaymentAsync(Guid orderId, string paymentStatus, string? stripePaymentIntentId, string? failureReason)
    {
        var orderBaseUrl = _configuration["Services:OrderService:BaseUrl"];
        if (string.IsNullOrWhiteSpace(orderBaseUrl))
            orderBaseUrl = "http://localhost:5002";

        var internalApiKey = _configuration["Services:OrderService:InternalApiKey"];

        using var http = _httpClientFactory.CreateClient();
        var url = $"{orderBaseUrl.TrimEnd('/')}/api/order/internal/{orderId}/payment";

        var payload = new
        {
            paymentStatus,
            stripePaymentIntentId,
            failureReason
        };
        var json = JsonSerializer.Serialize(payload);
        var req = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(internalApiKey))
            req.Headers.TryAddWithoutValidation("X-Internal-Api-Key", internalApiKey);

        var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            _logger.LogWarning("Order payment patch failed for OrderId={OrderId} (HTTP {StatusCode}): {Body}", orderId, res.StatusCode, body);
        }
    }

    private async Task<string> ReadRequestBodyAsync()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;
        return body;
    }

}
