using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConferenceSlugRequiredAndExternalUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conferences_Slug",
                table: "Conferences");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Conferences",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalWebsiteUrl",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conferences_Slug",
                table: "Conferences",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conferences_Slug",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "ExternalWebsiteUrl",
                table: "Conferences");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Conferences",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_Conferences_Slug",
                table: "Conferences",
                column: "Slug");
        }
    }
}
