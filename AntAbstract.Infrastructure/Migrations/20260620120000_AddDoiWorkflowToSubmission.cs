using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDoiWorkflowToSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DoiAssignedAt",
                table: "Submissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoiErrorMessage",
                table: "Submissions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoiProvider",
                table: "Submissions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DoiRequestedAt",
                table: "Submissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DoiStatus",
                table: "Submissions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoiAssignedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "DoiErrorMessage",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "DoiProvider",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "DoiRequestedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "DoiStatus",
                table: "Submissions");
        }
    }
}
