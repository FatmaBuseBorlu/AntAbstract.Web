using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAcademicFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAuthor_Submissions_SubmissionId",
                table: "SubmissionAuthor");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubmissionAuthor",
                table: "SubmissionAuthor");

            migrationBuilder.RenameTable(
                name: "SubmissionAuthor",
                newName: "SubmissionAuthors");

            migrationBuilder.RenameIndex(
                name: "IX_SubmissionAuthor_SubmissionId",
                table: "SubmissionAuthors",
                newName: "IX_SubmissionAuthors_SubmissionId");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TransferOptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "TransferOptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Submissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RoomTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "RoomTypes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Hotels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Hotels",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleScholarLink",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrcidId",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResearcherId",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AccommodationBookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "AccommodationBookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubmissionAuthors",
                table: "SubmissionAuthors",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAuthors_Submissions_SubmissionId",
                table: "SubmissionAuthors",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionAuthors_Submissions_SubmissionId",
                table: "SubmissionAuthors");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubmissionAuthors",
                table: "SubmissionAuthors");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TransferOptions");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "TransferOptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RoomTypes");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "RoomTypes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "GoogleScholarLink",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OrcidId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ResearcherId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AccommodationBookings");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "AccommodationBookings");

            migrationBuilder.RenameTable(
                name: "SubmissionAuthors",
                newName: "SubmissionAuthor");

            migrationBuilder.RenameIndex(
                name: "IX_SubmissionAuthors_SubmissionId",
                table: "SubmissionAuthor",
                newName: "IX_SubmissionAuthor_SubmissionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubmissionAuthor",
                table: "SubmissionAuthor",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionAuthor_Submissions_SubmissionId",
                table: "SubmissionAuthor",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
