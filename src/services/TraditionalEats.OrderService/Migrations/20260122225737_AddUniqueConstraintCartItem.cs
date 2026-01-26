using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.OrderService.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintCartItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_MenuItemId",
                table: "CartItems",
                columns: new[] { "CartId", "MenuItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_MenuItemId",
                table: "CartItems");
        }
    }
}
