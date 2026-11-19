using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class FishRpu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fish_rpu_profile",
                columns: table => new
                {
                    fish_rpu_profile_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    player_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    karma = table.Column<float>(type: "REAL", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fish_rpu_profile", x => x.fish_rpu_profile_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fish_rpu_profile_player_user_id",
                table: "fish_rpu_profile",
                column: "player_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fish_rpu_profile");
        }
    }
}
