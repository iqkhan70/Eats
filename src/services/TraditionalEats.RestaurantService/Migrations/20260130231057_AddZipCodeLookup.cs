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
            // Idempotent: table may already exist (e.g. created manually or by a previous run)
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `ZipCodeLookup` (
    `ZipCode` varchar(10) NOT NULL,
    `Latitude` decimal(10,8) NOT NULL,
    `Longitude` decimal(11,8) NOT NULL,
    `City` varchar(100) NULL,
    `State` varchar(2) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`ZipCode`)
) CHARACTER SET utf8mb4;
");
            // Create index only if it does not exist (MySQL has no IF NOT EXISTS for indexes; table name may be lowercased)
            migrationBuilder.Sql(@"
SET @exist = (SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND LOWER(table_name) = 'zipcodelookup' AND index_name = 'IX_ZipCodeLookup_State');
SET @sql = IF(@exist = 0, 'CREATE INDEX IX_ZipCodeLookup_State ON ZipCodeLookup (State)', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ZipCodeLookup");
        }
    }
}
