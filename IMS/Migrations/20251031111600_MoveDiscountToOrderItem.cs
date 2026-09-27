using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Migrations
{
    /// <inheritdoc />
    public partial class MoveDiscountToOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // إزالة عمود DiscountPercentage من جدول Orders
            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "Orders");

            // إضافة عمود DiscountPercentage إلى جدول OrderItems
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "OrderItems",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // إزالة عمود DiscountPercentage من جدول OrderItems
            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "OrderItems");

            // إعادة إضافة عمود DiscountPercentage إلى جدول Orders
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "Orders",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
