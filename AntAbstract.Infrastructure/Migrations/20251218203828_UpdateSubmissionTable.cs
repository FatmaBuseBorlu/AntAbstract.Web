using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubmissionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegistrationTypes_Conferences_ConferenceId",
                table: "RegistrationTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewAssignments_AspNetUsers_ReviewerId",
                table: "ReviewAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewAssignments_Reviews_ReviewId",
                table: "ReviewAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_AspNetUsers_AuthorId",
                table: "Submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Sessions_SessionId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_SessionId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AbstractText",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "FinalDecision",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Submissions");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Submissions",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "SubmissionId",
                table: "Submissions",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Submissions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Keywords",
                table: "Submissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Abstract",
                table: "Submissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Submissions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReviewId",
                table: "ReviewAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrationTypes_Conferences_ConferenceId",
                table: "RegistrationTypes",
                column: "ConferenceId",
                principalTable: "Conferences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewAssignments_AspNetUsers_ReviewerId",
                table: "ReviewAssignments",
                column: "ReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewAssignments_Reviews_ReviewId",
                table: "ReviewAssignments",
                column: "ReviewId",
                principalTable: "Reviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_AspNetUsers_AuthorId",
                table: "Submissions",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegistrationTypes_Conferences_ConferenceId",
                table: "RegistrationTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewAssignments_AspNetUsers_ReviewerId",
                table: "ReviewAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewAssignments_Reviews_ReviewId",
                table: "ReviewAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_AspNetUsers_AuthorId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Abstract",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Submissions");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "Submissions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Submissions",
                newName: "SubmissionId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Submissions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Keywords",
                table: "Submissions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AbstractText",
                table: "Submissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "Submissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalDecision",
                table: "Submissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Submissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReviewId",
                table: "ReviewAssignments",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SessionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Conferences_ConferenceId",
                        column: x => x.ConferenceId,
                        principalTable: "Conferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_SessionId",
                table: "Submissions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ConferenceId",
                table: "Sessions",
                column: "ConferenceId");

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrationTypes_Conferences_ConferenceId",
                table: "RegistrationTypes",
                column: "ConferenceId",
                principalTable: "Conferences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewAssignments_AspNetUsers_ReviewerId",
                table: "ReviewAssignments",
                column: "ReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewAssignments_Reviews_ReviewId",
                table: "ReviewAssignments",
                column: "ReviewId",
                principalTable: "Reviews",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_AspNetUsers_AuthorId",
                table: "Submissions",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Sessions_SessionId",
                table: "Submissions",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id");
        }
    }
}
