using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPublishedForPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublishedForPractice",
                schema: "quiz",
                table: "questions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublishedForPractice",
                schema: "quiz",
                table: "essay_questions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Backfill: Manual (giáo viên tự soạn, kể cả dữ liệu cũ từ trước khi có SourceType) coi
            // như đã xuất bản — giữ nguyên trải nghiệm hiện có. AiGenerated/Imported giữ mặc định
            // false (đã set ở AddColumn phía trên), cần giáo viên chủ động xuất bản (C3).
            migrationBuilder.Sql("UPDATE quiz.questions SET IsPublishedForPractice = 1 WHERE SourceType = 'Manual' OR SourceType IS NULL;");
            migrationBuilder.Sql("UPDATE quiz.essay_questions SET IsPublishedForPractice = 1 WHERE SourceType = 'Manual' OR SourceType IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublishedForPractice",
                schema: "quiz",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "IsPublishedForPractice",
                schema: "quiz",
                table: "essay_questions");
        }
    }
}
