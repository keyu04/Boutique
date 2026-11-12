using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boutique.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingCostSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCostSnapshot",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingCostSnapshot",
                table: "Orders");
        }
    }
}
