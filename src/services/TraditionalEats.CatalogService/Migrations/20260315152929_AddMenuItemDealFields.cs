using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.CatalogService.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemDealFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveDealDiscountPercent",
                table: "MenuItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActiveDealEndTime",
                table: "MenuItems",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveDealTitle",
                table: "MenuItems",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveDealDiscountPercent",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ActiveDealEndTime",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ActiveDealTitle",
                table: "MenuItems");
        }
    }
}
