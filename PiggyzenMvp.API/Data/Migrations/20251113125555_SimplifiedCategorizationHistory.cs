using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiggyzenMvp.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifiedCategorizationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimesConfirmed",
                table: "CategorizationHistories");

            migrationBuilder.DropColumn(
                name: "TimesOverridden",
                table: "CategorizationHistories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimesConfirmed",
                table: "CategorizationHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimesOverridden",
                table: "CategorizationHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
