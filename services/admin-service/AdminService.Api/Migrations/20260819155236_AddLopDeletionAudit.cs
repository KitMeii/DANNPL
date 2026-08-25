using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLopDeletionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lop_deletion_audits",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreparationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LopTen = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StudentsCount = table.Column<int>(type: "int", nullable: false),
                    QuizResultsCount = table.Column<int>(type: "int", nullable: false),
                    ExamResultsCount = table.Column<int>(type: "int", nullable: false),
                    ExamSessionsCount = table.Column<int>(type: "int", nullable: false),
                    OralResultsCount = table.Column<int>(type: "int", nullable: false),
                    WrongAnswersCount = table.Column<int>(type: "int", nullable: false),
                    QuestionVisibilityCount = table.Column<int>(type: "int", nullable: false),
                    EssayQuestionVisibilityCount = table.Column<int>(type: "int", nullable: false),
                    ExamVersionVisibilityCount = table.Column<int>(type: "int", nullable: false),
                    StudentProgressCount = table.Column<int>(type: "int", nullable: false),
                    StudyLogsCount = table.Column<int>(type: "int", nullable: false),
                    ActivityLogsCount = table.Column<int>(type: "int", nullable: false),
                    StepResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lop_deletion_audits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lop_deletion_audits_LopId",
                schema: "admin",
                table: "lop_deletion_audits",
                column: "LopId");

            migrationBuilder.CreateIndex(
                name: "IX_lop_deletion_audits_PreparationId",
                schema: "admin",
                table: "lop_deletion_audits",
                column: "PreparationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lop_deletion_audits",
                schema: "admin");
        }
    }
}
