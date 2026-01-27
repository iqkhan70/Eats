using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.OrderService.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialInstructionsToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpecialInstructions",
                table: "Orders",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecialInstructions",
                table: "Orders");
        }
    }
}
