using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AntAbstract.Infrastructure.Migrations
{
    /// <summary>
    /// Submissions.AdminDecisionNote sütunu model tarafında tanımlı ama hiçbir
    /// migration onu oluşturmuyordu: 20260615131944_AddSubmissionAdminDecisionNote
    /// üretilmiş, içi boş bırakılmış. Üretim veritabanında sütun elle eklendiği
    /// için sorun görünmüyordu; sıfırdan kurulan her veritabanında ise bildiri
    /// gönderimi "Invalid column name 'AdminDecisionNote'" ile kırılıyordu.
    /// Yerelde birebir yaşandı.
    ///
    /// Model anlık görüntüsü sütunu zaten içerdiği için "migrations add" boş
    /// dosya üretiyor; bu yüzden elle yazıldı.
    ///
    /// Koşullu çalışıyor: sütun yoksa ekliyor, varsa hiçbir şey yapmıyor.
    /// Böylece sütunun elle eklendiği üretim veritabanında da güvenle koşuyor.
    /// </summary>
    public partial class AddMissingAdminDecisionNoteColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Submissions', 'AdminDecisionNote') IS NULL
BEGIN
    ALTER TABLE [Submissions] ADD [AdminDecisionNote] nvarchar(max) NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('Submissions', 'AdminDecisionNote') IS NOT NULL
BEGIN
    ALTER TABLE [Submissions] DROP COLUMN [AdminDecisionNote];
END
");
        }
    }
}
