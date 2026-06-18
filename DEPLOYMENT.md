# AntAbstract.Web — Deployment Rehberi

## Genel Akış

```
Local: ./deploy.sh → deploy_output/ → FTP → Plesk
Sunucu: migration → restart
```

---

## 1. Local — Publish Paketi Oluştur

```bash
./deploy.sh
```

Çıktı: `deploy_output/` klasörü (repoya girmez, her seferinde yeniden oluşturulur).

---

## 2. Plesk — Deployment Variables Ayarla

`appsettings.Production.json` içindeki `#{TOKEN}#` değerleri Plesk tarafından
otomatik olarak değiştirilir. Plesk panelinde şu değerleri tanımla:

**Domains → domain.com → Deployment → Variables**

| Token | Açıklama |
|-------|----------|
| `PRODUCTION_CONNECTION_STRING` | PostgreSQL/MSSQL bağlantı dizesi |
| `STRIPE_PUBLISHABLE_KEY` | Stripe public key |
| `STRIPE_SECRET_KEY` | Stripe secret key |
| `STRIPE_WEBHOOK_SECRET` | Stripe webhook signing secret |
| `SMTP_SERVER` | SMTP sunucu adresi |
| `SMTP_USERNAME` | SMTP kullanıcı adı |
| `SMTP_PASSWORD` | SMTP şifre |
| `ORCID_CLIENT_ID` | ORCID OAuth client ID |
| `ORCID_CLIENT_SECRET` | ORCID OAuth client secret |

> **Not:** Plesk bu token'ları dosya yüklendikten sonra değiştirir.
> Elle yükleme yapıyorsan `appsettings.Production.json` dosyasını
> sunucuda doğrudan düzenle — git'e **asla** gerçek değerleri commit etme.

---

## 3. Sunucu — Dosyaları Yükle

`deploy_output/` içindeki **tüm dosyaları** Plesk File Manager veya FTP ile
sunucunun httpdocs klasörüne yükle. Mevcut dosyaların üzerine yaz.

```
/var/www/vhosts/<domain>/httpdocs/
```

---

## 4. Sunucu — Migration Çalıştır

Uygulama bekleyen migration varsa **başlamayı reddeder**. SSH ile:

```bash
cd /var/www/vhosts/<domain>/httpdocs

# Seçenek A — Startup'ta migrate (Program.cs'e eklenirse):
dotnet AntAbstract.Web.dll

# Seçenek B — EF CLI ile (önerilen):
export ConnectionStrings__Default="Server=...;Database=...;..."
dotnet ef database update \
  --project AntAbstract.Infrastructure \
  --startup-project AntAbstract.Web
```

---

## 5. Plesk — Uygulamayı Restart Et

Plesk panelinde:
`Domains → domain.com → .NET → Restart`

veya SSH:

```bash
touch /var/www/vhosts/<domain>/httpdocs/app_offline.htm
sleep 2
rm /var/www/vhosts/<domain>/httpdocs/app_offline.htm
```

---

## Local Geliştirme

```bash
# Connection string ve secrets için:
cd AntAbstract.Web
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=AntAbstract;..."
dotnet user-secrets set "Email:SmtpServer" "smtp.example.com"
# ... vb.

# Çalıştır:
dotnet run
```

`appsettings.Development.json` zaten `SET_VIA_USER_SECRETS` değerlerine
işaret ediyor — user-secrets dışında hiçbir yere gerçek credential yazma.

---

## Kontrol Listesi — Her Deploy Öncesi

- [ ] `dotnet build -c Release` — hatasız build
- [ ] `dotnet test` — testler geçiyor
- [ ] `./deploy.sh` çıktısında "Bekleyen migration yok" veya migration hazır
- [ ] Plesk deployment variables güncel
- [ ] `deploy_output/` git'e commit edilmedi (`.gitignore`'da)
