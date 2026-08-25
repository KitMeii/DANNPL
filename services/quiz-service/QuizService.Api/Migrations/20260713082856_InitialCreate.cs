using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quiz");

            migrationBuilder.CreateTable(
                name: "exam_results",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    Correct = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    TimeSpentSeconds = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "oral_questions",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oral_questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    QuestionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionB = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectAnswer = table.Column<int>(type: "int", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "quiz_results",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Score = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    Correct = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quiz_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "oral_results",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MainAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FollowupAnswersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiScore = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    AiComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RubricScoresJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oral_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_oral_results_oral_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "quiz",
                        principalTable: "oral_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wrong_answers",
                schema: "quiz",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WrongCount = table.Column<int>(type: "int", nullable: false),
                    LastWrongAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wrong_answers", x => new { x.UserId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_wrong_answers_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "quiz",
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exam_results_UserId",
                schema: "quiz",
                table: "exam_results",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_oral_questions_Chapter",
                schema: "quiz",
                table: "oral_questions",
                column: "Chapter");

            migrationBuilder.CreateIndex(
                name: "IX_oral_results_QuestionId",
                schema: "quiz",
                table: "oral_results",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_oral_results_UserId",
                schema: "quiz",
                table: "oral_results",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_questions_Chapter",
                schema: "quiz",
                table: "questions",
                column: "Chapter");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_results_UserId",
                schema: "quiz",
                table: "quiz_results",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_wrong_answers_QuestionId",
                schema: "quiz",
                table: "wrong_answers",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_results",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "oral_results",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "quiz_results",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "wrong_answers",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "oral_questions",
                schema: "quiz");

            migrationBuilder.DropTable(
                name: "questions",
                schema: "quiz");
        }
    }
}
