using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TraditionalEats.PaymentService.Services;

namespace TraditionalEats.PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger, IConfiguration configuration, IWebHostEnvironment env)
    {
        _paymentService = paymentService;
        _logger = logger;
        _configuration = configuration;
        _env = env;
    }

    // ----- Stripe Connect (vendor onboarding) -----
    [HttpPost("vendor/connect-link")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CreateVendorConnectLink()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "User ID claim is missing" });
        try
        {
            var url = await _paymentService.CreateVendorConnectLinkAsync(userId);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Vendor connect link: configuration or validation error");
            return BadRequest(new { message = ex.Message });
        }
        catch (Stripe.StripeException ex)
        {
            var stripeMessage = ex.Message;
            var stripeCode = ex.StripeError?.Code;
            _logger.LogError(ex, "Stripe API error creating vendor connect link: {StripeError}", stripeMessage);
            return BadRequest(new { message = "Stripe error: " + stripeMessage, stripeErrorCode = stripeCode });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating vendor connect link");
            var msg = _env.IsDevelopment()
                ? "Database error: " + (ex.InnerException?.Message ?? ex.Message) + ". Ensure MySQL is running, database exists, and migrations are applied."
                : "Database error. Please try again or contact support.";
            return StatusCode(500, new { message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create vendor connect link");
            var msg = "Failed to create Stripe connect link.";
            if (_env.IsDevelopment())
                msg += " " + (ex.InnerException?.Message ?? ex.Message);
            return StatusCode(500, new { message = msg });
        }
    }

    [HttpGet("vendor/onboarding-status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorOnboardingStatus()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "User ID claim is missing" });
        try
        {
            var (stripeAccountId, status) = await _paymentService.GetVendorOnboardingStatusAsync(userId);
            return Ok(new { stripeAccountId, status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vendor onboarding status");
            return StatusCode(500, new { message = "Failed to get onboarding status" });
        }
    }

    /// <summary>
    /// Sync onboarding status from Stripe (details_submitted, charges_enabled, payouts_enabled) and return updated status.
    /// Call this after the user returns from Stripe Connect onboarding (return or refresh URL) so the app reflects completion
    /// even if the account.updated webhook was missed (e.g. wrong mode or webhook not configured for Connect).
    /// </summary>
    [HttpPost("vendor/refresh-onboarding-status")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> RefreshVendorOnboardingStatus()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "User ID claim is missing" });
        try
        {
            var (stripeAccountId, _) = await _paymentService.GetVendorOnboardingStatusAsync(userId);
            if (string.IsNullOrEmpty(stripeAccountId))
                return BadRequest(new { message = "No Stripe account linked. Use 'Finish Stripe setup' to start onboarding." });
            await _paymentService.UpdateVendorOnboardingFromStripeAsync(stripeAccountId);
            var (_, status) = await _paymentService.GetVendorOnboardingStatusAsync(userId);
            return Ok(new { stripeAccountId, status });
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe API error refreshing onboarding for user {UserId}: {Message}", userId, ex.Message);
            return BadRequest(new { message = "Stripe error: " + ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh vendor onboarding status");
            return StatusCode(500, new { message = "Failed to refresh onboarding status" });
        }
    }

    [HttpGet("restaurant/{restaurantId}/payment-ready")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckRestaurantPaymentReady(Guid restaurantId)
    {
        try
        {
            var isReady = await _paymentService.IsVendorPaymentReadyAsync(restaurantId);
            return Ok(new { restaurantId, paymentReady = isReady });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check restaurant payment readiness");
            return StatusCode(500, new { message = "Failed to check payment readiness" });
        }
    }

    // ----- Checkout (Stripe Checkout Session: destination charge + app fee + manual capture) -----
    [HttpPost("checkout/session")]
    [Authorize]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required" });
        try
        {
            var url = await _paymentService.CreateCheckoutSessionAsync(
                request.OrderId,
                request.Amount,
                request.ServiceFee,
                request.RestaurantId,
                request.SuccessUrl,
                request.CancelUrl);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("not connected") || ex.Message.Contains("incomplete"))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkout session");
            return StatusCode(500, new { message = "Failed to create checkout session" });
        }
    }

    // ----- Payment intents / checkout (authorize) -----
    [HttpPost("intent")]
    [Authorize]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
    {
        try
        {
            var paymentIntentId = await _paymentService.CreatePaymentIntentAsync(
                request.OrderId,
                request.Amount,
                request.Currency ?? "USD");

            return Ok(new { paymentIntentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create payment intent");
            return StatusCode(500, new { message = "Failed to create payment intent" });
        }
    }

    [HttpPost("authorize")]
    [Authorize]
    public async Task<IActionResult> AuthorizePayment([FromBody] AuthorizePaymentRequest request)
    {
        try
        {
            var success = await _paymentService.AuthorizePaymentAsync(
                request.PaymentIntentId,
                request.PaymentMethodId);

            if (!success)
            {
                return BadRequest(new { message = "Payment authorization failed" });
            }

            return Ok(new { message = "Payment authorized successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authorize payment");
            return StatusCode(500, new { message = "Failed to authorize payment" });
        }
    }

    [HttpPost("capture")]
    public async Task<IActionResult> CapturePayment([FromBody] CapturePaymentRequest request)
    {
        try
        {
            var success = await _paymentService.CapturePaymentAsync(request.PaymentIntentId);

            if (!success)
            {
                return BadRequest(new { message = "Payment capture failed" });
            }

            return Ok(new { message = "Payment captured successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture payment");
            return StatusCode(500, new { message = "Failed to capture payment" });
        }
    }

    /// <summary>Internal: capture payment by order id (e.g. when order status becomes Completed). Called by OrderService.</summary>
    [HttpPost("internal/capture-by-order")]
    [AllowAnonymous]
    public async Task<IActionResult> InternalCapturePaymentByOrder([FromBody] CapturePaymentByOrderRequest request, [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey = null)
    {
        var expectedKey = _configuration["InternalApiKey"] ?? _configuration["Services:PaymentService:InternalApiKey"];
        if (!string.IsNullOrEmpty(expectedKey) && apiKey != expectedKey)
        {
            _logger.LogWarning("Internal capture-by-order rejected: missing or invalid API key");
            return Unauthorized(new { message = "Invalid or missing internal API key" });
        }
        return await DoCapturePaymentByOrder(request);
    }

    [HttpPost("capture-by-order")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> CapturePaymentByOrder([FromBody] CapturePaymentByOrderRequest request) => await DoCapturePaymentByOrder(request);

    private async Task<IActionResult> DoCapturePaymentByOrder(CapturePaymentByOrderRequest request)
    {
        try
        {
            var success = await _paymentService.CapturePaymentByOrderIdAsync(request.OrderId);
            if (!success)
                return BadRequest(new { message = "No authorizable payment found for this order or capture failed" });
            return Ok(new { message = "Payment captured successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture payment by order {OrderId}", request.OrderId);
            return StatusCode(500, new { message = "Failed to capture payment" });
        }
    }

    [HttpPost("refund")]
    [Authorize]
    public async Task<IActionResult> CreateRefund([FromBody] CreateRefundRequest request)
    {
        try
        {
            var refundId = await _paymentService.CreateRefundAsync(
                request.PaymentIntentId,
                request.OrderId,
                request.Amount,
                request.Reason);

            return Ok(new { refundId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create refund");
            return StatusCode(500, new { message = "Failed to create refund" });
        }
    }
}

public record CreateCheckoutSessionRequest(Guid OrderId, decimal Amount, decimal ServiceFee, Guid RestaurantId, string SuccessUrl, string CancelUrl);
public record CreatePaymentIntentRequest(Guid OrderId, decimal Amount, string? Currency);
public record AuthorizePaymentRequest(Guid PaymentIntentId, string PaymentMethodId);
public record CapturePaymentRequest(Guid PaymentIntentId);
public record CapturePaymentByOrderRequest(Guid OrderId);
public record CreateRefundRequest(Guid PaymentIntentId, Guid OrderId, decimal Amount, string Reason);
