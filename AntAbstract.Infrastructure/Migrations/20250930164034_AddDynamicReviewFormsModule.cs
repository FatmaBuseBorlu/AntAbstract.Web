using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicReviewFormsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewForms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewForms_Conferences_ConferenceId",
                        column: x => x.ConferenceId,
                        principalTable: "Conferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CriterionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewCriteria_ReviewForms_FormId",
                        column: x => x.FormId,
                        principalTable: "ReviewForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    TextAnswer = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewAnswers_ReviewCriteria_CriterionId",
                        column: x => x.CriterionId,
                        principalTable: "ReviewCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewAnswers_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewAnswers_CriterionId",
                table: "ReviewAnswers",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewAnswers_ReviewId",
                table: "ReviewAnswers",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCriteria_FormId",
                table: "ReviewCriteria",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewForms_ConferenceId",
                table: "ReviewForms",
                column: "ConferenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewAnswers");

            migrationBuilder.DropTable(
                name: "ReviewCriteria");

            migrationBuilder.DropTable(
                name: "ReviewForms");
        }
    }
}
