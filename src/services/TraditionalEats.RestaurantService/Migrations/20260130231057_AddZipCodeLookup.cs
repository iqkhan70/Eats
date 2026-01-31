using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.RestaurantService.Migrations
{
    /// <inheritdoc />
    public partial class AddZipCodeLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZipCodeLookup",
                columns: table => new
                {
                    ZipCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: false),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZipCodeLookup", x => x.ZipCode);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ZipCodeLookup_State",
                table: "ZipCodeLookup",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ZipCodeLookup");
        }
    }
}
