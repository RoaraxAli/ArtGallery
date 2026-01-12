using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Migrations
{

    public partial class fg1625252 : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AuctionEndTime",
                table: "products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BidCount",
                table: "products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBid",
                table: "products",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAuction",
                table: "products",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuctionEndTime",
                table: "products");

            migrationBuilder.DropColumn(
                name: "BidCount",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CurrentBid",
                table: "products");

            migrationBuilder.DropColumn(
                name: "IsAuction",
                table: "products");
        }
    }
}
