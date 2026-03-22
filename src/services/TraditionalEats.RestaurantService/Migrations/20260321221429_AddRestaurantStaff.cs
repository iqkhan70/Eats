using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.RestaurantService.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestaurantStaff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RestaurantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantStaff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantStaff_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantStaff_RestaurantId_UserId",
                table: "RestaurantStaff",
                columns: new[] { "RestaurantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantStaff_UserId",
                table: "RestaurantStaff",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestaurantStaff");
        }
    }
}
