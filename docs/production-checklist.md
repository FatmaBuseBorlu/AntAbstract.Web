# Production Deployment Checklist

Her deploy öncesi bu listeyi gözden geçirin.

---

## 1. Veritabanı

- [ ] `ConnectionStrings:Default` user-secrets veya environment variable ile set edildi
- [ ] DB şifresi rotasyonu yapıldı (eski: `Buse_531122` — **kullanmayın**)
- [ ] `dotnet ef database update` deployment pipeline'ında çalıştırıldı (startup'ta MigrateAsync yok)
- [ ] DB yedekleme planı aktif (günlük + her deploy öncesi manuel)
- [ ] Bağlantı havuzu limiti uygulama sunucu sayısına göre ayarlandı

## 2. SMTP / E-posta

- [ ] `Email:SmtpHost`, `Email:SmtpPort`, `Email:Username`, `Email:Password` user-secrets/env ile set edildi
- [ ] SMTP kimlik bilgileri rotasyonu yapıldı (eski: user `16dfba9e5f36b2` — **kullanmayın**)
- [ ] Test e-postası gönderildi ve alındı doğrulandı
- [ ] `Email:FromAddress` ve `Email:FromName` production değerleri girildi
- [ ] SPF / DKIM / DMARC DNS kayıtları domain'de mevcut

## 3. Stripe

- [ ] `Stripe:SecretKey` → production Secret Key (sk_live_...)
- [ ] `Stripe:PublishableKey` → production Publishable Key (pk_live_...)
- [ ] `Stripe:WebhookSecret` → production webhook imza sırrı
- [ ] Stripe Dashboard'da webhook endpoint URL production adresine güncellendi
- [ ] Test modu kapatıldı, canlı anahtarlar kullanılıyor
- [ ] Stripe event log (webhook) gözlemlendi, test isteği başarılı

## 4. ORCID OAuth

- [ ] `Authentication:ORCID:ClientId` → production ORCID client ID
- [ ] `Authentication:ORCID:ClientSecret` → production ORCID client secret
- [ ] `Authentication:ORCID:Authority` → `https://orcid.org` (sandbox değil)
- [ ] ORCID Developer Tools'ta Redirect URI production URL olarak kayıtlı
- [ ] OAuth login akışı test edildi

## 5. Storage / Dosya Yolları

- [ ] `private-uploads/` dizini uygulama `ContentRootPath` altında oluşturuldu
- [ ] Web sunucusu (IIS/nginx) `private-uploads/` dizinine **okuma+yazma** yetkisi var
- [ ] Web sunucusu `private-uploads/` dizinini **HTTP üzerinden serve etmiyor** (URL erişimi kapalı)
- [ ] Eski `wwwroot/uploads/{submissions,receipts,templates}` dosyaları `migrate-uploads.sh` ile taşındı
- [ ] DB'deki dosya yolları güncellendi (SQL migration scripti çalıştırıldı)
- [ ] `wwwroot/uploads/submissions`, `wwwroot/uploads/receipts`, `wwwroot/uploads/templates` klasörleri silindi veya 403 dönüyor

## 6. HTTPS / TLS

- [ ] SSL sertifikası geçerli ve süresi dolmamış (en az 30 gün kaldı)
- [ ] `app.UseHttpsRedirection()` aktif (Program.cs'te mevcut)
- [ ] HSTS aktif (`app.UseHsts()` production'da mevcut)
- [ ] HTTP → HTTPS yönlendirmesi web sunucusunda (nginx/IIS) da yapılandırıldı
- [ ] TLS 1.2+ zorunlu, TLS 1.0/1.1 devre dışı
- [ ] SSL Labs veya benzeri araçla A+ skoru doğrulandı

## 7. Güvenlik Header'ları

- [ ] `X-Frame-Options: SAMEORIGIN` response'larda mevcut
- [ ] `X-Content-Type-Options: nosniff` mevcut
- [ ] `Referrer-Policy: strict-origin-when-cross-origin` mevcut
- [ ] `Content-Security-Policy` yapılandırıldı ve test edildi
- [ ] `Permissions-Policy` yapılandırıldı

## 8. Şifre ve Kimlik Yönetimi

- [ ] Identity password policy production'da aktif (min 8 karakter, büyük harf, rakam)
- [ ] Hesap kilitleme aktif (5 deneme → 15 dk kilit)
- [ ] DataProtection anahtarları kalıcı depolamada (DB veya Azure Key Vault); sunucu yeniden başlayınca kaybolmuyor
- [ ] Cookie `Secure=true`, `HttpOnly=true`, `SameSite=Strict/Lax` ayarlandı

## 9. Loglama ve İzleme

- [ ] Production log seviyesi `Warning` veya üzeri (Debug loglar kapalı)
- [ ] Uygulama hata logları merkezi sistemde (Application Insights, Seq, Sentry vb.)
- [ ] Audit log tablosu DB'de mevcut ve yazılıyor
- [ ] Kritik hatalar için alert/bildirim yapılandırıldı

## 10. CI/CD

- [ ] `.github/workflows/ci.yml` aktif, PR'larda otomatik çalışıyor
- [ ] Tüm testler yeşil (37/37)
- [ ] Deployment pipeline'ında `dotnet publish -c Release` kullanılıyor
- [ ] `appsettings.Development.json` production sunucusunda **yok** veya .gitignore'da

## 11. Sunucu / Ortam

- [ ] `ASPNETCORE_ENVIRONMENT=Production` set edildi
- [ ] `ASPNETCORE_URLS` veya port doğru yapılandırıldı
- [ ] Rotativa (PDF) için `wkhtmltopdf` binary sunucuda mevcut ve yolda
- [ ] Uygulama havuzu / process monitor (systemd/supervisor/IIS App Pool) yapılandırıldı
- [ ] Sağlık kontrolü endpoint'i var ve load balancer'a tanımlı (varsa)

## 12. Son Kontrol

- [ ] Tüm user-secrets environment variable'a taşındı, kaynak kodda sır yok
- [ ] `git log` incelendi, commit geçmişinde sır bulunmuyor
- [ ] Smoke test çalıştırıldı: ana sayfa, login, bildiri gönder, ödeme
- [ ] Rollback planı hazır (önceki sürüm deploy edilebilir durumda)
