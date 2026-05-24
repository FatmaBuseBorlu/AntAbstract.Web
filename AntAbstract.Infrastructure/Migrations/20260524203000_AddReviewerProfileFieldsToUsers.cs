using System;
using AntAbstract.Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260524203000_AddReviewerProfileFieldsToUsers")]
    public partial class AddReviewerProfileFieldsToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewerConflictInstitutions",
                table: "AspNetUsers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerConflictPeople",
                table: "AspNetUsers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewerUnavailableEndDate",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerUnavailableReason",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewerUnavailableStartDate",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewerConflictInstitutions",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReviewerConflictPeople",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReviewerUnavailableEndDate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReviewerUnavailableReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReviewerUnavailableStartDate",
                table: "AspNetUsers");
        }
    }
}
