using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMonHoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mon_hoc",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TinChi = table.Column<int>(type: "int", nullable: false),
                    GiaoVienId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mon_hoc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mon_hoc_users_GiaoVienId",
                        column: x => x.GiaoVienId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mon_hoc_lop",
                schema: "auth",
                columns: table => new
                {
                    MonHocId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mon_hoc_lop", x => new { x.MonHocId, x.LopId });
                    table.ForeignKey(
                        name: "FK_mon_hoc_lop_lop_LopId",
                        column: x => x.LopId,
                        principalSchema: "auth",
                        principalTable: "lop",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mon_hoc_lop_mon_hoc_MonHocId",
                        column: x => x.MonHocId,
                        principalSchema: "auth",
                        principalTable: "mon_hoc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mon_hoc_GiaoVienId",
                schema: "auth",
                table: "mon_hoc",
                column: "GiaoVienId");

            migrationBuilder.CreateIndex(
                name: "IX_mon_hoc_lop_LopId",
                schema: "auth",
                table: "mon_hoc_lop",
                column: "LopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mon_hoc_lop",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "mon_hoc",
                schema: "auth");
        }
    }
}
