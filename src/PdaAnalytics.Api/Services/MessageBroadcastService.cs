using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Api.Dtos;
using PdaAnalytics.Api.Hubs;
using PdaAnalytics.Data;

namespace PdaAnalytics.Api.Services;

/// <summary>
/// Фоновый сервис, который поллит PostgreSQL на наличие новых сообщений
/// и транслирует их через SignalR Hub.
/// 
/// Работает независимо от Syncer — просто проверяет, появились ли новые
/// записи в messages_denormalized с id > lastBroadcastedId.
/// </summary>
public class MessageBroadcastService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PdaHub> _hubContext;
    private readonly ILogger<MessageBroadcastService> _logger;

    private long _lastBroadcastedId;

    public MessageBroadcastService(
        IServiceScopeFactory scopeFactory,
        IHubContext<PdaHub> hubContext,
        ILogger<MessageBroadcastService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageBroadcastService запущен");

        // Инициализация: узнаём текущий максимальный ID
        await Task.Delay(2000, stoppingToken);
        await InitializeLastIdAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndBroadcastAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в MessageBroadcastService");
            }

            try
            {
                await Task.Delay(3000, stoppingToken); // Poll каждые 3 секунды
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("MessageBroadcastService остановлен");
    }

    private async Task InitializeLastIdAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        _lastBroadcastedId = await db.Messages.MaxAsync(m => (long?)m.Id, ct) ?? 0;
        _logger.LogInformation("MessageBroadcast: инициализация, lastId = {LastId}", _lastBroadcastedId);
    }

    private async Task PollAndBroadcastAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

        var newMessages = await db.Messages
            .Where(m => m.Id > _lastBroadcastedId)
            .OrderBy(m => m.Id)
            .Take(100) // Максимум 100 за раз
            .ToListAsync(ct);

        if (newMessages.Count == 0) return;

        foreach (var msg in newMessages)
        {
            var dto = new MessageDto
            {
                Id = msg.Id,
                SourceMessageId = msg.SourceMessageId,
                ChatType = msg.ChatType.ToString(),
                ChatName = msg.ChatName,
                SenderLogin = msg.SenderLogin,
                SenderSteamId = msg.SenderSteamId,
                SenderNickname = msg.SenderNickname,
                ReceiverLogin = msg.ReceiverLogin,
                ReceiverSteamId = msg.ReceiverSteamId,
                ReceiverNickname = msg.ReceiverNickname,
                Message = msg.Message,
                Attachments = msg.Attachments,
                SentAt = msg.SentAt,
                SourceInstance = msg.SourceInstance
            };

            // Broadcast to all connected clients
            await _hubContext.Clients.All.SendAsync("NewMessage", dto, ct);

            // Broadcast to instance-specific group
            await _hubContext.Clients.Group($"instance:{msg.SourceInstance}")
                .SendAsync("NewMessage", dto, ct);

            // Broadcast to player-specific groups
            if (msg.SenderSteamId != null)
            {
                await _hubContext.Clients.Group($"player:{msg.SenderSteamId}")
                    .SendAsync("NewMessage", dto, ct);
            }

            if (msg.ReceiverSteamId != null)
            {
                await _hubContext.Clients.Group($"player:{msg.ReceiverSteamId}")
                    .SendAsync("NewMessage", dto, ct);
            }
        }

        _lastBroadcastedId = newMessages.Max(m => m.Id);
        _logger.LogDebug("SignalR: транслировано {Count} новых сообщений (lastId → {LastId})",
            newMessages.Count, _lastBroadcastedId);
    }
}
