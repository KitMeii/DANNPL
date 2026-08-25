using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudinaryPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CloudinaryPublicId",
                schema: "content",
                table: "materials",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloudinaryPublicId",
                schema: "content",
                table: "materials");
        }
    }
}
