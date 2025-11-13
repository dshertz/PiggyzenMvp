using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiggyzenMvp.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameCategorizationHistoryToRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategorizationUsages_CategorizationHistories_CategorizationHistoryId",
                table: "CategorizationUsages");

            migrationBuilder.DropForeignKey(
                name: "FK_CategorizationHistories_Categories_CategoryId",
                table: "CategorizationHistories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CategorizationHistories",
                table: "CategorizationHistories");

            migrationBuilder.RenameTable(
                name: "CategorizationHistories",
                newName: "CategorizationRules");

            migrationBuilder.RenameIndex(
                name: "IX_CategorizationHistories_NormalizedDescription_IsPositive",
                table: "CategorizationRules",
                newName: "IX_CategorizationRules_NormalizedDescription_IsPositive");

            migrationBuilder.RenameIndex(
                name: "IX_CategorizationHistories_NormalizedDescription",
                table: "CategorizationRules",
                newName: "IX_CategorizationRules_NormalizedDescription");

            migrationBuilder.RenameIndex(
                name: "IX_CategorizationHistories_CategoryId",
                table: "CategorizationRules",
                newName: "IX_CategorizationRules_CategoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CategorizationRules",
                table: "CategorizationRules",
                column: "Id");

            migrationBuilder.RenameColumn(
                name: "CategorizationHistoryId",
                table: "CategorizationUsages",
                newName: "CategorizationRuleId");

            migrationBuilder.RenameIndex(
                name: "IX_CategorizationUsages_CategorizationHistoryId",
                table: "CategorizationUsages",
                newName: "IX_CategorizationUsages_CategorizationRuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_CategorizationRules_Categories_CategoryId",
                table: "CategorizationRules",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CategorizationUsages_CategorizationRules_CategorizationRuleId",
                table: "CategorizationUsages",
                column: "CategorizationRuleId",
                principalTable: "CategorizationRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategorizationRules_Categories_CategoryId",
                table: "CategorizationRules");

            migrationBuilder.DropForeignKey(
                name: "FK_CategorizationUsages_CategorizationRules_CategorizationRuleId",
                table: "CategorizationUsages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CategorizationRules",
                table: "CategorizationRules");

            migrationBuilder.RenameTable(
                name: "CategorizationRules",
                newName: "CategorizationHistories");

            migrationBuilder.RenameIndex(
                name: "IX_CategorizationRules_NormalizedDescription_IsPositive",
                table: "CategorizationHistories",
                newName: "IX_CategorizationHistories_NormalizedDescription_IsPositive");

            migrationBuilder.RenameIndex(
                name: "IX_CategorizationRules_NormalizedDescription",
                table: "CategorizationHistories",
                newName: "IX_CategorizationHistories_NormalizedDescription");

            migrationBuilder.RenameIndex(
                name: "IX_CategorizationRules_CategoryId",
                table: "CategorizationHistories",
                newName: "IX_CategorizationHistories_CategoryId");

            migrationBuilder.RenameColumn(
                name: "CategorizationRuleId",
                table: "CategorizationUsages",
                newName: "CategorizationHistoryId");

            migrationBuilder.RenameIndex(
                name: "IX_CategorizationUsages_CategorizationRuleId",
                table: "CategorizationUsages",
                newName: "IX_CategorizationUsages_CategorizationHistoryId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CategorizationHistories",
                table: "CategorizationHistories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CategorizationHistories_Categories_CategoryId",
                table: "CategorizationHistories",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CategorizationUsages_CategorizationHistories_CategorizationHistoryId",
                table: "CategorizationUsages",
                column: "CategorizationHistoryId",
                principalTable: "CategorizationHistories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
