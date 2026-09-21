using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Menulux.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReferenceAndCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNo",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"UPDATE ""Orders"" SET ""ReferenceNo"" = 'ORD-' || upper(left(replace(""Id""::text, '-', ''), 8));");

            migrationBuilder.Sql(@"
                UPDATE ""Orders"" o
                SET ""OrderNumber"" = n.rn
                FROM (
                    SELECT ""Id"", row_number() OVER (
                        PARTITION BY ""RestaurantId"", ""CreatedAt""::date ORDER BY ""CreatedAt"") AS rn
                    FROM ""Orders""
                ) n
                WHERE o.""Id"" = n.""Id"" AND o.""OrderNumber"" = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId_ReferenceNo",
                table: "Orders",
                columns: new[] { "RestaurantId", "ReferenceNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_RestaurantId_ReferenceNo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReferenceNo",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_RestaurantId",
                table: "Orders",
                column: "RestaurantId");
        }
    }
}
