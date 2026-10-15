using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forest.Minimal.Migrations
{
    /// <inheritdoc />
    public partial class v001 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: false),
                    register = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_enable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expire = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    refresh_expire = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens", x => x.id);
                    table.UniqueConstraint("AK_tokens_refresh_id", x => x.refresh_id);
                    table.ForeignKey(
                        name: "FK_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_tokens_expire",
                table: "tokens",
                column: "expire");

            migrationBuilder.CreateIndex(
                name: "IX_tokens_refresh_expire",
                table: "tokens",
                column: "refresh_expire");

            migrationBuilder.CreateIndex(
                name: "IX_tokens_user_id",
                table: "tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_register",
                table: "users",
                column: "register");

            migrationBuilder.CreateIndex(
                name: "IX_users_user_name",
                table: "users",
                column: "user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tokens");

            migrationBuilder.DropTable(
                name: "user_details");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
