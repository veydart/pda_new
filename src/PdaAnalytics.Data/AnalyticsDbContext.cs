using Microsoft.EntityFrameworkCore;
using PdaAnalytics.Domain.Entities;

namespace PdaAnalytics.Data;

public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<PdaAccount> PdaAccounts => Set<PdaAccount>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<MessageDenormalized> Messages => Set<MessageDenormalized>();
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<FactionMembership> FactionMemberships => Set<FactionMembership>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<WebUser> WebUsers => Set<WebUser>();
    public DbSet<ServerSession> ServerSessions => Set<ServerSession>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<ChatWebhookConfig> ChatWebhookConfigs => Set<ChatWebhookConfig>();
    public DbSet<FactionMentionConfig> FactionMentionConfigs => Set<FactionMentionConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Player ───────────────────────────────────────────────
        modelBuilder.Entity<Player>(e =>
        {
            e.ToTable("players");
            e.HasKey(p => new { p.SteamId, p.SourceInstance });
            e.Property(p => p.SteamId).HasMaxLength(50).HasColumnName("steam_id");
            e.Property(p => p.Nickname).HasMaxLength(100).HasColumnName("nickname");
            e.Property(p => p.RegistrationDate).HasColumnName("registration_date");
            e.Property(p => p.LastLogonDate).HasColumnName("last_logon_date");
            e.Property(p => p.SourceInstance).HasMaxLength(20).HasColumnName("source_instance");
            e.Property(p => p.SyncedAt).HasColumnName("synced_at");
            
            e.HasIndex(p => p.SteamId).HasDatabaseName("ix_players_steam_id");
            e.HasIndex(p => p.Nickname).HasDatabaseName("ix_players_nickname");
        });

        // ─── PdaAccount ──────────────────────────────────────────
        modelBuilder.Entity<PdaAccount>(e =>
        {
            e.ToTable("pda_accounts");
            e.HasKey(a => new { a.SourceId, a.SourceInstance });
            e.Property(a => a.SourceId).HasColumnName("source_id");
            e.Property(a => a.Login).HasMaxLength(100).HasColumnName("login");
            e.Property(a => a.SteamId).HasMaxLength(50).HasColumnName("steam_id");
            e.Property(a => a.LastActivity).HasColumnName("last_activity");
            e.Property(a => a.SourceInstance).HasMaxLength(20).HasColumnName("source_instance");
            e.Property(a => a.SyncedAt).HasColumnName("synced_at");

            e.HasIndex(a => a.SteamId).HasDatabaseName("ix_pda_accounts_steam_id");
            e.HasIndex(a => a.Login).HasDatabaseName("ix_pda_accounts_login");

            e.HasOne(a => a.Player)
                .WithMany(p => p.PdaAccounts)
                .HasForeignKey(a => new { a.SteamId, a.SourceInstance })
                .HasPrincipalKey(p => new { p.SteamId, p.SourceInstance })
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Chat ────────────────────────────────────────────────
        modelBuilder.Entity<Chat>(e =>
        {
            e.ToTable("chats");
            e.HasKey(c => new { c.SourceId, c.SourceInstance });
            e.Property(c => c.SourceId).HasColumnName("source_id");
            e.Property(c => c.Name).HasMaxLength(200).HasColumnName("name");
            e.Property(c => c.Type).HasColumnName("type")
                .HasConversion<string>().HasMaxLength(10);
            e.Property(c => c.SourceInstance).HasMaxLength(20).HasColumnName("source_instance");
            e.Property(c => c.SyncedAt).HasColumnName("synced_at");
        });

        // ─── ChatParticipant ─────────────────────────────────────
        modelBuilder.Entity<ChatParticipant>(e =>
        {
            e.ToTable("chat_participants");
            e.HasKey(cp => cp.Id);
            e.Property(cp => cp.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(cp => cp.ChatSourceId).HasColumnName("chat_source_id");
            e.Property(cp => cp.AccountSourceId).HasColumnName("account_source_id");
            e.Property(cp => cp.ContactAccountSourceId).HasColumnName("contact_account_source_id");
            e.Property(cp => cp.SourceInstance).HasMaxLength(20).HasColumnName("source_instance");
            e.Property(cp => cp.SyncedAt).HasColumnName("synced_at");

            // Unique constraint: one record per chat+account+instance
            e.HasIndex(cp => new { cp.ChatSourceId, cp.AccountSourceId, cp.SourceInstance })
                .IsUnique()
                .HasDatabaseName("uq_chat_participants");

            e.HasOne(cp => cp.Chat)
                .WithMany(c => c.Participants)
                .HasForeignKey(cp => new { cp.ChatSourceId, cp.SourceInstance })
                .HasPrincipalKey(c => new { c.SourceId, c.SourceInstance })
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── MessageDenormalized ─────────────────────────────────
        modelBuilder.Entity<MessageDenormalized>(e =>
        {
            e.ToTable("messages_denormalized");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(m => m.SourceMessageId).HasColumnName("source_message_id");
            e.Property(m => m.SourceChatId).HasColumnName("source_chat_id");
            e.Property(m => m.ChatType).HasColumnName("chat_type")
                .HasConversion<string>().HasMaxLength(10);
            e.Property(m => m.ChatName).HasMaxLength(200).HasColumnName("chat_name");
            
            e.Property(m => m.SenderAccountId).HasColumnName("sender_account_id");
            e.Property(m => m.SenderLogin).HasMaxLength(100).HasColumnName("sender_login");
            e.Property(m => m.SenderSteamId).HasMaxLength(50).HasColumnName("sender_steam_id");
            e.Property(m => m.SenderNickname).HasMaxLength(100).HasColumnName("sender_nickname");
            
            e.Property(m => m.ReceiverAccountId).HasColumnName("receiver_account_id");
            e.Property(m => m.ReceiverLogin).HasMaxLength(100).HasColumnName("receiver_login");
            e.Property(m => m.ReceiverSteamId).HasMaxLength(50).HasColumnName("receiver_steam_id");
            e.Property(m => m.ReceiverNickname).HasMaxLength(100).HasColumnName("receiver_nickname");
            
            e.Property(m => m.Message).HasColumnName("message");
            e.Property(m => m.Attachments).HasColumnName("attachments");
            e.Property(m => m.SentAt).HasColumnName("sent_at");
            e.Property(m => m.SourceInstance).HasMaxLength(20).HasColumnName("source_instance");
            e.Property(m => m.SyncedAt).HasColumnName("synced_at");

            // ─── Индексы для быстрого поиска ───
            
            // Уникальный — дедупликация при re-sync
            e.HasIndex(m => new { m.SourceMessageId, m.SourceInstance })
                .IsUnique()
                .HasDatabaseName("uq_messages_source");
            
            // Поиск: "кто кому писал" — все сообщения между двумя SteamID
            e.HasIndex(m => new { m.SenderSteamId, m.ReceiverSteamId, m.SentAt })
                .HasDatabaseName("ix_messages_sender_receiver_date");
            
            // Поиск по получателю
            e.HasIndex(m => new { m.ReceiverSteamId, m.SentAt })
                .HasDatabaseName("ix_messages_receiver_date");
            
            // Поиск по чату
            e.HasIndex(m => new { m.SourceChatId, m.SourceInstance, m.SentAt })
                .HasDatabaseName("ix_messages_chat_date");
            
            // Поиск по дате (Live Feed)
            e.HasIndex(m => m.SentAt)
                .HasDatabaseName("ix_messages_sent_at");
            
            // Full-text search по тексту (GIN index поставим через SQL)
        });

        // ─── Faction ─────────────────────────────────────────────
        modelBuilder.Entity<Faction>(e =>
        {
            e.ToTable("factions");
            e.HasKey(f => new { f.SourceId, f.SourceInstance });
            e.Property(f => f.SourceId).HasColumnName("source_id");
            e.Property(f => f.Name).HasMaxLength(100).HasColumnName("name");
            e.Property(f => f.Color).HasColumnName("color");
            e.Property(f => f.Icon).HasMaxLength(200).HasColumnName("icon");
            e.Property(f => f.SourceInstance).HasMaxLength(20).HasColumnName("source_instance");
            e.Property(f => f.SyncedAt).HasColumnName("synced_at");
        });

        // ─── FactionMembership ───────────────────────────────────
        modelBuilder.Entity<FactionMembership>(e =>
        {
            e.ToTable("faction_memberships");
            e.HasKey(fm => fm.Id);
            e.Property(fm => fm.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(fm => fm.FactionSourceId).HasColumnName("faction_source_id");
            e.Property(fm => fm.RankId).HasColumnName("rank_id");
            e.Property(fm => fm.MemberSteamId).HasMaxLength(50).HasColumnName("member_steam_id");
            e.Property(fm => fm.SourceInstance).HasMaxLength(20).HasColumnName("source_instance");
            e.Property(fm => fm.SyncedAt).HasColumnName("synced_at");

            e.HasIndex(fm => new { fm.MemberSteamId, fm.FactionSourceId, fm.SourceInstance })
                .IsUnique()
                .HasDatabaseName("uq_faction_memberships");

            e.HasOne(fm => fm.Faction)
                .WithMany(f => f.Members)
                .HasForeignKey(fm => new { fm.FactionSourceId, fm.SourceInstance })
                .HasPrincipalKey(f => new { f.SourceId, f.SourceInstance })
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(fm => fm.Player)
                .WithMany(p => p.FactionMemberships)
                .HasForeignKey(fm => new { fm.MemberSteamId, fm.SourceInstance })
                .HasPrincipalKey(p => new { p.SteamId, p.SourceInstance })
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── SyncState ───────────────────────────────────────────
        modelBuilder.Entity<SyncState>(e =>
        {
            e.ToTable("sync_states");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(s => s.InstanceName).HasMaxLength(30).HasColumnName("instance_name");
            e.Property(s => s.TableName).HasMaxLength(50).HasColumnName("table_name");
            e.Property(s => s.LastSyncedId).HasColumnName("last_synced_id");
            e.Property(s => s.LastSyncedAt).HasColumnName("last_synced_at");
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(s => new { s.InstanceName, s.TableName })
                .IsUnique()
                .HasDatabaseName("uq_sync_state_instance_table");
        });

        // ─── WebUser ─────────────────────────────────────────────
        modelBuilder.Entity<WebUser>(e =>
        {
            e.ToTable("web_users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(u => u.Username).HasMaxLength(50).HasColumnName("username");
            e.Property(u => u.PasswordHash).HasMaxLength(200).HasColumnName("password_hash");
            e.Property(u => u.Role).HasColumnName("role")
                .HasConversion<string>().HasMaxLength(20);
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.Property(u => u.IsActive).HasColumnName("is_active");

            e.HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("uq_web_users_username");
        });

        // ─── ServerSession ───────────────────────────────────────
        modelBuilder.Entity<ServerSession>(e =>
        {
            e.ToTable("server_sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(s => s.Name).HasMaxLength(100).HasColumnName("name");
            e.Property(s => s.Description).HasMaxLength(500).HasColumnName("description");
            e.Property(s => s.MariaDbHost).HasMaxLength(200).HasColumnName("mariadb_host");
            e.Property(s => s.MariaDbUser).HasMaxLength(100).HasColumnName("mariadb_user");
            e.Property(s => s.InstanceNames).HasMaxLength(1000).HasColumnName("instance_names");
            e.Property(s => s.IsActive).HasColumnName("is_active");
            e.Property(s => s.CreatedAt).HasColumnName("created_at");
            e.Property(s => s.ArchivedAt).HasColumnName("archived_at");
            e.Property(s => s.ArchivedMessageCount).HasColumnName("archived_message_count");
            e.Property(s => s.ArchivedPlayerCount).HasColumnName("archived_player_count");
        });

        // ─── SystemSetting ───────────────────────────────────────
        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.ToTable("system_settings");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(s => s.Key).HasMaxLength(100).HasColumnName("key");
            e.Property(s => s.Value).HasMaxLength(2000).HasColumnName("value");
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(s => s.Key)
                .IsUnique()
                .HasDatabaseName("uq_system_settings_key");
        });

        // ─── ChatWebhookConfig ────────────────────────────────────
        modelBuilder.Entity<ChatWebhookConfig>(e =>
        {
            e.ToTable("discord_chat_webhook_configs");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(c => c.ChatSourceId).HasColumnName("chat_source_id");
            e.Property(c => c.SourceInstance).HasMaxLength(50).HasColumnName("source_instance");
            e.Property(c => c.ChatName).HasMaxLength(200).HasColumnName("chat_name");
            e.Property(c => c.WebhookUrl).HasMaxLength(500).HasColumnName("webhook_url");
            e.Property(c => c.IsEnabled).HasColumnName("is_enabled");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(c => new { c.ChatSourceId, c.SourceInstance })
                .HasDatabaseName("ix_discord_chat_webhook_chat");
        });

        // ─── FactionMentionConfig ──────────────────────────────────
        modelBuilder.Entity<FactionMentionConfig>(e =>
        {
            e.ToTable("discord_faction_mention_configs");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(f => f.FactionSourceId).HasColumnName("faction_source_id");
            e.Property(f => f.SourceInstance).HasMaxLength(50).HasColumnName("source_instance");
            e.Property(f => f.DisplayName).HasMaxLength(100).HasColumnName("display_name");
            e.Property(f => f.AliasesJson).HasColumnName("aliases_json");
            e.Property(f => f.DiscordRoleId).HasMaxLength(30).HasColumnName("discord_role_id");
            e.Property(f => f.WebhookUrl).HasMaxLength(500).HasColumnName("webhook_url");
            e.Property(f => f.IsEnabled).HasColumnName("is_enabled");
            e.Property(f => f.CreatedAt).HasColumnName("created_at");
            e.Property(f => f.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(f => f.FactionSourceId)
                .HasDatabaseName("ix_discord_faction_mention_faction");
        });
    }
}
