using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicReviewFormsModule1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_ReviewAssignments_AssignmentId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_AssignmentId",
                table: "Reviews");

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewAssignmentId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDate",
                table: "Reviews",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewAssignmentId",
                table: "Reviews",
                column: "ReviewAssignmentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_ReviewAssignments_ReviewAssignmentId",
                table: "Reviews",
                column: "ReviewAssignmentId",
                principalTable: "ReviewAssignments",
                principalColumn: "ReviewAssignmentId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_ReviewAssignments_ReviewAssignmentId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ReviewAssignmentId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ReviewAssignmentId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ReviewDate",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AssignmentId",
                table: "Reviews",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_ReviewAssignments_AssignmentId",
                table: "Reviews",
                column: "AssignmentId",
                principalTable: "ReviewAssignments",
                principalColumn: "ReviewAssignmentId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
