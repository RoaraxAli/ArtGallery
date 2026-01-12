using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Migrations
{

    public partial class fg162525225 : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HighestBidderId",
                table: "products",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighestBidderId",
                table: "products");
        }
    }
}
