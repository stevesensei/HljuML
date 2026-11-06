using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HljuML.Core.Model.Entity.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerProfileEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlayerEmail",
                table: "PlayerProfiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayerEmail",
                table: "PlayerProfiles");
        }
    }
}
