using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// E-posta doğrulaması zorunlu sürüm canlıdayken SMTP boştu: doğrulama
    /// e-postası hiç gitmedi, o arada kayıt olanlar "doğrulanmamış" kaldı.
    /// Doğrulama artık yalnızca SMTP ayarlıyken zorunlu; SMTP sonradan
    /// kurulduğunda bu kişiler kilitlenmesin diye doğrulanmış sayılır.
    /// Silinmiş (anonimleştirilmiş) hesaplara dokunulmaz.
    /// </summary>
    public partial class ConfirmUsersStuckWithoutSmtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE AspNetUsers SET EmailConfirmed = 1 " +
                "WHERE EmailConfirmed = 0 AND (Email IS NULL OR Email NOT LIKE '%@deleted.antabstract.invalid');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hangi satırların değiştiği tutulmadı; geri alınacak bir şey yok.
        }
    }
}
