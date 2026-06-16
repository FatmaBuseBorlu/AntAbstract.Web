using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Conferences",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AuditLogs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "AuditLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ConferenceId_Status",
                table: "Submissions",
                columns: new[] { "ConferenceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_TenantId_ConferenceId",
                table: "Submissions",
                columns: new[] { "TenantId", "ConferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewAssignments_SubmissionId_ReviewerId",
                table: "ReviewAssignments",
                columns: new[] { "SubmissionId", "ReviewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_ConferenceId_Status",
                table: "Registrations",
                columns: new[] { "ConferenceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Conferences_Slug",
                table: "Conferences",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Category",
                table: "AuditLogs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ConferenceId",
                table: "AuditLogs",
                column: "ConferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ConferenceId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "ConferenceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_ConferenceId_Status",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_TenantId_ConferenceId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_ReviewAssignments_SubmissionId_ReviewerId",
                table: "ReviewAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_ConferenceId_Status",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Conferences_Slug",
                table: "Conferences");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Category",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ConferenceId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ConferenceId_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Conferences",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
