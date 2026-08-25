using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPersonalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoMonKhoa",
                schema: "auth",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CapBac",
                schema: "auth",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NamHoc",
                schema: "auth",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoDienThoai",
                schema: "auth",
                table: "users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoMonKhoa",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "CapBac",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "NamHoc",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SoDienThoai",
                schema: "auth",
                table: "users");
        }
    }
}
