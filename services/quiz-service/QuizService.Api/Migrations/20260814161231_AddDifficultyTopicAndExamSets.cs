using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDifficultyTopicAndExamSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                schema: "quiz",
                table: "questions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                schema: "quiz",
                table: "questions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exam_sets",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalPoolSize = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_sets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exam_versions",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaDe = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exam_versions_exam_sets_ExamSetId",
                        column: x => x.ExamSetId,
                        principalSchema: "quiz",
                        principalTable: "exam_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_version_questions",
                schema: "quiz",
                columns: table => new
                {
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_version_questions", x => new { x.ExamVersionId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_exam_version_questions_exam_versions_ExamVersionId",
                        column: x => x.ExamVersionId,
                        principalSchema: "quiz",
                        principalTable: "exam_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exam_version_questions_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "quiz",
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_questions_Difficulty",
                schema: "quiz",
                table: "questions",
                column: "Difficulty");

            migrationBuilder.CreateIndex(
                name: "IX_exam_version_questions_QuestionId",
                schema: "quiz",
                table: "exam_version_questions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_versions_ExamSetId",
                schema: "quiz",
                table: "exam_versions",
                column: "ExamSetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_version_questions",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "exam_versions",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "exam_sets",
                schema: "quiz");

            migrationBuilder.DropIndex(
                name: "IX_questions_Difficulty",
                schema: "quiz",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                schema: "quiz",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "Topic",
                schema: "quiz",
                table: "questions");
        }
    }
}
