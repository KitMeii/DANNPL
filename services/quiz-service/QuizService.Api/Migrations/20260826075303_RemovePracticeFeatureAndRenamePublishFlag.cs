using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemovePracticeFeatureAndRenamePublishFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "practice_set_lop_visibility",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "quiz_results",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "practice_sets",
                schema: "quiz");

            migrationBuilder.RenameColumn(
                name: "IsPublishedForPractice",
                schema: "quiz",
                table: "questions",
                newName: "IsPublished");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPublished",
                schema: "quiz",
                table: "questions",
                newName: "IsPublishedForPractice");

            migrationBuilder.CreateTable(
                name: "practice_sets",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GiaoVienId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_sets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "quiz_results",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Correct = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "practice_set_lop_visibility",
                schema: "quiz",
                columns: table => new
                {
                    PracticeSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_set_lop_visibility", x => new { x.PracticeSetId, x.LopId });
                    table.ForeignKey(
                        name: "FK_practice_set_lop_visibility_practice_sets_PracticeSetId",
                        column: x => x.PracticeSetId,
                        principalSchema: "quiz",
                        principalTable: "practice_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_practice_set_lop_visibility_LopId",
                schema: "quiz",
                table: "practice_set_lop_visibility",
                column: "LopId");

            migrationBuilder.CreateIndex(
                name: "IX_practice_sets_GiaoVienId",
                schema: "quiz",
                table: "practice_sets",
                column: "GiaoVienId");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_results_UserId",
                schema: "quiz",
                table: "quiz_results",
                column: "UserId");
        }
    }
}
