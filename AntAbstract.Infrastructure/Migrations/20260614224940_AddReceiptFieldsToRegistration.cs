using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptFieldsToRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminPaymentNote",
                table: "Registrations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptFilePath",
                table: "Registrations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptUploadedAt",
                table: "Registrations",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminPaymentNote",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "ReceiptFilePath",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "ReceiptUploadedAt",
                table: "Registrations");
        }
    }
}
