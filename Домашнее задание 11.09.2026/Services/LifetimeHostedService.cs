using Microsoft.Extensions.Hosting;

namespace Домашнее_задание_11._09._2026.Services
{
    public class LifetimeHostedService : IHostedService
    {
        private readonly ILogger<LifetimeHostedService> _logger;

        public LifetimeHostedService(
            ILogger<LifetimeHostedService> logger,
            AppLifetimeService lifetimeService)  
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[HOSTED] StartAsync — приложение запускается");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[HOSTED] StopAsync — приложение завершает работу");
            return Task.CompletedTask;
        }
    }
}
