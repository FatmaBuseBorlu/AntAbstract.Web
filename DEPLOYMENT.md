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
Bu klasör içinde idempotent `migration.sql` dosyası da üretilir.

---

## 2. Plesk — Deployment Variables Ayarla

`appsettings.Production.json` içindeki `#{TOKEN}#` değerleri Plesk tarafından
otomatik olarak değiştirilir. Plesk panelinde şu değerleri tanımla:

**Domains → domain.com → Deployment → Variables**

| Token | Açıklama | Zorunlu |
|-------|----------|--------|
| `PRODUCTION_CONNECTION_STRING` | MSSQL bağlantı dizesi | ✅ |
| `STRIPE_PUBLISHABLE_KEY` | Stripe public key | Stripe kullanılıyorsa |
| `STRIPE_SECRET_KEY` | Stripe secret key | Stripe kullanılıyorsa |
| `STRIPE_WEBHOOK_SECRET` | Stripe webhook signing secret | Stripe kullanılıyorsa |
| `PAYTR_MERCHANT_ID` | PayTR merchant ID | PayTR kullanılıyorsa |
| `PAYTR_MERCHANT_KEY` | PayTR merchant key | PayTR kullanılıyorsa |
| `PAYTR_MERCHANT_SALT` | PayTR merchant salt | PayTR kullanılıyorsa |
| `PLAGIARISM_API_KEY` | iThenticate/Turnitin API key | İntihal kontrolü kullanılıyorsa |
| `SMTP_SERVER` | SMTP sunucu adresi | ✅ |
| `SMTP_USERNAME` | SMTP kullanıcı adı | ✅ |
| `SMTP_PASSWORD` | SMTP şifre | ✅ |
| `PUBLIC_BASE_URL` | Sitenin public URL'i (ör: https://antabstract.com.tr) | ✅ |
| `ORCID_CLIENT_ID` | ORCID OAuth client ID | ORCID kullanılıyorsa |
| `ORCID_CLIENT_SECRET` | ORCID OAuth client secret | ORCID kullanılıyorsa |
| `HEALTH_API_KEY` | Health endpoint API key | Opsiyonel |
| `JWT_SECRET_KEY` | JWT token imzalama anahtarı (min 32 karakter) | ✅ |
| `BOOTSTRAP_ADMIN_EMAIL` | İlk kurulumda oluşturulacak SuperAdmin e-postası | SuperAdmin yoksa gerekli |
| `BOOTSTRAP_ADMIN_PASSWORD` | İlk kurulum SuperAdmin şifresi (min 12 karakter) | SuperAdmin yoksa gerekli |
| `DOI_REPOSITORY_ID` | DataCite repository ID | DOI kullanılıyorsa |
| `DOI_PASSWORD` | DataCite API şifresi | DOI kullanılıyorsa |
| `DOI_PREFIX` | DOI prefix (ör: 10.12345) | DOI kullanılıyorsa |

> **Not:** Plesk bu token'ları dosya yüklendikten sonra değiştirir.
> Elle yükleme yapıyorsan `appsettings.Production.json` dosyasını
> sunucuda doğrudan düzenle — git'e **asla** gerçek değerleri commit etme.
>
> `BOOTSTRAP_ADMIN_*` değerleri yalnızca sistemde bu e-postaya sahip kullanıcı
> yoksa ilk SuperAdmin hesabını oluşturmak için kullanılır. Canlıya aldıktan
> sonra bu hesabın şifresini değiştir ve mümkünse bootstrap değişkenlerini kaldır.

---

## 3. Sunucu — Dosyaları Yükle

`deploy_output/` içindeki **tüm dosyaları** Plesk File Manager veya FTP ile
sunucunun httpdocs klasörüne yükle. Mevcut dosyaların üzerine yaz.

```
/var/www/vhosts/<domain>/httpdocs/
```

---

## 4. Sunucu — Migration SQL Uygula

> **ÖNEMLİ:** Uygulama bekleyen migration varsa **başlamayı reddeder**.
> Deploy sonrası migration uygulanmazsa site 500 verir.

Önerilen yol:

1. `deploy_output/migration.sql` dosyasını aç.
2. Plesk MSSQL yönetim aracı, SQL Server Management Studio veya hosting panelindeki SQL çalıştırma ekranında production veritabanına uygula.
3. İşlem başarılı olduktan sonra uygulamayı restart et.

Alternatif olarak sunucuda kaynak proje dosyaları da varsa EF CLI kullanılabilir:

```bash
export ConnectionStrings__Default="Server=...;Database=...;..."
dotnet ef database update --project AntAbstract.Infrastructure --startup-project AntAbstract.Web
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

- [ ] `dotnet build -c Release` — hatasız build, 0 warning
- [ ] `dotnet test` — tüm testler geçiyor
- [ ] `dotnet list package --vulnerable` — High/Critical yok (veya kabul edilir)
- [ ] `deploy_output/migration.sql` production veritabanına uygulandı
- [ ] Plesk deployment variables güncel (yeni token eklendi mi?)
- [ ] `deploy_output/` git'e commit edilmedi (`.gitignore`'da)

## İlk Deploy — Ek Adımlar

- [ ] MSSQL veritabanı oluşturuldu, connection string doğru
- [ ] `dotnet ef database update` ile tüm migration'lar uygulandı
- [ ] SMTP credential'ları test edildi (test maili gönder)
- [ ] PayTR TestMode **false** (production'da)
- [ ] Stripe webhook endpoint Stripe Dashboard'da kayıtlı: `https://<domain>/payment/stripe-webhook`
- [ ] PayTR callback URL'i PayTR panelinde kayıtlı: `https://<domain>/payment/paytr-callback`
- [ ] `private-uploads/` klasörü oluşturuldu ve yazma izni var
- [ ] `wwwroot/uploads/` klasörü oluşturuldu ve yazma izni var
- [ ] HTTPS sertifikası aktif (Let's Encrypt veya Plesk SSL)
- [ ] `Email:BaseUrl` production domain'e ayarlı (https://antabstract.com.tr)
- [ ] wkhtmltopdf Linux'ta kurulu: `sudo apt-get install wkhtmltopdf` (kabul/ret mektubu PDF için)

## Dikkat — Upload Dosyaları

`private-uploads/` ve `wwwroot/uploads/` klasörleri sunucudaki kullanıcı
verilerini içerir (bildiri dosyaları, makbuzlar, şablonlar). Deploy sırasında
bu klasörleri **asla silmeyin**. FTP/File Manager ile yükleme yaparken
mevcut dosyaların üzerine yazın ama `uploads/` klasörlerini temizlemeyin.

```bash
# Güvenli deploy: sadece uygulama dosyalarını kopyala, uploads dokunma
rsync -av --exclude='uploads/' deploy_output/ user@server:/var/www/vhosts/<domain>/httpdocs/
```
