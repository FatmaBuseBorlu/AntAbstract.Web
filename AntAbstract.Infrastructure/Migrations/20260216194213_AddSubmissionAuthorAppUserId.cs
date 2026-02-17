using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionAuthorAppUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SubmissionAuthors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppUserId",
                table: "SubmissionAuthors",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionAuthors_AppUserId",
                table: "SubmissionAuthors",
                column: "AppUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAuthors_AspNetUsers_AppUserId",
                table: "SubmissionAuthors",
                column: "AppUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAuthors_AspNetUsers_AppUserId",
                table: "SubmissionAuthors");

            migrationBuilder.DropIndex(
                name: "IX_SubmissionAuthors_AppUserId",
                table: "SubmissionAuthors");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "SubmissionAuthors");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SubmissionAuthors",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
