using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.RestaurantService.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveDealFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveDealDiscountPercent",
                table: "Restaurants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActiveDealEndTime",
                table: "Restaurants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveDealTitle",
                table: "Restaurants",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveDealDiscountPercent",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "ActiveDealEndTime",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "ActiveDealTitle",
                table: "Restaurants");
        }
    }
}
