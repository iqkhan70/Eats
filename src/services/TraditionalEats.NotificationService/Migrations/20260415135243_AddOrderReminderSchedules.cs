using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.NotificationService.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReminderSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderReminderSchedules",
                columns: table => new
                {
                    OrderReminderScheduleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RestaurantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReminderCountSent = table.Column<int>(type: "int", nullable: false),
                    MaxReminders = table.Column<int>(type: "int", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    NextReminderAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReminderSchedules", x => x.OrderReminderScheduleId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReminderSchedules_IsActive",
                table: "OrderReminderSchedules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReminderSchedules_NextReminderAt",
                table: "OrderReminderSchedules",
                column: "NextReminderAt");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReminderSchedules_OrderId",
                table: "OrderReminderSchedules",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderReminderSchedules");
        }
    }
}
