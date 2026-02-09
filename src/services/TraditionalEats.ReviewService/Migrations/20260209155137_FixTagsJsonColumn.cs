using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraditionalEats.ReviewService.Migrations
{
    /// <inheritdoc />
    public partial class FixTagsJsonColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename Tags column to TagsJson if it exists
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Reviews';
                SET @columnname = 'Tags';
                SET @newname = 'TagsJson';
                SET @preparedStatement = (SELECT IF(
                  (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE
                      (table_name = @tablename)
                      AND (table_schema = @dbname)
                      AND (column_name = @columnname)
                  ) > 0,
                  CONCAT('ALTER TABLE ', @tablename, ' CHANGE ', @columnname, ' ', @newname, ' JSON NOT NULL;'),
                  'SELECT ''Column Tags does not exist, skipping rename'';'
                ));
                PREPARE alterIfExists FROM @preparedStatement;
                EXECUTE alterIfExists;
                DEALLOCATE PREPARE alterIfExists;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rename TagsJson column back to Tags if needed
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @tablename = 'Reviews';
                SET @columnname = 'TagsJson';
                SET @newname = 'Tags';
                SET @preparedStatement = (SELECT IF(
                  (
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE
                      (table_name = @tablename)
                      AND (table_schema = @dbname)
                      AND (column_name = @columnname)
                  ) > 0,
                  CONCAT('ALTER TABLE ', @tablename, ' CHANGE ', @columnname, ' ', @newname, ' JSON NOT NULL;'),
                  'SELECT ''Column TagsJson does not exist, skipping rename'';'
                ));
                PREPARE alterIfExists FROM @preparedStatement;
                EXECUTE alterIfExists;
                DEALLOCATE PREPARE alterIfExists;
            ");
        }
    }
}
