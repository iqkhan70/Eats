using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.RestaurantService.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantLatLonIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only add index on Restaurants (Latitude, Longitude) for bounding-box / distance queries
            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_Latitude_Longitude",
                table: "Restaurants",
                columns: new[] { "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Restaurants_Latitude_Longitude",
                table: "Restaurants");
        }
    }
}
