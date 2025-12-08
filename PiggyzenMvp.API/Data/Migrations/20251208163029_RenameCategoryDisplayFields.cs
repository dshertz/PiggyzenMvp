using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiggyzenMvp.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameCategoryDisplayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserDisplayName",
                table: "Categories",
                newName: "CustomDisplayName");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "Categories",
                newName: "SystemDisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SystemDisplayName",
                table: "Categories",
                newName: "DisplayName");

            migrationBuilder.RenameColumn(
                name: "CustomDisplayName",
                table: "Categories",
                newName: "UserDisplayName");
        }
    }
}
