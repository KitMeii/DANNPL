using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionSourceAndEssayQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceMaterialId",
                schema: "quiz",
                table: "questions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                schema: "quiz",
                table: "questions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.CreateTable(
                name: "essay_questions",
                schema: "quiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    QuestionText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SuggestedAnswer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Manual"),
                    SourceMaterialId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_essay_questions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_essay_questions_Chapter",
                schema: "quiz",
                table: "essay_questions",
                column: "Chapter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "essay_questions",
                schema: "quiz");

            migrationBuilder.DropColumn(
                name: "SourceMaterialId",
                schema: "quiz",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                schema: "quiz",
                table: "questions");
        }
    }
}
