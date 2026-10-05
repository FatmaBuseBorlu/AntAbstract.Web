using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Stripe Türkiye'deki şirketlere hesap açmıyor ve platformda Stripe
    /// anahtarı yok. "Stripe açık, PayTR kapalı" kongrelerde ödeme sayfası
    /// hiçbir kart seçeneği göstermiyordu (havale de kapalıysa "ödeme yöntemi
    /// tanımlanmamış"). Kartla ödeme isteyen bu kongrelerde kart PayTR'ye
    /// devredilir. Yalnızca havale seçmiş kongrelere dokunulmaz.
    /// </summary>
    public partial class CardPaymentsViaPayTR : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE Conferences SET IsPayTREnabled = 1, IsStripeEnabled = 0 " +
                "WHERE IsStripeEnabled = 1 AND IsPayTREnabled = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hangi satırların değiştiği tutulmadı; geri alınacak bir şey yok.
        }
    }
}
