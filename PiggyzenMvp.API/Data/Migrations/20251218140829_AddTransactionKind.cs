using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiggyzenMvp.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TypeRaw",
                table: "Transactions",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TypeRaw",
                table: "Transactions");
        }
    }
}
