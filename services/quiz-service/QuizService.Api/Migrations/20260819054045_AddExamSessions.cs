using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExamSessionId",
                schema: "quiz",
                table: "oral_results",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExamSessionId",
                schema: "quiz",
                table: "exam_results",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoSubmitted",
                schema: "quiz",
                table: "exam_results",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "exam_sessions",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    QuestionIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedDurationSeconds = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "InProgress"),
                    ExamResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_oral_results_ExamSessionId",
                schema: "quiz",
                table: "oral_results",
                column: "ExamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_exam_sessions_UserId_Kind_Status",
                schema: "quiz",
                table: "exam_sessions",
                columns: new[] { "UserId", "Kind", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_sessions",
                schema: "quiz");

            migrationBuilder.DropIndex(
                name: "IX_oral_results_ExamSessionId",
                schema: "quiz",
                table: "oral_results");

            migrationBuilder.DropColumn(
                name: "ExamSessionId",
                schema: "quiz",
                table: "oral_results");

            migrationBuilder.DropColumn(
                name: "ExamSessionId",
                schema: "quiz",
                table: "exam_results");

            migrationBuilder.DropColumn(
                name: "IsAutoSubmitted",
                schema: "quiz",
                table: "exam_results");
        }
    }
}
