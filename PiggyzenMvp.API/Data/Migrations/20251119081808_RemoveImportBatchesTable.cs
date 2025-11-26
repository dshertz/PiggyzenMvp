using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiggyzenMvp.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveImportBatchesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ImportBatches\";");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FirstDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });
        }
    }
}
