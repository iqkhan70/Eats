using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceFeeToPaymentIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFee",
                table: "PaymentIntents",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceFee",
                table: "PaymentIntents");
        }
    }
}
