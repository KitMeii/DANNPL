using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKhoaLopChucVu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassName",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Course",
                schema: "auth",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "ChucVu",
                schema: "auth",
                table: "users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Học viên");

            migrationBuilder.AddColumn<Guid>(
                name: "LopId",
                schema: "auth",
                table: "users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "khoa",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_khoa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lop",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KhoaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiaoVienId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lop", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lop_khoa_KhoaId",
                        column: x => x.KhoaId,
                        principalSchema: "auth",
                        principalTable: "khoa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lop_users_GiaoVienId",
                        column: x => x.GiaoVienId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_LopId",
                schema: "auth",
                table: "users",
                column: "LopId");

            migrationBuilder.CreateIndex(
                name: "IX_lop_GiaoVienId",
                schema: "auth",
                table: "lop",
                column: "GiaoVienId");

            migrationBuilder.CreateIndex(
                name: "IX_lop_KhoaId",
                schema: "auth",
                table: "lop",
                column: "KhoaId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_lop_LopId",
                schema: "auth",
                table: "users",
                column: "LopId",
                principalSchema: "auth",
                principalTable: "lop",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_lop_LopId",
                schema: "auth",
                table: "users");

            migrationBuilder.DropTable(
                name: "lop",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "khoa",
                schema: "auth");

            migrationBuilder.DropIndex(
                name: "IX_users_LopId",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ChucVu",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LopId",
                schema: "auth",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "ClassName",
                schema: "auth",
                table: "users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Course",
                schema: "auth",
                table: "users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
