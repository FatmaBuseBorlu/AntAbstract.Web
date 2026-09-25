using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <summary>
    /// SignIn.RequireConfirmedEmail açıldı. Bu ana kadar kayıt olan herkes
    /// doğrulanmış sayılıyordu; aralarında bayrağı false kalmış biri (eski
    /// sistemden aktarılan vb.) varsa bir sabah giriş yapamaz hale gelmesin.
    /// Yeni kayıtlar bundan sonra e-postayla doğrulanır.
    /// </summary>
    public partial class ExistingUsersEmailConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE AspNetUsers SET EmailConfirmed = 1 WHERE EmailConfirmed = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hangi satırların değiştiği tutulmadı; geri alınacak bir şey yok.
        }
    }
}
