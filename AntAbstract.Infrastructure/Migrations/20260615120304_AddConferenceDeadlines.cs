using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConferenceDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AbstractSubmissionDeadline",
                table: "Conferences",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FullTextSubmissionDeadline",
                table: "Conferences",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSubmissionOpen",
                table: "Conferences",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbstractSubmissionDeadline",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "FullTextSubmissionDeadline",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "IsSubmissionOpen",
                table: "Conferences");
        }
    }
}
