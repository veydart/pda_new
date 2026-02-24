namespace PdaAnalytics.Syncer.MariaDb;

/// <summary>
/// DTO для MariaDB таблицы `players`.
/// Маппинг через Dapper — имена свойств совпадают с колонками.
/// </summary>
public class MariaPlayer
{
    public string SteamID { get; set; } = null!;
    public string? Nickname { get; set; }
    public DateTime RegistrationDate { get; set; }
    public DateTime LastLogonDate { get; set; }
}

/// <summary>
/// DTO для MariaDB таблицы `pda_accounts`.
/// </summary>
public class MariaPdaAccount
{
    public int ID { get; set; }
    public string Login { get; set; } = null!;
    public string SteamID { get; set; } = null!;
    public DateTime LastActivity { get; set; }
}

/// <summary>
/// DTO для MariaDB таблицы `pda_chats`.
/// </summary>
public class MariaChat
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public string Type { get; set; } = "GLOBAL";
}

/// <summary>
/// DTO для MariaDB таблицы `pda_chat_participants`.
/// </summary>
public class MariaChatParticipant
{
    public int ChatID { get; set; }
    public int AccountID { get; set; }
    public int ContactAccountID { get; set; }
}

/// <summary>
/// DTO для MariaDB таблицы `pda_chat_messages`.
/// </summary>
public class MariaChatMessage
{
    public int ID { get; set; }
    public int ChatID { get; set; }
    public int SenderID { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Attachments { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>
/// DTO для MariaDB таблицы `factions`.
/// </summary>
public class MariaFaction
{
    public int ID { get; set; }
    public string Name { get; set; } = null!;
    public long Color { get; set; }
    public string? Icon { get; set; }
}

/// <summary>
/// DTO для MariaDB таблицы `faction_members`.
/// </summary>
public class MariaFactionMember
{
    public int FACTION_ID { get; set; }
    public int RankID { get; set; }
    public string MEMBER_ID { get; set; } = null!;
}
