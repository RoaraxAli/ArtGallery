using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Migrations
{

    public partial class fg1 : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CursorStyle",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CursorStyle",
                table: "Users");
        }
    }
}
