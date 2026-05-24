using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateSignersToConferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CertificateFirstSignerName",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateFirstSignerTitle",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateSecondSignerName",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateSecondSignerTitle",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificateFirstSignerName",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "CertificateFirstSignerTitle",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "CertificateSecondSignerName",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "CertificateSecondSignerTitle",
                table: "Conferences");
        }
    }
}
