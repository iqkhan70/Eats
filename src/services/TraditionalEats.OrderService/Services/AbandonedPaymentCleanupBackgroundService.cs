using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TraditionalEats.OrderService.Services;

/// <summary>
/// Periodically cancels orders stuck in PaymentPending (abandoned Stripe checkout).
/// Runs every 10 minutes and cancels orders older than 30 minutes.
/// </summary>
public class AbandonedPaymentCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AbandonedPaymentCleanupBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AbandonedThreshold = TimeSpan.FromMinutes(30);

    public AbandonedPaymentCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AbandonedPaymentCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AbandonedPaymentCleanupBackgroundService started. Will run every {Interval} minutes.", Interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                var count = await orderService.CancelAbandonedPaymentPendingOrdersAsync(AbandonedThreshold);
                if (count > 0)
                    _logger.LogInformation("AbandonedPaymentCleanup: Cancelled {Count} abandoned payment-pending order(s)", count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AbandonedPaymentCleanup: Error during cleanup run");
            }
        }

        _logger.LogInformation("AbandonedPaymentCleanupBackgroundService stopped");
    }
}
