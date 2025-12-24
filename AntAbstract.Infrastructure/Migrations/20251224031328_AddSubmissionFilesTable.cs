using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionFilesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionFile_Submissions_SubmissionId",
                table: "SubmissionFile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubmissionFile",
                table: "SubmissionFile");

            migrationBuilder.RenameTable(
                name: "SubmissionFile",
                newName: "SubmissionFiles");

            migrationBuilder.RenameIndex(
                name: "IX_SubmissionFile_SubmissionId",
                table: "SubmissionFiles",
                newName: "IX_SubmissionFiles_SubmissionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubmissionFiles",
                table: "SubmissionFiles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionFiles_Submissions_SubmissionId",
                table: "SubmissionFiles",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionFiles_Submissions_SubmissionId",
                table: "SubmissionFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubmissionFiles",
                table: "SubmissionFiles");

            migrationBuilder.RenameTable(
                name: "SubmissionFiles",
                newName: "SubmissionFile");

            migrationBuilder.RenameIndex(
                name: "IX_SubmissionFiles_SubmissionId",
                table: "SubmissionFile",
                newName: "IX_SubmissionFile_SubmissionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubmissionFile",
                table: "SubmissionFile",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionFile_Submissions_SubmissionId",
                table: "SubmissionFile",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
