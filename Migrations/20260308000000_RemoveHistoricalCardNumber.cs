using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using JQuery_ECommerceAss.Data;

#nullable disable

namespace JQuery_ECommerceAss.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260308000000_RemoveHistoricalCardNumber")]
public partial class RemoveHistoricalCardNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CardNumber",
            table: "TransactionHis");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CardNumber",
            table: "TransactionHis",
            type: "nvarchar(max)",
            nullable: true);
    }
}
