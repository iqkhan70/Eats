using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.RestaurantService.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "EloRating",
                table: "Restaurants",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 1500m,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AddColumn<string>(
                name: "VendorType",
                table: "Restaurants",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Food")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VendorType",
                table: "Restaurants");

            migrationBuilder.AlterColumn<decimal>(
                name: "EloRating",
                table: "Restaurants",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)",
                oldDefaultValue: 1500m);
        }
    }
}
