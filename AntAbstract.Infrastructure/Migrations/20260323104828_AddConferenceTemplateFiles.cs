using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConferenceTemplateFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AbstractTemplatePath",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullTextTemplatePath",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WritingRulesPath",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbstractTemplatePath",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "FullTextTemplatePath",
                table: "Conferences");

            migrationBuilder.DropColumn(
                name: "WritingRulesPath",
                table: "Conferences");
        }
    }
}
