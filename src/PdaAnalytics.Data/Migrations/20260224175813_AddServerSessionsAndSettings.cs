using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PdaAnalytics.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServerSessionsAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "server_sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    mariadb_host = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mariadb_user = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    instance_names = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    archived_message_count = table.Column<int>(type: "integer", nullable: true),
                    archived_player_count = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_server_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_system_settings_key",
                table: "system_settings",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "server_sessions");

            migrationBuilder.DropTable(
                name: "system_settings");
        }
    }
}
