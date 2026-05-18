using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProceedingBookFieldsToConference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProceedingBookPublished",
                table: "Conferences",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProceedingBookFilePath",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProceedingBookPublishedDate",
                table: "Conferences",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProceedingBookPublished",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "ProceedingBookFilePath",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "ProceedingBookPublishedDate",
                table: "Conferences");
        }
    }
}
