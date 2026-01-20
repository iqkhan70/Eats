using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraditionEats.PaymentService.Services;

namespace TraditionEats.PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("intent")]
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

    [HttpPost("refund")]
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

public record CreatePaymentIntentRequest(Guid OrderId, decimal Amount, string? Currency);
public record AuthorizePaymentRequest(Guid PaymentIntentId, string PaymentMethodId);
public record CapturePaymentRequest(Guid PaymentIntentId);
public record CreateRefundRequest(Guid PaymentIntentId, Guid OrderId, decimal Amount, string Reason);
