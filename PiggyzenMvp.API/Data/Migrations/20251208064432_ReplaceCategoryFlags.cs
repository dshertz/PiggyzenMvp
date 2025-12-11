using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiggyzenMvp.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCategoryFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                UPDATE Categories
                SET IsEnabled = CASE WHEN IsActive = 1 AND IsHidden = 0 THEN 1 ELSE 0 END;
                """
            );

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE Categories
                SET
                    IsActive = CASE WHEN IsEnabled = 1 THEN 1 ELSE 0 END,
                    IsHidden = CASE WHEN IsEnabled = 1 THEN 0 ELSE 1 END;
                """
            );

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "Categories");
        }
    }
}
