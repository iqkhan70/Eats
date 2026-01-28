using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.ChatService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    order_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    sender_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    sender_role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    message = table.Column<string>(type: "TEXT", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_read = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    read_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.message_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chat_participants",
                columns: table => new
                {
                    participant_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    order_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    joined_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_read_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_participants", x => x.participant_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_order_id",
                table: "chat_messages",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_sender_id",
                table: "chat_messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_sent_at",
                table: "chat_messages",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "IX_chat_participants_order_id",
                table: "chat_participants",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_participants_order_id_user_id",
                table: "chat_participants",
                columns: new[] { "order_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "chat_participants");
        }
    }
}
