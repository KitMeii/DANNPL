using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExamVersionKindAndOralQuestionJoin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "quiz",
                table: "exam_versions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Mcq");

            migrationBuilder.CreateTable(
                name: "exam_version_oral_questions",
                schema: "quiz",
                columns: table => new
                {
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OralQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_version_oral_questions", x => new { x.ExamVersionId, x.OralQuestionId });
                    table.ForeignKey(
                        name: "FK_exam_version_oral_questions_exam_versions_ExamVersionId",
                        column: x => x.ExamVersionId,
                        principalSchema: "quiz",
                        principalTable: "exam_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exam_version_oral_questions_oral_questions_OralQuestionId",
                        column: x => x.OralQuestionId,
                        principalSchema: "quiz",
                        principalTable: "oral_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exam_version_oral_questions_OralQuestionId",
                schema: "quiz",
                table: "exam_version_oral_questions",
                column: "OralQuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_version_oral_questions",
                schema: "quiz");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "quiz",
                table: "exam_versions");
        }
    }
}
