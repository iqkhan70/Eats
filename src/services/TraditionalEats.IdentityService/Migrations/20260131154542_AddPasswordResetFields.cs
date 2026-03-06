using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.IdentityService.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: only add if column doesn't exist (handles schema/history mismatch)
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'PasswordResetToken');
                SET @sql = IF(@col_exists = 0, 
                    'ALTER TABLE `Users` ADD `PasswordResetToken` varchar(500) CHARACTER SET utf8mb4 NULL', 
                    'SELECT 1');
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'PasswordResetTokenExpiry');
                SET @sql = IF(@col_exists = 0, 
                    'ALTER TABLE `Users` ADD `PasswordResetTokenExpiry` datetime(6) NULL', 
                    'SELECT 1');
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");
            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND INDEX_NAME = 'IX_Users_PasswordResetToken');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE INDEX `IX_Users_PasswordResetToken` ON `Users` (`PasswordResetToken`)', 
                    'SELECT 1');
                PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PasswordResetToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Users");
        }
    }
}
