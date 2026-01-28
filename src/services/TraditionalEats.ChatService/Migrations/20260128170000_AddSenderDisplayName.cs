using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.ChatService.Migrations
{
    /// <inheritdoc />
    public partial class AddSenderDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sender_display_name",
                table: "chat_messages",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sender_display_name",
                table: "chat_messages");
        }
    }
}
