using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace sh_backend_core
{
    public class OutboxBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxBackgroundWorker> _logger;

        public OutboxBackgroundWorker(IServiceProvider serviceProvider, ILogger<OutboxBackgroundWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong Outbox Background Worker.");
                }

                // Delay 5 seconds
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            // Query unprocessed messages
            var messages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(stoppingToken);

            if (messages.Count > 0)
            {
                // Required log output format
                Console.WriteLine("[Outbox Worker] Phát hiện tin nhắn Outbox chưa xử lý. Tiến hành đẩy sự kiện lên RabbitMQ và đóng gói Transaction.");

                foreach (var message in messages)
                {
                    try
                    {
                        if (message.EventType == nameof(EmployeeCreatedEvent))
                        {
                            var ev = JsonSerializer.Deserialize<EmployeeCreatedEvent>(message.Content);
                            if (ev != null)
                            {
                                await publishEndpoint.Publish(ev, stoppingToken);
                            }
                        }

                        // Mark message as processed
                        message.ProcessedAt = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Không thể đẩy outbox message với ID: {message.Id} lên RabbitMQ.");
                    }
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
