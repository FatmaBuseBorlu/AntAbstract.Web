# AntAbstract — Manuel Test Senaryoları

**Versiyon:** 1.0 | **Tarih:** Haziran 2026 | **Toplam:** 12 Bölüm, 85+ Test Adımı

> **Ön Koşullar:** DB migration uygulanmış, en az 1 kongre + 1 kayıt türü mevcut.
> **Gerekli Roller:** SuperAdmin, Admin, Author, Referee, Listener hesapları hazır.
> **Harici Servisler:** Stripe/PayTR/SMTP/DataCite key yoksa ilgili testler atlanır.

---

## 1. Kayıt ve Giriş

### 1.1 Yeni Kullanıcı Kayıt

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | `/` adresine git | Anasayfa yüklenir, navbar görünür | | |
| 2 | Kayıt Ol butonuna tıkla | Kayıt formu açılır | | |
| 3 | Ad, soyad, email, şifre gir (min 8 karakter, büyük harf, rakam) | Form validasyonu geçer | | |
| 4 | Kayıt Ol butonuna bas | Başarı mesajı + login sayfasına yönlendirilir | | |
| 5 | Aynı email ile tekrar kayıt dene | Hata: Bu e-posta adresi zaten kayıtlı | | |
| 6 | Zayıf şifre dene (123456) | Validasyon hatası gösterilir | | |

### 1.2 Giriş

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | `/login` sayfasına git | Login formu görünür | | |
| 2 | Doğru email + şifre gir | Dashboard'a yönlendirilir | | |
| 3 | Yanlış şifre ile 5 kez dene | Hesap 15 dk kilitlenir, uyarı görünür | | |
| 4 | Şifremi Unuttum linkine tıkla | Email gönderim formu açılır | | |
| 5 | Geçerli email ile sıfırlama iste | Sıfırlama maili gönderilir (SMTP aktifse) | | |

### 1.3 İki Faktörlü Doğrulama (2FA)

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Profil > Güvenlik > 2FA sayfasına git | 2FA durumu görünür (Kapalı) | | |
| 2 | Authenticator Ekle butonuna bas | QR kod + anahtar görünür | | |
| 3 | Google Authenticator ile QR tara, kodu gir | 2FA etkinleştirilir, kurtarma kodları gösterilir | | |
| 4 | Çıkış yap ve tekrar giriş dene | Authenticator kodu istenir | | |

---

## 2. Kongre Yönetimi (Admin)

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Admin panel > Kongreler > Yeni Kongre Oluştur | Kongre formu açılır | | |
| 2 | Başlık, tarih, şehir, slug gir, kaydet | Kongre oluşturulur, listeye döner | | |
| 3 | Kongre Düzenle: bilgileri değiştir | Değişiklikler kaydedilir | | |
| 4 | Kayıt Türleri > Yeni kayıt türü ekle | Kayıt türü oluşturulur | | |
| 5 | Bildiri Gönderimini Aç/Kapat toggle | Durum değişir, public sayfada yansır | | |
| 6 | Kongre Akışı sayfasını kontrol et | Tüm aşamalar görünür | | |

---

## 3. Bildiri Gönderimi ve Değerlendirme

### 3.1 Bildiri Gönderme (Yazar)

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | `/{slug}/submit-abstract` adresine git | Bildiri gönderim formu açılır | | |
| 2 | Başlık, özet, anahtar kelime, konu gir | Validasyon geçer | | |
| 3 | PDF dosyası yükle (drag & drop) | Dosya yüklenir, ilerleme görünür | | |
| 4 | Gönder butonuna bas | Bildiri oluşturulur, başarı mesajı | | |
| 5 | Bildirilerim sayfasında görüntüle | Bildiri "Yeni" durumunda listelenir | | |
| 6 | 10 MB üstü dosya yükle | Hata: Dosya boyutu çok büyük | | |
| 7 | .exe dosyası yükle | Hata: Geçersiz dosya türü | | |

### 3.2 Hakem Atama (Admin)

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Admin > Hakem Atamaları sayfasına git | Bildiri listesi görünür | | |
| 2 | Bir bildiriye hakem ata | Hakem atanır, bildiri "İncelemede" olur | | |
| 3 | Toplu hakem atama: checkbox ile seç + ata | Seçilen tüm bildirilere hakem atanır | | |
| 4 | Atanan hakeme bildirim geldi mi kontrol et | SignalR anlık bildirim görünür | | |

### 3.3 Değerlendirme (Hakem)

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Hakem hesabıyla giriş yap | Hakem dashboard görünür | | |
| 2 | Atanan bildiriyi görüntüle | Bildiri detayı ve dosyalar görünür | | |
| 3 | Kör hakemlik: dosya adı anonim mi? | Dosya adı: `submission-XXXXXXXX.pdf` | | |
| 4 | PDF metadata kontrol et (Author alanı) | Author boş, Creator: AntAbstract | | |
| 5 | Değerlendirme formunu doldur, puan ver | Değerlendirme kaydedilir | | |

### 3.4 Karar (Admin)

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Admin > Karar Ekranına git | Bildiriler ve hakem puanları görünür | | |
| 2 | Bir bildiriyi Kabul Et | Durum "Kabul Edildi", yazara email + bildirim | | |
| 3 | Bir bildiriyi Reddet | Durum "Reddedildi", yazara email | | |
| 4 | Toplu karar: checkbox ile seç + Kabul | Tüm seçilenler kabul edilir | | |
| 5 | Yazar: Rebuttal gönder (revizyon durumunda) | Yanıt kaydedilir, admin görüntüler | | |

---

## 4. Ödeme Akışı

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Kongre kaydı oluştur | Kayıt oluşur, ödeme bekleniyor | | |
| 2 | Ödeme sayfasına git, Stripe seç | Stripe Checkout açılır (key varsa) | | |
| 3 | Stripe yoksa PayTR seç | PayTR iframe açılır (key varsa) | | |
| 4 | Banka Havalesi seç, makbuz yükle | Makbuz yüklenir, admin onayı beklenir | | |
| 5 | Admin: Makbuzu onayla | Kayıt "Onaylanmış" olur, kullanıcıya email | | |
| 6 | Admin: Ödemeyi iade et | Kayıt iptal, kullanıcıya email + bildirim | | |
| 7 | Webhook log sayfasını kontrol et | Stripe + PayTR callback'ler görünür | | |

---

## 5. Kongre Programı ve Canlı Yayın

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Admin > Oturum Yönetimi > Yeni Oturum | Oturum formu açılır | | |
| 2 | Başlık, tarih, saat, salon, Zoom linki gir | Oturum oluşur | | |
| 3 | Sürükle-bırak ile oturum sırasını değiştir | Sıralama kaydedilir | | |
| 4 | `/{slug}/Program` sayfasına git | Program günlere göre listelenir | | |
| 5 | Canlı Yayın linkine tıkla | Zoom/Teams linki yeni sekmede açılır | | |
| 6 | `/{slug}/Program/Posters` sayfasına git | Poster galerisi kart düzeninde görünür | | |

---

## 6. DOI Entegrasyonu

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Kabul edilen bildiri detayına git (Admin) | DOI kartı görünür | | |
| 2 | Metadata Hazırla butonuna bas | Önerilen DOI URL görünür | | |
| 3 | Manuel DOI URL gir ve Kaydet | DOI atanır, link görünür | | |
| 4 | DataCite Otomatik Kaydet (config varsa) | DOI DataCite'a kaydedilir | | |
| 5 | Yazar bildiri detayında DOI butonunu gör | DOI linki tıklanabilir | | |
| 6 | Reddedilmiş bildiriye DOI atamayı dene | Hata mesajı | | |

---

## 7. İntihal Kontrolü

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Bildiri detayında İntihal bölümünü bul | İntihal kartı görünür | | |
| 2 | Tam metin varsa Başlat butonuna bas | Kontrol başlatılır (API key varsa) | | |
| 3 | API key yoksa | "Servis yapılandırılmamış" mesajı | | |
| 4 | Sonuç hazırsa Güncelle butonuna bas | %skor ve rapor linki görünür | | |

---

## 8. Sertifika ve Yaka Kartı

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Admin > Sertifikalar > Toplu Oluştur | Sertifikalar üretilir, bildirim gönderilir | | |
| 2 | Kullanıcı: Sertifikalarım sayfasına git | Sertifika listelenir, PDF indir | | |
| 3 | Admin > Yaka Kartı Baskı sayfasına git | Yaka kartları QR kodlu görünür | | |
| 4 | QR kodu telefon ile tara | Check-in token okunur | | |
| 5 | Admin > QR Scan sayfasında token gir | Check-in başarılı, isim görünür | | |
| 6 | Aynı QR ile tekrar check-in dene | Uyarı: Daha önce check-in yapılmış | | |

---

## 9. E-posta Yönetimi

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Admin > Email Şablonları sayfasına git | Şablon listesi görünür | | |
| 2 | Bir şablonu Düzenle | Split-pane editör açılır | | |
| 3 | Görsel mod butonuna bas | TinyMCE WYSIWYG editör yüklenir | | |
| 4 | Placeholder butonuna tıkla ({FullName}) | Placeholder editöre eklenir | | |
| 5 | Kaydet ve Test Gönder butonuna bas | Test maili admin'e gönderilir | | |
| 6 | Admin > Toplu E-posta: zamanlama ayarla | Broadcast oluşturulur | | |
| 7 | Zamanlanmış e-posta zamanı gelince | BroadcastWorker maili otomatik gönderir | | |

---

## 10. Kullanıcı Arayüzü ve Tema

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | Anasayfayı aç (/) | Kongre kartları görünür (max 3), arama yok | | |
| 2 | 3'ten fazla kongre varsa Tümünü Gör butonunu kontrol et | `/congresses` sayfasına yönlendirir | | |
| 3 | Mobil cihazda (375px) anasayfayı aç | Responsive: kartlar tek sütun, hamburger menü | | |
| 4 | Dashboard'da Dark Mode butonuna bas | Tema karanlık olur | | |
| 5 | Sayfa yenilemede tema korunuyor mu? | localStorage'dan tema yüklenir | | |
| 6 | Favicon kontrol et | Mavi kongre ikonu görünür (SVG) | | |
| 7 | Bildiri kitabı kartlarına tıkla (anasayfa) | Public Proceedings sayfasına gider | | |
| 8 | Türkçe/İngilizce dil değiştir | Tüm metinler değişir | | |

---

## 11. REST API

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | `POST /api/AuthApi/login` (email + şifre) | JWT token döner | | |
| 2 | `GET /api/ConferencesApi` | Kongre listesi JSON döner | | |
| 3 | `GET /api/ConferencesApi/{id}/program` | Oturum listesi döner | | |
| 4 | `GET /api/SubmissionsApi` (Bearer token) | Kullanıcının bildirileri döner | | |
| 5 | `GET /api/ProfileApi/me` (Bearer token) | Profil bilgileri döner | | |
| 6 | `GET /api/ProfileApi/notifications` | Bildirim listesi döner | | |
| 7 | Token olmadan `/api/SubmissionsApi` | 401 Unauthorized | | |
| 8 | 6 kez hızlı login dene | 429 Too Many Requests | | |
| 9 | `/swagger` (development) | Swagger UI açılır | | |

---

## 12. Güvenlik Kontrolleri

| # | Test Adımı | Beklenen Sonuç | ✅ | ❌ |
|---|-----------|----------------|---|---|
| 1 | `/Admin/*` sayfalarına login olmadan eriş | Login'e yönlendirilir | | |
| 2 | Author hesabıyla Admin paneline eriş | Erişim reddedilir (403) | | |
| 3 | Başka kullanıcının dosyasını indirmeyi dene | Erişim reddedilir (403) | | |
| 4 | URL'de path traversal dene | BadRequest veya 404 | | |
| 5 | XSS: bildiri başlığına `<script>` yaz | HTML encode edilir, çalışmaz | | |
| 6 | Response header'ları kontrol et | X-Frame-Options, CSP, HSTS mevcut | | |
| 7 | Health endpoint'e API key olmadan eriş | 401 Unauthorized | | |
| 8 | Webhook callback hashı yanlış gönder | PAYTR_INVALID_HASH, log'a yazılır | | |

---

## Test Notları

- **Test Ortamı:** localhost veya staging. Production'da test yapılmamalı.
- **Harici servisler yoksa:** Stripe/PayTR/SMTP/DataCite testleri atlanır, ilgili satıra "N/A" yazılır.
- **Hata bulunursa:** Beklenen/gerçek sonucu ve ekran görüntüsünü not edin.
- **Tarayıcılar:** Chrome + Safari + mobil Safari ile test edilmeli.
