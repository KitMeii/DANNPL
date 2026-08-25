using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOralQuestionLopVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "oral_question_lop_visibility",
                schema: "quiz",
                columns: table => new
                {
                    OralQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oral_question_lop_visibility", x => new { x.OralQuestionId, x.LopId });
                    table.ForeignKey(
                        name: "FK_oral_question_lop_visibility_oral_questions_OralQuestionId",
                        column: x => x.OralQuestionId,
                        principalSchema: "quiz",
                        principalTable: "oral_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_oral_question_lop_visibility_LopId",
                schema: "quiz",
                table: "oral_question_lop_visibility",
                column: "LopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "oral_question_lop_visibility",
                schema: "quiz");
        }
    }
}
