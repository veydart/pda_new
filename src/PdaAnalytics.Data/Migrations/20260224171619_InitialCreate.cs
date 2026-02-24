using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PdaAnalytics.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chats",
                columns: table => new
                {
                    source_id = table.Column<int>(type: "integer", nullable: false),
                    source_instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chats", x => new { x.source_id, x.source_instance });
                });

            migrationBuilder.CreateTable(
                name: "factions",
                columns: table => new
                {
                    source_id = table.Column<int>(type: "integer", nullable: false),
                    source_instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    color = table.Column<long>(type: "bigint", nullable: false),
                    icon = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factions", x => new { x.source_id, x.source_instance });
                });

            migrationBuilder.CreateTable(
                name: "messages_denormalized",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_message_id = table.Column<int>(type: "integer", nullable: false),
                    source_chat_id = table.Column<int>(type: "integer", nullable: false),
                    chat_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    chat_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sender_account_id = table.Column<int>(type: "integer", nullable: false),
                    sender_login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sender_steam_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sender_nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    receiver_account_id = table.Column<int>(type: "integer", nullable: true),
                    receiver_login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    receiver_steam_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    receiver_nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    message = table.Column<string>(type: "text", nullable: false),
                    attachments = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages_denormalized", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    steam_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    registration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_logon_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => new { x.steam_id, x.source_instance });
                });

            migrationBuilder.CreateTable(
                name: "sync_states",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instance_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    table_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_synced_id = table.Column<long>(type: "bigint", nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "faction_memberships",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    faction_source_id = table.Column<int>(type: "integer", nullable: false),
                    rank_id = table.Column<int>(type: "integer", nullable: false),
                    member_steam_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faction_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_faction_memberships_factions_faction_source_id_source_insta~",
                        columns: x => new { x.faction_source_id, x.source_instance },
                        principalTable: "factions",
                        principalColumns: new[] { "source_id", "source_instance" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_faction_memberships_players_member_steam_id_source_instance",
                        columns: x => new { x.member_steam_id, x.source_instance },
                        principalTable: "players",
                        principalColumns: new[] { "steam_id", "source_instance" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pda_accounts",
                columns: table => new
                {
                    source_id = table.Column<int>(type: "integer", nullable: false),
                    source_instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    login = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    steam_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_activity = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pda_accounts", x => new { x.source_id, x.source_instance });
                    table.ForeignKey(
                        name: "FK_pda_accounts_players_steam_id_source_instance",
                        columns: x => new { x.steam_id, x.source_instance },
                        principalTable: "players",
                        principalColumns: new[] { "steam_id", "source_instance" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_participants",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chat_source_id = table.Column<int>(type: "integer", nullable: false),
                    account_source_id = table.Column<int>(type: "integer", nullable: false),
                    contact_account_source_id = table.Column<int>(type: "integer", nullable: false),
                    source_instance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccountSourceInstance = table.Column<string>(type: "character varying(20)", nullable: true),
                    ContactAccountSourceInstance = table.Column<string>(type: "character varying(20)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_participants_chats_chat_source_id_source_instance",
                        columns: x => new { x.chat_source_id, x.source_instance },
                        principalTable: "chats",
                        principalColumns: new[] { "source_id", "source_instance" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_participants_pda_accounts_account_source_id_AccountSou~",
                        columns: x => new { x.account_source_id, x.AccountSourceInstance },
                        principalTable: "pda_accounts",
                        principalColumns: new[] { "source_id", "source_instance" });
                    table.ForeignKey(
                        name: "FK_chat_participants_pda_accounts_contact_account_source_id_Co~",
                        columns: x => new { x.contact_account_source_id, x.ContactAccountSourceInstance },
                        principalTable: "pda_accounts",
                        principalColumns: new[] { "source_id", "source_instance" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_participants_account_source_id_AccountSourceInstance",
                table: "chat_participants",
                columns: new[] { "account_source_id", "AccountSourceInstance" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_participants_chat_source_id_source_instance",
                table: "chat_participants",
                columns: new[] { "chat_source_id", "source_instance" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_participants_contact_account_source_id_ContactAccountS~",
                table: "chat_participants",
                columns: new[] { "contact_account_source_id", "ContactAccountSourceInstance" });

            migrationBuilder.CreateIndex(
                name: "uq_chat_participants",
                table: "chat_participants",
                columns: new[] { "chat_source_id", "account_source_id", "source_instance" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_faction_memberships_faction_source_id_source_instance",
                table: "faction_memberships",
                columns: new[] { "faction_source_id", "source_instance" });

            migrationBuilder.CreateIndex(
                name: "IX_faction_memberships_member_steam_id_source_instance",
                table: "faction_memberships",
                columns: new[] { "member_steam_id", "source_instance" });

            migrationBuilder.CreateIndex(
                name: "uq_faction_memberships",
                table: "faction_memberships",
                columns: new[] { "member_steam_id", "faction_source_id", "source_instance" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_chat_date",
                table: "messages_denormalized",
                columns: new[] { "source_chat_id", "source_instance", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_receiver_date",
                table: "messages_denormalized",
                columns: new[] { "receiver_steam_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender_receiver_date",
                table: "messages_denormalized",
                columns: new[] { "sender_steam_id", "receiver_steam_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_messages_sent_at",
                table: "messages_denormalized",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "uq_messages_source",
                table: "messages_denormalized",
                columns: new[] { "source_message_id", "source_instance" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pda_accounts_login",
                table: "pda_accounts",
                column: "login");

            migrationBuilder.CreateIndex(
                name: "ix_pda_accounts_steam_id",
                table: "pda_accounts",
                column: "steam_id");

            migrationBuilder.CreateIndex(
                name: "IX_pda_accounts_steam_id_source_instance",
                table: "pda_accounts",
                columns: new[] { "steam_id", "source_instance" });

            migrationBuilder.CreateIndex(
                name: "ix_players_nickname",
                table: "players",
                column: "nickname");

            migrationBuilder.CreateIndex(
                name: "ix_players_steam_id",
                table: "players",
                column: "steam_id");

            migrationBuilder.CreateIndex(
                name: "uq_sync_state_instance_table",
                table: "sync_states",
                columns: new[] { "instance_name", "table_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_participants");

            migrationBuilder.DropTable(
                name: "faction_memberships");

            migrationBuilder.DropTable(
                name: "messages_denormalized");

            migrationBuilder.DropTable(
                name: "sync_states");

            migrationBuilder.DropTable(
                name: "chats");

            migrationBuilder.DropTable(
                name: "pda_accounts");

            migrationBuilder.DropTable(
                name: "factions");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}
