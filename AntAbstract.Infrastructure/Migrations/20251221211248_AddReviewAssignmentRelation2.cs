using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewAssignmentRelation2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_ReviewAssignmentId",
                table: "Reviews");

            migrationBuilder.AlterColumn<int>(
                name: "ReviewAssignmentId",
                table: "Reviews",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewAssignmentId",
                table: "Reviews",
                column: "ReviewAssignmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_ReviewAssignmentId",
                table: "Reviews");

            migrationBuilder.AlterColumn<int>(
                name: "ReviewAssignmentId",
                table: "Reviews",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewAssignmentId",
                table: "Reviews",
                column: "ReviewAssignmentId",
                unique: true,
                filter: "[ReviewAssignmentId] IS NOT NULL");
        }
    }
}
