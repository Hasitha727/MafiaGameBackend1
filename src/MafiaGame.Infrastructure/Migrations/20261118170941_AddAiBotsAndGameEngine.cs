using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MafiaGame.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiBotsAndGameEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBot",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BotPersonality",
                table: "RoomPlayers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsBot",
                table: "RoomPlayers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChatLanguage",
                table: "GameRooms",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Singlish");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBot",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BotPersonality",
                table: "RoomPlayers");

            migrationBuilder.DropColumn(
                name: "IsBot",
                table: "RoomPlayers");

            migrationBuilder.DropColumn(
                name: "ChatLanguage",
                table: "GameRooms");
        }
    }
}
