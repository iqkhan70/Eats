using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.ChatService.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "metadata_json",
                table: "vendor_chat_messages",
                type: "JSON",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "metadata_json",
                table: "chat_messages",
                type: "JSON",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "metadata_json",
                table: "vendor_chat_messages");

            migrationBuilder.DropColumn(
                name: "metadata_json",
                table: "chat_messages");
        }
    }
}
