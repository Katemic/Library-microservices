
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    public partial class AddUserOutboxAndVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add versioning to the existing Users table.
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            // Store events until RabbitMQ publication succeeds.
            migrationBuilder.CreateTable(
                name: "user_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "char(36)",
                        nullable: false,
                        collation: "ascii_general_ci"),

                    UserId = table.Column<int>(
                        type: "int",
                        nullable: false),

                    UserVersion = table.Column<long>(
                        type: "bigint",
                        nullable: false),

                    EventType = table.Column<string>(
                        type: "varchar(100)",
                        maxLength: 100,
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Payload = table.Column<string>(
                        type: "longtext",
                        nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    OccurredAtUtc = table.Column<DateTime>(
                        type: "datetime(6)",
                        nullable: false),

                    PublishedAtUtc = table.Column<DateTime>(
                        type: "datetime(6)",
                        nullable: true),

                    RetryCount = table.Column<int>(
                        type: "int",
                        nullable: false),

                    LastError = table.Column<string>(
                        type: "text",
                        nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_user_outbox_messages",
                        x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Efficiently query unpublished events.
            migrationBuilder.CreateIndex(
                name: "IX_user_outbox_messages_PublishedAtUtc_OccurredAtUtc",
                table: "user_outbox_messages",
                columns: new[]
                {
                    "PublishedAtUtc",
                    "OccurredAtUtc"
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_outbox_messages");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Users");
        }
    }
}
