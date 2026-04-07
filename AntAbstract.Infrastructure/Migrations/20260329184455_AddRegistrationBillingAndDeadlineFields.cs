using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationBillingAndDeadlineFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "RegistrationTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "RegistrationTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BillingAddress",
                table: "Registrations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingName",
                table: "Registrations",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxNumber",
                table: "Registrations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxOffice",
                table: "Registrations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "RegistrationTypes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "RegistrationTypes");

            migrationBuilder.DropColumn(
                name: "BillingAddress",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "BillingName",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "TaxNumber",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "TaxOffice",
                table: "Registrations");
        }
    }
}
