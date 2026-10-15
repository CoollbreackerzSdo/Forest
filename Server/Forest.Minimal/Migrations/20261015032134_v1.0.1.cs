using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forest.Minimal.Migrations
{
    /// <inheritdoc />
    public partial class v101 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "details",
                table: "users",
                newName: "Details");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Details",
                table: "users",
                newName: "details");
        }
    }
}
