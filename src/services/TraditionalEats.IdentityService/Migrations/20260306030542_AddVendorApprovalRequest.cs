using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.IdentityService.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorApprovalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PasswordResetToken/PasswordResetTokenExpiry already added by AddPasswordResetFields
            migrationBuilder.CreateTable(
                name: "VendorApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserEmail = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorApprovalRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_VendorApprovalRequests_RequestedAt",
                table: "VendorApprovalRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VendorApprovalRequests_Status",
                table: "VendorApprovalRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VendorApprovalRequests_UserId",
                table: "VendorApprovalRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorApprovalRequests");
        }
    }
}
