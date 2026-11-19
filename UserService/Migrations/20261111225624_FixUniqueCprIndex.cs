
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Migrations
{
    public partial class FixUniqueCprIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Limit CPR to 11 characters (including an optional hyphen).
            migrationBuilder.AlterColumn<string>(
                name: "CPR",
                table: "Users",
                type: "varchar(11)",
                maxLength: 11,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true);

            // Prevent two profiles from having the same CPR value.
            migrationBuilder.CreateIndex(
                name: "IX_Users_CPR",
                table: "Users",
                column: "CPR",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the unique CPR index.
            migrationBuilder.DropIndex(
                name: "IX_Users_CPR",
                table: "Users");

            // Restore the previous CPR column type.
            migrationBuilder.AlterColumn<string>(
                name: "CPR",
                table: "Users",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(11)",
                oldMaxLength: 11,
                oldNullable: true);
        }
    }
}
