using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiggyzenMvp.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImportMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAtUtc",
                table: "Transactions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ImportSequence",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE \"Transactions\" SET \"ImportSequence\" = COALESCE(\"SequenceInBatch\", 0);"
            );

            migrationBuilder.DropColumn(
                name: "SequenceInBatch",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportBatchId",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SequenceInBatch",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Transactions\" SET \"SequenceInBatch\" = \"ImportSequence\";"
            );

            migrationBuilder.DropColumn(
                name: "ImportSequence",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ImportedAtUtc",
                table: "Transactions");
        }
    }
}
