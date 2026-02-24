using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PdaAnalytics.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Таблицы могли быть созданы предыдущим MigrateAsync до записи в history
            migrationBuilder.Sql("DROP TABLE IF EXISTS discord_chat_webhook_configs CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS discord_faction_mention_configs CASCADE;");

            migrationBuilder.CreateTable(
                name: "discord_chat_webhook_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chat_source_id = table.Column<int>(type: "integer", nullable: false),
                    source_instance = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chat_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    webhook_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_chat_webhook_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "discord_faction_mention_configs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    faction_source_id = table.Column<int>(type: "integer", nullable: true),
                    source_instance = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aliases_json = table.Column<string>(type: "text", nullable: false),
                    webhook_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_faction_mention_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_discord_chat_webhook_chat",
                table: "discord_chat_webhook_configs",
                columns: new[] { "chat_source_id", "source_instance" });

            migrationBuilder.CreateIndex(
                name: "ix_discord_faction_mention_faction",
                table: "discord_faction_mention_configs",
                column: "faction_source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discord_chat_webhook_configs");

            migrationBuilder.DropTable(
                name: "discord_faction_mention_configs");
        }
    }
}
