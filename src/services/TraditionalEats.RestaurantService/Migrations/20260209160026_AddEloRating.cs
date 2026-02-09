using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.RestaurantService.Migrations
{
    /// <inheritdoc />
    public partial class AddEloRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add EloRating column only if it doesn't exist
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Restaurants';
                SET @columnname = 'EloRating';
                SET @preparedStatement = (SELECT IF(
                  (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE
                      (table_name = @tablename)
                      AND (table_schema = @dbname)
                      AND (column_name = @columnname)
                  ) > 0,
                  'SELECT ''Column EloRating already exists, skipping add'';',
                  CONCAT('ALTER TABLE ', @tablename, ' ADD ', @columnname, ' decimal(65,30) NOT NULL DEFAULT 1500.0;')
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
            
            // Initialize EloRating for existing restaurants that might have 0 or NULL
            // This ensures all restaurants start with base Elo rating (1500)
            migrationBuilder.Sql(@"
                UPDATE Restaurants 
                SET EloRating = 1500.0 
                WHERE EloRating = 0 OR EloRating IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop EloRating column only if it exists
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Restaurants';
                SET @columnname = 'EloRating';
                SET @preparedStatement = (SELECT IF(
                  (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE
                      (table_name = @tablename)
                      AND (table_schema = @dbname)
                      AND (column_name = @columnname)
                  ) > 0,
                  CONCAT('ALTER TABLE ', @tablename, ' DROP COLUMN ', @columnname, ';'),
                  'SELECT ''Column EloRating does not exist, skipping drop'';'
                ));
                PREPARE stmt FROM @preparedStatement;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }
    }
}
