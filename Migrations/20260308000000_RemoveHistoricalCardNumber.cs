using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JQuery_ECommerceAss.Migrations;

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
