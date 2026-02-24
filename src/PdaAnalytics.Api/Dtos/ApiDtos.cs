namespace PdaAnalytics.Api.Dtos;

// ═══════════════════════════════════════════════════════════════════
//  PLAYER
// ═══════════════════════════════════════════════════════════════════

/// <summary> Карточка игрока (профиль). </summary>
public record PlayerProfileDto
{
    public required string SteamId { get; init; }
    public string? Nickname { get; init; }
    public DateTime RegistrationDate { get; init; }
    public DateTime LastLogonDate { get; init; }
    public required string SourceInstance { get; init; }
    
    // ── Steam Profile ──
    public string? SteamAvatarUrl { get; init; }
    public string? SteamProfileUrl { get; init; }
    public string? SteamPersonaName { get; init; }
    public string? SteamRealName { get; init; }
    public string? SteamCountryCode { get; init; }
    public int? SteamPersonaState { get; init; }  // 0=Offline, 1=Online, 2=Busy, 3=Away, 4=Snooze, 5=Looking to trade, 6=Looking to play
    
    /// <summary> Все PDA-аккаунты игрока (мультиаккаунты). </summary>
    public List<PdaAccountDto> PdaAccounts { get; init; } = [];
    
    /// <summary> Членство в фракциях. </summary>
    public List<FactionMembershipDto> Factions { get; init; } = [];
    
    /// <summary> Статистика: сколько сообщений отправил. </summary>
    public int TotalMessagesSent { get; init; }
    
    /// <summary> Статистика: сколько сообщений получил. </summary>
    public int TotalMessagesReceived { get; init; }
    
    /// <summary> Уникальные собеседники (SteamID → Nickname). </summary>
    public List<ContactDto> Contacts { get; init; } = [];
}

public record PdaAccountDto
{
    public int SourceId { get; init; }
    public required string Login { get; init; }
    public DateTime LastActivity { get; init; }
    public required string SourceInstance { get; init; }
}

public record FactionMembershipDto
{
    public required string FactionName { get; init; }
    public long FactionColor { get; init; }
    public string? FactionIcon { get; init; }
    public int RankId { get; init; }
    public required string SourceInstance { get; init; }
}

public record ContactDto
{
    public required string SteamId { get; init; }
    public string? Nickname { get; init; }
    public int MessageCount { get; init; }
    public DateTime LastMessageAt { get; init; }
}

// ═══════════════════════════════════════════════════════════════════
//  MESSAGES
// ═══════════════════════════════════════════════════════════════════

/// <summary> Сообщение в ленте / истории. </summary>
public record MessageDto
{
    public long Id { get; init; }
    public int SourceMessageId { get; init; }
    public string ChatType { get; init; } = "Global";
    public string? ChatName { get; init; }
    
    public string? SenderLogin { get; init; }
    public string? SenderSteamId { get; init; }
    public string? SenderNickname { get; init; }
    
    public string? ReceiverLogin { get; init; }
    public string? ReceiverSteamId { get; init; }
    public string? ReceiverNickname { get; init; }
    
    public string Message { get; init; } = string.Empty;
    public string? Attachments { get; init; }
    public DateTime SentAt { get; init; }
    public required string SourceInstance { get; init; }
}

// ═══════════════════════════════════════════════════════════════════
//  SEARCH
// ═══════════════════════════════════════════════════════════════════

/// <summary> Результат Omni-Search. </summary>
public record SearchResultDto
{
    public List<PlayerSearchHit> Players { get; init; } = [];
    public List<PdaAccountSearchHit> PdaAccounts { get; init; } = [];
    public List<MessageSearchHit> Messages { get; init; } = [];
}

public record PlayerSearchHit
{
    public required string SteamId { get; init; }
    public string? Nickname { get; init; }
    public required string SourceInstance { get; init; }
}

public record PdaAccountSearchHit
{
    public int SourceId { get; init; }
    public required string Login { get; init; }
    public required string SteamId { get; init; }
    public required string SourceInstance { get; init; }
}

public record MessageSearchHit
{
    public long Id { get; init; }
    public string? SenderLogin { get; init; }
    public string? SenderSteamId { get; init; }
    public string? ReceiverLogin { get; init; }
    public string? ReceiverSteamId { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
    public required string SourceInstance { get; init; }
}

// ═══════════════════════════════════════════════════════════════════
//  PAGINATION & COMMON
// ═══════════════════════════════════════════════════════════════════

public record PagedResult<T>
{
    public List<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore => Page * PageSize < TotalCount;
}

/// <summary> Статистика для дашборда. </summary>
public record DashboardStatsDto
{
    public int TotalPlayers { get; init; }
    public int TotalPdaAccounts { get; init; }
    public int TotalMessages { get; init; }
    public int TotalPrivateMessages { get; init; }
    public int TotalGlobalMessages { get; init; }
    public int TotalFactions { get; init; }
    public int TotalInstances { get; init; }
    public DateTime? LastMessageAt { get; init; }
}
