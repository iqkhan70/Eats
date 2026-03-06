using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.IdentityService.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstNameLastNameToVendorApprovalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "VendorApprovalRequests",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "VendorApprovalRequests",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "VendorApprovalRequests");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "VendorApprovalRequests");
        }
    }
}
