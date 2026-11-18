using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using JQuery_ECommerceAss.Data;

#nullable disable

namespace JQuery_ECommerceAss.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260309000000_AddCheckoutAttempt")]
public partial class AddCheckoutAttempt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CheckoutAttempts",
            columns: table => new
            {
                CheckoutAttemptID = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RequestId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                UserID = table.Column<int>(type: "int", nullable: false),
                DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CheckoutAttempts", x => x.CheckoutAttemptID);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CheckoutAttempts_RequestId",
            table: "CheckoutAttempts",
            column: "RequestId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CheckoutAttempts");
    }
}
