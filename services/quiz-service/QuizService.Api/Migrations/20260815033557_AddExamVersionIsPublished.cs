using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExamVersionIsPublished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                schema: "quiz",
                table: "exam_versions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                schema: "quiz",
                table: "exam_versions");
        }
    }
}
