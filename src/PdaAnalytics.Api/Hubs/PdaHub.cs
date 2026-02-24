using Microsoft.AspNetCore.SignalR;
using PdaAnalytics.Api.Dtos;

namespace PdaAnalytics.Api.Hubs;

/// <summary>
/// SignalR Hub для real-time трансляции PDA-событий.
/// Клиент подключается к /hubs/pda и получает события:
/// - "NewMessage" — новое сообщение (MessageDto)
/// - "StatsUpdate" — обновление статистики
/// </summary>
public class PdaHub : Hub
{
    private readonly ILogger<PdaHub> _logger;

    public PdaHub(ILogger<PdaHub> logger) => _logger = logger;

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR: клиент подключён {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR: клиент отключён {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Клиент может подписаться на конкретный инстанс.
    /// </summary>
    public async Task SubscribeToInstance(string instanceName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"instance:{instanceName}");
        _logger.LogDebug("SignalR: {ConnectionId} подписан на {Instance}", Context.ConnectionId, instanceName);
    }

    /// <summary>
    /// Клиент может подписаться на сообщения конкретного игрока.
    /// </summary>
    public async Task SubscribeToPlayer(string steamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"player:{steamId}");
        _logger.LogDebug("SignalR: {ConnectionId} подписан на игрока {SteamId}", Context.ConnectionId, steamId);
    }

    /// <summary>
    /// Отписаться от конкретного инстанса.
    /// </summary>
    public async Task UnsubscribeFromInstance(string instanceName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"instance:{instanceName}");
    }

    /// <summary>
    /// Отписаться от конкретного игрока.
    /// </summary>
    public async Task UnsubscribeFromPlayer(string steamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"player:{steamId}");
    }
}
