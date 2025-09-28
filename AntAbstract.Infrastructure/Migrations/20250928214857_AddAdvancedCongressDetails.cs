using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedCongressDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CongressTypeId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScientificFieldId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CongressTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CongressTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScientificFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScientificFields", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CongressTypeId",
                table: "Tenants",
                column: "CongressTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ScientificFieldId",
                table: "Tenants",
                column: "ScientificFieldId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_CongressTypes_CongressTypeId",
                table: "Tenants",
                column: "CongressTypeId",
                principalTable: "CongressTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_ScientificFields_ScientificFieldId",
                table: "Tenants",
                column: "ScientificFieldId",
                principalTable: "ScientificFields",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_CongressTypes_CongressTypeId",
                table: "Tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_ScientificFields_ScientificFieldId",
                table: "Tenants");

            migrationBuilder.DropTable(
                name: "CongressTypes");

            migrationBuilder.DropTable(
                name: "ScientificFields");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_CongressTypeId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_ScientificFieldId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CongressTypeId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ScientificFieldId",
                table: "Tenants");
        }
    }
}
