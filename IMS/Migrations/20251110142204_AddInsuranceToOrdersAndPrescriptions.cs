using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Migrations
{
    /// <inheritdoc />
    public partial class AddInsuranceToOrdersAndPrescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppliedCoveragePercentage",
                table: "Prescriptions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerPayableAmount",
                table: "Prescriptions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceCoverageAmount",
                table: "Prescriptions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InsurancePolicyId",
                table: "Prescriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedCoveragePercentage",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerPayableAmount",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceCoverageAmount",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "InsurancePolicyId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_InsurancePolicyId",
                table: "Prescriptions",
                column: "InsurancePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_InsurancePolicyId",
                table: "Orders",
                column: "InsurancePolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_InsurancePolicies_InsurancePolicyId",
                table: "Orders",
                column: "InsurancePolicyId",
                principalTable: "InsurancePolicies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_InsurancePolicies_InsurancePolicyId",
                table: "Prescriptions",
                column: "InsurancePolicyId",
                principalTable: "InsurancePolicies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_InsurancePolicies_InsurancePolicyId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_InsurancePolicies_InsurancePolicyId",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_InsurancePolicyId",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Orders_InsurancePolicyId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AppliedCoveragePercentage",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "CustomerPayableAmount",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "InsuranceCoverageAmount",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "InsurancePolicyId",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "AppliedCoveragePercentage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerPayableAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InsuranceCoverageAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InsurancePolicyId",
                table: "Orders");
        }
    }
}
