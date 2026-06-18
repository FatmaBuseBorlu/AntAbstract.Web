using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConferencePaymentConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankBranch",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankIban",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBankTransferEnabled",
                table: "Conferences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPayTREnabled",
                table: "Conferences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStripeEnabled",
                table: "Conferences",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAccountName",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "BankBranch",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "BankIban",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "IsBankTransferEnabled",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "IsPayTREnabled",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "IsStripeEnabled",
                table: "Conferences");
        }
    }
}
