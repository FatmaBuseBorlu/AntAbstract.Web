using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDoiWorkflowColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Submissions') AND name = 'DoiStatus')
                BEGIN
                    ALTER TABLE [Submissions] ADD [DoiStatus] int NOT NULL DEFAULT 0;
                    ALTER TABLE [Submissions] ADD [DoiProvider] nvarchar(50) NULL;
                    ALTER TABLE [Submissions] ADD [DoiErrorMessage] nvarchar(1000) NULL;
                    ALTER TABLE [Submissions] ADD [DoiRequestedAt] datetime2 NULL;
                    ALTER TABLE [Submissions] ADD [DoiAssignedAt] datetime2 NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DoiAssignedAt", table: "Submissions");
            migrationBuilder.DropColumn(name: "DoiErrorMessage", table: "Submissions");
            migrationBuilder.DropColumn(name: "DoiProvider", table: "Submissions");
            migrationBuilder.DropColumn(name: "DoiRequestedAt", table: "Submissions");
            migrationBuilder.DropColumn(name: "DoiStatus", table: "Submissions");
        }
    }
}
