using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    public partial class AddReviewAssignmentRelation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Önce yeni kolonu nullable olarak ekle (default 0 yok)
            migrationBuilder.AddColumn<int>(
                name: "ReviewAssignmentId",
                table: "Reviews",
                type: "int",
                nullable: true);

            // 2) Eski ilişkiden yeni ilişkiye veriyi taşı
            // ReviewAssignments.ReviewId -> Reviews.ReviewAssignmentId
            migrationBuilder.Sql(@"
UPDATE r
SET r.ReviewAssignmentId = ra.Id
FROM Reviews r
INNER JOIN ReviewAssignments ra ON ra.ReviewId = r.Id
");

            // 3) Unique index (SQL Server için null değerler sorun olmasın diye filtreli)
            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewAssignmentId",
                table: "Reviews",
                column: "ReviewAssignmentId",
                unique: true,
                filter: "[ReviewAssignmentId] IS NOT NULL");

            // 4) Yeni FK
            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_ReviewAssignments_ReviewAssignmentId",
                table: "Reviews",
                column: "ReviewAssignmentId",
                principalTable: "ReviewAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // 5) Eski FK ve kolon kaldırma (en sonda)
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewAssignments_Reviews_ReviewId",
                table: "ReviewAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ReviewAssignments_ReviewId",
                table: "ReviewAssignments");

            migrationBuilder.DropColumn(
                name: "ReviewId",
                table: "ReviewAssignments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski yapıyı geri kur
            migrationBuilder.AddColumn<int>(
                name: "ReviewId",
                table: "ReviewAssignments",
                type: "int",
                nullable: true);

            // Veriyi geri taşı
            migrationBuilder.Sql(@"
UPDATE ra
SET ra.ReviewId = r.Id
FROM ReviewAssignments ra
INNER JOIN Reviews r ON r.ReviewAssignmentId = ra.Id
");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewAssignments_ReviewId",
                table: "ReviewAssignments",
                column: "ReviewId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewAssignments_Reviews_ReviewId",
                table: "ReviewAssignments",
                column: "ReviewId",
                principalTable: "Reviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_ReviewAssignments_ReviewAssignmentId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ReviewAssignmentId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ReviewAssignmentId",
                table: "Reviews");
        }
    }
}
