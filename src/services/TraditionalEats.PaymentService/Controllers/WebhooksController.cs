using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
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

    public WebhooksController(
        IPaymentService paymentService,
        IConfiguration configuration,
        ILogger<WebhooksController> logger)
    {
        _paymentService = paymentService;
        _configuration = configuration;
        _logger = logger;
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

    private async Task<string> ReadRequestBodyAsync()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;
        return body;
    }

}
