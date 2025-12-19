using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiggyzenMvp.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DescriptionSignatureId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DescriptionSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NormalizedDescription = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPositive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMachineGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    MachineConfidence = table.Column<decimal>(type: "decimal(5,3)", nullable: false),
                    MerchantCandidate = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MachineSource = table.Column<int>(type: "INTEGER", nullable: false),
                    SeenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DescriptionSignatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_DescriptionSignatureId",
                table: "Transactions",
                column: "DescriptionSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_DescriptionSignatures_NormalizedDescription",
                table: "DescriptionSignatures",
                column: "NormalizedDescription");

            migrationBuilder.CreateIndex(
                name: "IX_DescriptionSignatures_NormalizedDescription_Kind_IsPositive",
                table: "DescriptionSignatures",
                columns: new[] { "NormalizedDescription", "Kind", "IsPositive" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_DescriptionSignatures_DescriptionSignatureId",
                table: "Transactions",
                column: "DescriptionSignatureId",
                principalTable: "DescriptionSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_DescriptionSignatures_DescriptionSignatureId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "DescriptionSignatures");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_DescriptionSignatureId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DescriptionSignatureId",
                table: "Transactions");
        }
    }
}
