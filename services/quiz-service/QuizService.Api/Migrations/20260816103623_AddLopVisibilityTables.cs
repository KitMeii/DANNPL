using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLopVisibilityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "essay_question_lop_visibility",
                schema: "quiz",
                columns: table => new
                {
                    EssayQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_essay_question_lop_visibility", x => new { x.EssayQuestionId, x.LopId });
                    table.ForeignKey(
                        name: "FK_essay_question_lop_visibility_essay_questions_EssayQuestionId",
                        column: x => x.EssayQuestionId,
                        principalSchema: "quiz",
                        principalTable: "essay_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_version_lop_visibility",
                schema: "quiz",
                columns: table => new
                {
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_version_lop_visibility", x => new { x.ExamVersionId, x.LopId });
                    table.ForeignKey(
                        name: "FK_exam_version_lop_visibility_exam_versions_ExamVersionId",
                        column: x => x.ExamVersionId,
                        principalSchema: "quiz",
                        principalTable: "exam_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_lop_visibility",
                schema: "quiz",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_lop_visibility", x => new { x.QuestionId, x.LopId });
                    table.ForeignKey(
                        name: "FK_question_lop_visibility_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "quiz",
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_essay_question_lop_visibility_LopId",
                schema: "quiz",
                table: "essay_question_lop_visibility",
                column: "LopId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_version_lop_visibility_LopId",
                schema: "quiz",
                table: "exam_version_lop_visibility",
                column: "LopId");

            migrationBuilder.CreateIndex(
                name: "IX_question_lop_visibility_LopId",
                schema: "quiz",
                table: "question_lop_visibility",
                column: "LopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "essay_question_lop_visibility",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "exam_version_lop_visibility",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "question_lop_visibility",
                schema: "quiz");
        }
    }
}
