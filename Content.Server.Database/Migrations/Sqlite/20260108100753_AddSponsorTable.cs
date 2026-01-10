using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddSponsorTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sponsor",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sponsor_tier = table.Column<string>(type: "TEXT", nullable: false),
                    expiry_date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sponsor", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_sponsor_player_player_id",
                        column: x => x.user_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sponsor_sponsor_tier_is_active",
                table: "sponsor",
                columns: new[] { "sponsor_tier", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_sponsor_user_id",
                table: "sponsor",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sponsor");
        }
    }
}
