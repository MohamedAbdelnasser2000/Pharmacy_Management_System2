using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IMS.Services
{
    public class ExpiryAlertBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiryAlertBackgroundService> _logger;

        public ExpiryAlertBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ExpiryAlertBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RunCycleAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await RunCycleAsync(stoppingToken);
            }
        }

        private async Task RunCycleAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ExpiryAlertService>();
                var count = await service.GenerateAlertsAsync(stoppingToken);

                if (count > 0)
                {
                    _logger.LogInformation("Generated {Count} expiry alerts.", count);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while generating expiry alerts.");
            }
        }
    }
}
