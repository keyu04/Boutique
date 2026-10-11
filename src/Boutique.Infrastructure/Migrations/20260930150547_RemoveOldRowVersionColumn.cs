using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boutique.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldRowVersionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductVariants");
        }

        /// <inheritdoc />
       protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<byte[]>(
        name: "RowVersion",
        table: "ProductVariants",
        type: "bytea",
        nullable: false,
        defaultValue: Array.Empty<byte>());
}
    }
}
