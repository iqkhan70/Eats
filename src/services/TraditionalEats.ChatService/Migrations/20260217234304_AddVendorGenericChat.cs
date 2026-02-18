using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.ChatService.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorGenericChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vendor_chat_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    conversation_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    sender_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    sender_role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sender_display_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    message = table.Column<string>(type: "TEXT", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sent_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_read = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    read_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_chat_messages", x => x.message_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "vendor_conversations",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    restaurant_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    customer_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    customer_display_name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_message_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_conversations", x => x.conversation_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_chat_messages_conversation_id",
                table: "vendor_chat_messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_chat_messages_sender_id",
                table: "vendor_chat_messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_chat_messages_sent_at",
                table: "vendor_chat_messages",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_conversations_customer_id",
                table: "vendor_conversations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_conversations_last_message_at",
                table: "vendor_conversations",
                column: "last_message_at");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_conversations_restaurant_id",
                table: "vendor_conversations",
                column: "restaurant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_conversations_restaurant_id_customer_id",
                table: "vendor_conversations",
                columns: new[] { "restaurant_id", "customer_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vendor_chat_messages");

            migrationBuilder.DropTable(
                name: "vendor_conversations");
        }
    }
}
