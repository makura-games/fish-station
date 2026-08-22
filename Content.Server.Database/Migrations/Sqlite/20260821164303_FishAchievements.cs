using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class FishAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fish_achievement_progress",
                columns: table => new
                {
                    fish_achievement_progress_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    achievement_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    progress = table.Column<int>(type: "INTEGER", nullable: false),
                    unlocked_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fish_achievement_progress", x => x.fish_achievement_progress_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fish_achievement_progress_player_user_id_achievement_id",
                table: "fish_achievement_progress",
                columns: new[] { "player_user_id", "achievement_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fish_achievement_progress");
        }
    }
}
