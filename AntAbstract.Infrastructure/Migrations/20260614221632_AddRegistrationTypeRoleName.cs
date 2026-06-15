using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationTypeRoleName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConferenceAttendances_AspNetUsers_UserId",
                table: "ConferenceAttendances");

            migrationBuilder.AddColumn<string>(
                name: "RoleName",
                table: "RegistrationTypes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Author");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "ConferenceAttendances",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TotalSeconds",
                table: "ConferenceAttendances",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "RequiredSeconds",
                table: "ConferenceAttendances",
                type: "int",
                nullable: false,
                defaultValue: 600,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "ConferenceAttendances",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ConferenceAttendances_AspNetUsers_UserId",
                table: "ConferenceAttendances",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConferenceAttendances_AspNetUsers_UserId",
                table: "ConferenceAttendances");

            migrationBuilder.DropColumn(
                name: "RoleName",
                table: "RegistrationTypes");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "ConferenceAttendances",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TotalSeconds",
                table: "ConferenceAttendances",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "RequiredSeconds",
                table: "ConferenceAttendances",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 600);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "ConferenceAttendances",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ConferenceAttendances_AspNetUsers_UserId",
                table: "ConferenceAttendances",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
