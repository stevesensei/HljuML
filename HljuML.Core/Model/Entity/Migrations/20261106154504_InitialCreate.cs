using System;
using ColorMC.Core.Objs;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HljuML.Core.Model.Entity.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomAuthServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    RegisterAddress = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAuthServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthServerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PlayerName = table.Column<string>(type: "TEXT", nullable: false),
                    LoginCache = table.Column<string>(type: "TEXT", nullable: true),
                    LoginCacheHash = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerProfiles_CustomAuthServers_AuthServerId",
                        column: x => x.AuthServerId,
                        principalTable: "CustomAuthServers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GameInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Avatar = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    Loader = table.Column<int>(type: "INTEGER", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedPlayerId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameInstances_PlayerProfiles_LastUsedPlayerId",
                        column: x => x.LastUsedPlayerId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameInstances_LastUsedPlayerId",
                table: "GameInstances",
                column: "LastUsedPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_AuthServerId",
                table: "PlayerProfiles",
                column: "AuthServerId");

            migrationBuilder.InsertData(
                table: "GameInstances",
                columns: new[] { "Id", "Name", "Avatar", "Version", "Loader", "LastLogin" },
                values: new object[]
                {
                    new Guid("728a591d-be79-49f6-96a0-a1e7e630c8b1"),
                    "原版 26.2（测试）",
                    string.Empty,
                    "26.2",
                    (int)Loaders.Normal,
                    DateTime.MinValue
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameInstances");

            migrationBuilder.DropTable(
                name: "PlayerProfiles");

            migrationBuilder.DropTable(
                name: "CustomAuthServers");
        }
    }
}
