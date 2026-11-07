using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HljuML.Core.Model.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddGameFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GameFolderId",
                table: "GameInstances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AbsolutePath = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameFolders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameInstances_GameFolderId",
                table: "GameInstances",
                column: "GameFolderId");

            migrationBuilder.AddForeignKey(
                name: "FK_GameInstances_GameFolders_GameFolderId",
                table: "GameInstances",
                column: "GameFolderId",
                principalTable: "GameFolders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameInstances_GameFolders_GameFolderId",
                table: "GameInstances");

            migrationBuilder.DropTable(
                name: "GameFolders");

            migrationBuilder.DropIndex(
                name: "IX_GameInstances_GameFolderId",
                table: "GameInstances");

            migrationBuilder.DropColumn(
                name: "GameFolderId",
                table: "GameInstances");
        }
    }
}
