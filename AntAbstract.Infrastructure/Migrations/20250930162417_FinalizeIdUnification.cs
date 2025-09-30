using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeIdUnification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewAssignments_Reviewers_ReviewerId",
                table: "ReviewAssignments");

            migrationBuilder.AlterColumn<string>(
                name: "ReviewerId",
                table: "ReviewAssignments",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewAssignments_AspNetUsers_ReviewerId",
                table: "ReviewAssignments",
                column: "ReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewAssignments_AspNetUsers_ReviewerId",
                table: "ReviewAssignments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReviewerId",
                table: "ReviewAssignments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewAssignments_Reviewers_ReviewerId",
                table: "ReviewAssignments",
                column: "ReviewerId",
                principalTable: "Reviewers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
