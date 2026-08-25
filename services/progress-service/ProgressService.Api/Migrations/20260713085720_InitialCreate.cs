using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProgressService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "progress");

            migrationBuilder.CreateTable(
                name: "student_progress",
                schema: "progress",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Streak = table.Column<int>(type: "int", nullable: false),
                    LastStudyDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TotalStudyMinutes = table.Column<int>(type: "int", nullable: false),
                    TotalAttempts = table.Column<int>(type: "int", nullable: false),
                    ScoreSum = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_progress", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "study_logs",
                schema: "progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Minutes = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_logs_UserId_StudyDate",
                schema: "progress",
                table: "study_logs",
                columns: new[] { "UserId", "StudyDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_progress",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "study_logs",
                schema: "progress");
        }
    }
}
