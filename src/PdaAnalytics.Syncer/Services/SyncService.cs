using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Entities;
using PdaAnalytics.Syncer.MariaDb;

namespace PdaAnalytics.Syncer.Services;

/// <summary>
/// Основной ETL-сервис. Выполняет инкрементальную синхронизацию
/// одного инстанса MariaDB → PostgreSQL.
/// </summary>
public class SyncService
{
    private readonly ILogger<SyncService> _logger;

    /// <summary>
    /// MariaDB хранит время в московском часовом поясе (UTC+3).
    /// Все даты конвертируются в UTC при синхронизации.
    /// </summary>
    private static readonly TimeZoneInfo MoscowTz =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow");

    private static DateTime MoscowToUtc(DateTime moscowTime)
    {
        var unspecified = DateTime.SpecifyKind(moscowTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, MoscowTz);
    }

    public SyncService(ILogger<SyncService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Полный цикл синхронизации для одного инстанса.
    /// Возвращает список новых денормализированных сообщений (для SignalR push).
    /// </summary>
    public async Task<List<MessageDenormalized>> SyncInstanceAsync(
        string instanceName,
        string mariaConnectionString,
        AnalyticsDbContext db,
        int messageBatchSize,
        CancellationToken ct)
    {
        await using var mariaConn = new MySqlConnection(mariaConnectionString);
        await mariaConn.OpenAsync(ct);

        _logger.LogInformation("[{Instance}] Начало синхронизации...", instanceName);

        // 1. Справочники (полная перезапись — данные небольшие)
        await SyncPlayersAsync(mariaConn, db, instanceName, ct);
        await SyncPdaAccountsAsync(mariaConn, db, instanceName, ct);
        await SyncFactionsAsync(mariaConn, db, instanceName, ct);
        await SyncFactionMembersAsync(mariaConn, db, instanceName, ct);
        await SyncChatsAsync(mariaConn, db, instanceName, ct);
        await SyncChatParticipantsAsync(mariaConn, db, instanceName, ct);

        // 2. Сообщения (инкрементально по ID)
        var newMessages = await SyncMessagesAsync(mariaConn, db, instanceName, messageBatchSize, ct);

        _logger.LogInformation("[{Instance}] Синхронизация завершена. Новых сообщений: {Count}",
            instanceName, newMessages.Count);

        return newMessages;
    }

    // ═══════════════════════════════════════════════════════════════
    //  PLAYERS
    // ═══════════════════════════════════════════════════════════════

    private async Task SyncPlayersAsync(
        MySqlConnection maria, AnalyticsDbContext db, string instance, CancellationToken ct)
    {
        var mariaPlayers = (await maria.QueryAsync<MariaPlayer>(
            "SELECT SteamID, Nickname, RegistrationDate, LastLogonDate FROM players"))
            .ToList();

        if (mariaPlayers.Count == 0) return;

        // Получаем существующие записи
        var existingSteamIds = await db.Players
            .Where(p => p.SourceInstance == instance)
            .Select(p => p.SteamId)
            .ToHashSetAsync(ct);

        var now = DateTime.UtcNow;
        var toAdd = new List<Player>();
        var toUpdate = new List<MariaPlayer>();

        foreach (var mp in mariaPlayers)
        {
            if (existingSteamIds.Contains(mp.SteamID))
                toUpdate.Add(mp);
            else
                toAdd.Add(new Player
                {
                    SteamId = mp.SteamID,
                    Nickname = mp.Nickname,
                    RegistrationDate = MoscowToUtc(mp.RegistrationDate),
                    LastLogonDate = MoscowToUtc(mp.LastLogonDate),
                    SourceInstance = instance,
                    SyncedAt = now
                });
        }

        if (toAdd.Count > 0)
        {
            db.Players.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("[{Instance}] Players: добавлено {Count}", instance, toAdd.Count);
        }

        // Update — batch через raw SQL для производительности
        if (toUpdate.Count > 0)
        {
            foreach (var mp in toUpdate)
            {
                await db.Players
                    .Where(p => p.SteamId == mp.SteamID && p.SourceInstance == instance)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Nickname, mp.Nickname)
                        .SetProperty(p => p.LastLogonDate, MoscowToUtc(mp.LastLogonDate))
                        .SetProperty(p => p.SyncedAt, now), ct);
            }
            _logger.LogDebug("[{Instance}] Players: обновлено {Count}", instance, toUpdate.Count);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PDA ACCOUNTS
    // ═══════════════════════════════════════════════════════════════

    private async Task SyncPdaAccountsAsync(
        MySqlConnection maria, AnalyticsDbContext db, string instance, CancellationToken ct)
    {
        var mariaAccounts = (await maria.QueryAsync<MariaPdaAccount>(
            "SELECT ID, Login, SteamID, LastActivity FROM pda_accounts"))
            .ToList();

        if (mariaAccounts.Count == 0) return;

        var existingIds = await db.PdaAccounts
            .Where(a => a.SourceInstance == instance)
            .Select(a => a.SourceId)
            .ToHashSetAsync(ct);

        var now = DateTime.UtcNow;
        var toAdd = new List<PdaAccount>();

        foreach (var ma in mariaAccounts)
        {
            if (!existingIds.Contains(ma.ID))
            {
                toAdd.Add(new PdaAccount
                {
                    SourceId = ma.ID,
                    Login = ma.Login,
                    SteamId = ma.SteamID,
                    LastActivity = MoscowToUtc(ma.LastActivity),
                    SourceInstance = instance,
                    SyncedAt = now
                });
            }
            else
            {
                // Update login/activity
                await db.PdaAccounts
                    .Where(a => a.SourceId == ma.ID && a.SourceInstance == instance)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.Login, ma.Login)
                        .SetProperty(a => a.SteamId, ma.SteamID)
                        .SetProperty(a => a.LastActivity, MoscowToUtc(ma.LastActivity))
                        .SetProperty(a => a.SyncedAt, now), ct);
            }
        }

        if (toAdd.Count > 0)
        {
            db.PdaAccounts.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("[{Instance}] PdaAccounts: добавлено {Count}", instance, toAdd.Count);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  FACTIONS
    // ═══════════════════════════════════════════════════════════════

    private async Task SyncFactionsAsync(
        MySqlConnection maria, AnalyticsDbContext db, string instance, CancellationToken ct)
    {
        var mariaFactions = (await maria.QueryAsync<MariaFaction>(
            "SELECT ID, Name, Color, Icon FROM factions"))
            .ToList();

        if (mariaFactions.Count == 0) return;

        var existingIds = await db.Factions
            .Where(f => f.SourceInstance == instance)
            .Select(f => f.SourceId)
            .ToHashSetAsync(ct);

        var now = DateTime.UtcNow;
        var toAdd = new List<Faction>();

        foreach (var mf in mariaFactions)
        {
            if (!existingIds.Contains(mf.ID))
            {
                toAdd.Add(new Faction
                {
                    SourceId = mf.ID,
                    Name = mf.Name,
                    Color = mf.Color,
                    Icon = mf.Icon,
                    SourceInstance = instance,
                    SyncedAt = now
                });
            }
            else
            {
                await db.Factions
                    .Where(f => f.SourceId == mf.ID && f.SourceInstance == instance)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(f => f.Name, mf.Name)
                        .SetProperty(f => f.Color, mf.Color)
                        .SetProperty(f => f.Icon, mf.Icon)
                        .SetProperty(f => f.SyncedAt, now), ct);
            }
        }

        if (toAdd.Count > 0)
        {
            db.Factions.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("[{Instance}] Factions: добавлено {Count}", instance, toAdd.Count);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  FACTION MEMBERS
    // ═══════════════════════════════════════════════════════════════

    private async Task SyncFactionMembersAsync(
        MySqlConnection maria, AnalyticsDbContext db, string instance, CancellationToken ct)
    {
        var mariaMembers = (await maria.QueryAsync<MariaFactionMember>(
            "SELECT FACTION_ID, RankID, MEMBER_ID FROM faction_members"))
            .ToList();

        if (mariaMembers.Count == 0) return;

        // Полная перезапись — данных мало, а FK связи могут меняться
        var existingMemberships = await db.FactionMemberships
            .Where(fm => fm.SourceInstance == instance)
            .ToListAsync(ct);

        var existingSet = existingMemberships
            .ToHashSet(new FactionMembershipComparer());

        var now = DateTime.UtcNow;
        var toAdd = new List<FactionMembership>();

        foreach (var mm in mariaMembers)
        {
            var key = new FactionMembership
            {
                FactionSourceId = mm.FACTION_ID,
                MemberSteamId = mm.MEMBER_ID,
                SourceInstance = instance
            };

            if (!existingSet.Contains(key))
            {
                toAdd.Add(new FactionMembership
                {
                    FactionSourceId = mm.FACTION_ID,
                    RankId = mm.RankID,
                    MemberSteamId = mm.MEMBER_ID,
                    SourceInstance = instance,
                    SyncedAt = now
                });
            }
        }

        if (toAdd.Count > 0)
        {
            db.FactionMemberships.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("[{Instance}] FactionMembers: добавлено {Count}", instance, toAdd.Count);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  CHATS
    // ═══════════════════════════════════════════════════════════════

    private async Task SyncChatsAsync(
        MySqlConnection maria, AnalyticsDbContext db, string instance, CancellationToken ct)
    {
        var mariaChats = (await maria.QueryAsync<MariaChat>(
            "SELECT ID, Name, Type FROM pda_chats"))
            .ToList();

        if (mariaChats.Count == 0) return;

        var existingIds = await db.Chats
            .Where(c => c.SourceInstance == instance)
            .Select(c => c.SourceId)
            .ToHashSetAsync(ct);

        var now = DateTime.UtcNow;
        var toAdd = new List<Chat>();

        foreach (var mc in mariaChats)
        {
            var chatType = mc.Type.Equals("PRIVATE", StringComparison.OrdinalIgnoreCase)
                ? ChatType.Private
                : ChatType.Global;

            if (!existingIds.Contains(mc.ID))
            {
                toAdd.Add(new Chat
                {
                    SourceId = mc.ID,
                    Name = mc.Name,
                    Type = chatType,
                    SourceInstance = instance,
                    SyncedAt = now
                });
            }
            else
            {
                await db.Chats
                    .Where(c => c.SourceId == mc.ID && c.SourceInstance == instance)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Name, mc.Name)
                        .SetProperty(c => c.Type, chatType)
                        .SetProperty(c => c.SyncedAt, now), ct);
            }
        }

        if (toAdd.Count > 0)
        {
            db.Chats.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("[{Instance}] Chats: добавлено {Count}", instance, toAdd.Count);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  CHAT PARTICIPANTS
    // ═══════════════════════════════════════════════════════════════

    private async Task SyncChatParticipantsAsync(
        MySqlConnection maria, AnalyticsDbContext db, string instance, CancellationToken ct)
    {
        var mariaParticipants = (await maria.QueryAsync<MariaChatParticipant>(
            "SELECT ChatID, AccountID, ContactAccountID FROM pda_chat_participants"))
            .ToList();

        if (mariaParticipants.Count == 0) return;

        var existingSet = (await db.ChatParticipants
            .Where(cp => cp.SourceInstance == instance)
            .Select(cp => new { cp.ChatSourceId, cp.AccountSourceId })
            .ToListAsync(ct))
            .ToHashSet();

        var now = DateTime.UtcNow;
        var toAdd = new List<ChatParticipant>();

        foreach (var mp in mariaParticipants)
        {
            var key = new { ChatSourceId = mp.ChatID, AccountSourceId = mp.AccountID };
            if (!existingSet.Contains(key))
            {
                toAdd.Add(new ChatParticipant
                {
                    ChatSourceId = mp.ChatID,
                    AccountSourceId = mp.AccountID,
                    ContactAccountSourceId = mp.ContactAccountID,
                    SourceInstance = instance,
                    SyncedAt = now
                });
            }
        }

        if (toAdd.Count > 0)
        {
            db.ChatParticipants.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("[{Instance}] ChatParticipants: добавлено {Count}", instance, toAdd.Count);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  MESSAGES (INCREMENTAL + DENORMALIZATION) — ⭐ Главная логика
    // ═══════════════════════════════════════════════════════════════

    private async Task<List<MessageDenormalized>> SyncMessagesAsync(
        MySqlConnection maria, AnalyticsDbContext db, string instance,
        int batchSize, CancellationToken ct)
    {
        // 1. Узнаём, где мы остановились
        var syncState = await db.SyncStates
            .FirstOrDefaultAsync(s => s.InstanceName == instance && s.TableName == "pda_chat_messages", ct);

        long lastId = syncState?.LastSyncedId ?? 0;

        // 2. Читаем новые сообщения из MariaDB
        var mariaMessages = (await maria.QueryAsync<MariaChatMessage>(
            """
            SELECT ID, ChatID, SenderID, Message, Attachments, Date 
            FROM pda_chat_messages 
            WHERE ID > @LastId 
            ORDER BY ID ASC 
            LIMIT @BatchSize
            """,
            new { LastId = lastId, BatchSize = batchSize }))
            .ToList();

        if (mariaMessages.Count == 0) return [];

        // 3. Предзагрузка справочников для денормализации
        //    (из Postgres — данные уже синхронизированы выше)
        var accountLookup = await db.PdaAccounts
            .Where(a => a.SourceInstance == instance)
            .ToDictionaryAsync(a => a.SourceId, ct);

        var playerLookup = await db.Players
            .Where(p => p.SourceInstance == instance)
            .ToDictionaryAsync(p => p.SteamId, ct);

        var chatLookup = await db.Chats
            .Where(c => c.SourceInstance == instance)
            .ToDictionaryAsync(c => c.SourceId, ct);

        // Участники чатов — для определения получателя в PRIVATE чатах
        // Группировка: ChatSourceId → List<ChatParticipant>
        var participantsByChat = (await db.ChatParticipants
            .Where(cp => cp.SourceInstance == instance)
            .ToListAsync(ct))
            .GroupBy(cp => cp.ChatSourceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 4. Денормализация и создание записей
        var newMessages = new List<MessageDenormalized>();
        var now = DateTime.UtcNow;

        foreach (var mm in mariaMessages)
        {
            var denorm = new MessageDenormalized
            {
                SourceMessageId = mm.ID,
                SourceChatId = mm.ChatID,
                SenderAccountId = mm.SenderID,
                Message = mm.Message,
                Attachments = mm.Attachments,
                SentAt = MoscowToUtc(mm.Date),
                SourceInstance = instance,
                SyncedAt = now
            };

            // ── Определяем тип чата и имя ──
            if (chatLookup.TryGetValue(mm.ChatID, out var chat))
            {
                denorm.ChatType = chat.Type;
                denorm.ChatName = chat.Name;
            }
            else
            {
                denorm.ChatType = ChatType.Global; // fallback
            }

            // ── Sender info ──
            if (accountLookup.TryGetValue(mm.SenderID, out var senderAccount))
            {
                denorm.SenderLogin = senderAccount.Login;
                denorm.SenderSteamId = senderAccount.SteamId;

                if (playerLookup.TryGetValue(senderAccount.SteamId, out var senderPlayer))
                {
                    denorm.SenderNickname = senderPlayer.Nickname;
                }
            }

            // ── Receiver info (только для PRIVATE чатов) ──
            if (denorm.ChatType == ChatType.Private)
            {
                DenormalizeReceiver(denorm, mm, participantsByChat, accountLookup, playerLookup);
            }

            newMessages.Add(denorm);
        }

        // 5. Bulk-вставка в Postgres
        db.Messages.AddRange(newMessages);
        await db.SaveChangesAsync(ct);

        // 6. Обновляем sync state
        var maxSyncedId = mariaMessages.Max(m => m.ID);
        if (syncState == null)
        {
            db.SyncStates.Add(new SyncState
            {
                InstanceName = instance,
                TableName = "pda_chat_messages",
                LastSyncedId = maxSyncedId,
                UpdatedAt = now
            });
        }
        else
        {
            syncState.LastSyncedId = maxSyncedId;
            syncState.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[{Instance}] Messages: синхронизировано {Count} (lastId: {LastId} → {NewLastId})",
            instance, newMessages.Count, lastId, maxSyncedId);

        return newMessages;
    }

    /// <summary>
    /// Определяет receiver для PRIVATE чата.
    /// 
    /// Логика:
    /// В приватном чате ровно 2 участника (pda_chat_participants).  
    /// Каждый участник имеет поле ContactAccountID, указывающее на собеседника.
    /// Sender = mm.SenderID (AccountID). Receiver = другой участник чата.
    /// 
    /// Алгоритм:
    /// 1. Берём всех участников чата mm.ChatID
    /// 2. Находим участника, у которого AccountID == mm.SenderID
    /// 3. Его ContactAccountID — и есть получатель
    ///    (альтернативно: находим участника с AccountID != mm.SenderID)
    /// </summary>
    private static void DenormalizeReceiver(
        MessageDenormalized denorm,
        MariaChatMessage mm,
        Dictionary<int, List<ChatParticipant>> participantsByChat,
        Dictionary<int, PdaAccount> accountLookup,
        Dictionary<string, Player> playerLookup)
    {
        if (!participantsByChat.TryGetValue(mm.ChatID, out var participants))
            return;

        // Способ 1: через ContactAccountID отправителя
        var senderParticipant = participants.FirstOrDefault(p => p.AccountSourceId == mm.SenderID);
        int? receiverAccountId = null;

        if (senderParticipant != null && senderParticipant.ContactAccountSourceId > 0)
        {
            receiverAccountId = senderParticipant.ContactAccountSourceId;
        }
        else
        {
            // Способ 2: fallback — другой участник чата (для двух участников)
            var otherParticipant = participants.FirstOrDefault(p => p.AccountSourceId != mm.SenderID);
            if (otherParticipant != null)
            {
                receiverAccountId = otherParticipant.AccountSourceId;
            }
        }

        if (receiverAccountId == null) return;

        denorm.ReceiverAccountId = receiverAccountId.Value;

        if (accountLookup.TryGetValue(receiverAccountId.Value, out var receiverAccount))
        {
            denorm.ReceiverLogin = receiverAccount.Login;
            denorm.ReceiverSteamId = receiverAccount.SteamId;

            if (playerLookup.TryGetValue(receiverAccount.SteamId, out var receiverPlayer))
            {
                denorm.ReceiverNickname = receiverPlayer.Nickname;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private class FactionMembershipComparer : IEqualityComparer<FactionMembership>
    {
        public bool Equals(FactionMembership? x, FactionMembership? y) =>
            x?.FactionSourceId == y?.FactionSourceId &&
            x?.MemberSteamId == y?.MemberSteamId &&
            x?.SourceInstance == y?.SourceInstance;

        public int GetHashCode(FactionMembership obj) =>
            HashCode.Combine(obj.FactionSourceId, obj.MemberSteamId, obj.SourceInstance);
    }
}
