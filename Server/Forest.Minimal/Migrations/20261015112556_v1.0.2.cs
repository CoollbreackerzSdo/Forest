using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forest.Minimal.Migrations
{
    /// <inheritdoc />
    public partial class v102 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Details",
                table: "users");

            migrationBuilder.CreateTable(
                name: "user_details",
                columns: table => new
                {
                    UserModelKey = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    photo_id = table.Column<string>(type: "text", nullable: true),
                    banner_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_details", x => x.UserModelKey);
                    table.ForeignKey(
                        name: "FK_user_details_users_UserModelKey",
                        column: x => x.UserModelKey,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_details");

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "users",
                type: "jsonb",
                nullable: true);
        }
    }
}
