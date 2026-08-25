using AntAbstract.Domain.Entities;
using AntAbstract.Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Infrastructure.Data
{
    /// <summary>
    /// Development ortamına zengin test verisi ekler.
    /// İdempotent: aynı slug/e-posta varsa atlar.
    /// </summary>
    public static class TestDataSeeder
    {
        private const string Password = "Test123!";

        public static async Task SeedAsync(
            UserManager<AppUser> userManager,
            AppDbContext db)
        {
            // ──────────────────────────────────────────────────────────────
            // 0. SystemParameter'lar (Title, University, Faculty, Department)
            // ──────────────────────────────────────────────────────────────
            await EnsureSystemParameters(db);

            // ──────────────────────────────────────────────────────────────
            // 1. Tenant
            // ──────────────────────────────────────────────────────────────
            var tenant = await db.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == "test-universitesi");

            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Slug = "test-universitesi",
                    Name = "Test Üniversitesi"
                };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
            }

            // ──────────────────────────────────────────────────────────────
            // 2. Kullanıcılar
            // ──────────────────────────────────────────────────────────────
            var admin = await EnsureUser(userManager, "admin@test.edu.tr", "Ahmet", "Yıldız", "Dr. Öğr. Üyesi", "Bilgisayar Müh.", "Test Üniversitesi", tenant.Id, "Admin");
            var author1 = await EnsureUser(userManager, "yazar1@test.edu.tr", "Fatma", "Demir", "Araş. Gör.", "Elektrik Müh.", "Test Üniversitesi", tenant.Id, "Author");
            var author2 = await EnsureUser(userManager, "yazar2@test.edu.tr", "Mehmet", "Kaya", "Dr.", "Makine Müh.", "Ankara Üniversitesi", null, "Author");
            var author3 = await EnsureUser(userManager, "yazar3@test.edu.tr", "Zeynep", "Arslan", "Prof. Dr.", "Kimya Müh.", "İTÜ", null, "Author");
            var reviewer1 = await EnsureUser(userManager, "hakem1@test.edu.tr", "Ali", "Çelik", "Doç. Dr.", "Bilgisayar Müh.", "Test Üniversitesi", tenant.Id, "Referee");
            var reviewer2 = await EnsureUser(userManager, "hakem2@test.edu.tr", "Ayşe", "Şahin", "Prof. Dr.", "Yazılım Müh.", "ODTÜ", null, "Referee");
            var listener = await EnsureUser(userManager, "dinleyici@test.edu.tr", "Emre", "Yılmaz", null, null, null, null, "Listener");

            // ──────────────────────────────────────────────────────────────
            // 3. Kongreler
            // ──────────────────────────────────────────────────────────────
            var now = DateTime.UtcNow;

            // --- Kongre A: Geçmiş, bildiri kitabı yayınlı ---
            var confA = await EnsureConference(db, tenant.Id, "bilisim-2023", conf =>
            {
                conf.Title = "Ulusal Bilişim Teknolojileri Sempozyumu 2023";
                conf.Description = "Yazılım, donanım ve siber güvenlik alanındaki güncel araştırmaların sunulduğu ulusal sempozyum.";
                conf.StartDate = now.AddMonths(-8);
                conf.EndDate = now.AddMonths(-8).AddDays(2);
                conf.City = "Ankara"; conf.Country = "Türkiye"; conf.Venue = "Hacettepe Kültür Merkezi";
                conf.IsSubmissionOpen = false; conf.IsRegistrationOpen = false;
                conf.IsProceedingBookPublished = true;
                conf.ProceedingBookFilePath = "/uploads/proceeding-books/bilisim-2023-bildiri-kitabi.pdf";
                conf.ProceedingBookPublishedDate = now.AddMonths(-7);
                conf.AbstractSubmissionDeadline = now.AddMonths(-9);
                conf.CertificateFirstSignerName = "Prof. Dr. Ahmet Yıldız";
                conf.CertificateFirstSignerTitle = "Kongre Başkanı";
                conf.CertificateSecondSignerName = "Doç. Dr. Ali Çelik";
                conf.CertificateSecondSignerTitle = "Bilim Kurulu Başkanı";
            });

            // --- Kongre B: Geçmiş, bildiri kitabı yayınlı ---
            var confB = await EnsureConference(db, tenant.Id, "yapay-zeka-2024", conf =>
            {
                conf.Title = "Yapay Zekâ ve Makine Öğrenmesi Kongresi 2024";
                conf.Description = "Yapay zekâ, derin öğrenme ve doğal dil işleme konularındaki öncü araştırmaların buluşma noktası.";
                conf.StartDate = now.AddMonths(-3);
                conf.EndDate = now.AddMonths(-3).AddDays(3);
                conf.City = "İstanbul"; conf.Country = "Türkiye"; conf.Venue = "İTÜ Süleyman Demirel Kültür Merkezi";
                conf.IsSubmissionOpen = false; conf.IsRegistrationOpen = false;
                conf.IsProceedingBookPublished = true;
                conf.ProceedingBookFilePath = "/uploads/proceeding-books/yapay-zeka-2024-bildiri-kitabi.pdf";
                conf.ProceedingBookPublishedDate = now.AddMonths(-2);
                conf.AbstractSubmissionDeadline = now.AddMonths(-4);
                conf.CertificateFirstSignerName = "Prof. Dr. Ahmet Yıldız";
                conf.CertificateFirstSignerTitle = "Kongre Başkanı";
            });

            // --- Kongre C: Devam eden ---
            var confC = await EnsureConference(db, tenant.Id, "muhendislik-2025", conf =>
            {
                conf.Title = "Uluslararası Mühendislik ve Fen Bilimleri Kongresi 2025";
                conf.Description = "Mühendislik ve fen bilimlerinin farklı disiplinlerini bir araya getiren uluslararası platform.";
                conf.StartDate = now.AddDays(-1);
                conf.EndDate = now.AddDays(2);
                conf.City = "İzmir"; conf.Country = "Türkiye"; conf.Venue = "Dokuz Eylül Üniversitesi Kongre Merkezi";
                conf.IsSubmissionOpen = false; conf.IsRegistrationOpen = true;
                conf.IsProceedingBookPublished = false;
                conf.AbstractSubmissionDeadline = now.AddDays(-30);
                conf.CertificateFirstSignerName = "Prof. Dr. Ahmet Yıldız";
                conf.CertificateFirstSignerTitle = "Kongre Başkanı";
            });

            // --- Kongre D: Yaklaşan, bildiri kabul açık ---
            var confD = await EnsureConference(db, tenant.Id, "saglik-bilimleri-2025", conf =>
            {
                conf.Title = "Sağlık Bilimleri ve Tıp Teknolojileri Sempozyumu 2025";
                conf.Description = "Klinik araştırmalar, tıbbi cihazlar ve dijital sağlık alanındaki yeniliklerin paylaşıldığı sempozyum.";
                conf.StartDate = now.AddMonths(3);
                conf.EndDate = now.AddMonths(3).AddDays(2);
                conf.City = "Bursa"; conf.Country = "Türkiye"; conf.Venue = "Uludağ Üniversitesi Kültür Merkezi";
                conf.IsSubmissionOpen = true; conf.IsRegistrationOpen = true;
                conf.AbstractSubmissionDeadline = now.AddMonths(2);
                conf.MaxRegistrations = 300;
                conf.CertificateFirstSignerName = "Prof. Dr. Ahmet Yıldız";
                conf.CertificateFirstSignerTitle = "Kongre Başkanı";
            });

            // --- Kongre E: Yaklaşan, erken kayıt indirimi olan ---
            var confE = await EnsureConference(db, tenant.Id, "cevre-iklim-2026", conf =>
            {
                conf.Title = "Çevre ve İklim Değişikliği Kongresi 2026";
                conf.Description = "İklim krizi, sürdürülebilirlik ve yenilenebilir enerji politikalarının ele alındığı disiplinlerarası kongre.";
                conf.StartDate = now.AddMonths(2);
                conf.EndDate = now.AddMonths(2).AddDays(2);
                conf.City = "Antalya"; conf.Country = "Türkiye"; conf.Venue = "Akdeniz Üniversitesi Kongre Merkezi";
                conf.IsSubmissionOpen = true; conf.IsRegistrationOpen = true;
                conf.AbstractSubmissionDeadline = now.AddMonths(1);
                conf.CertificateFirstSignerName = "Prof. Dr. Ahmet Yıldız";
                conf.CertificateFirstSignerTitle = "Kongre Başkanı";
            });

            // --- Kongre F: Yaklaşan, kontenjanlı ---
            var confF = await EnsureConference(db, tenant.Id, "veteriner-hekimlik-2026", conf =>
            {
                conf.Title = "Veteriner Hekimlik ve Hayvan Sağlığı Sempozyumu 2026";
                conf.Description = "Klinik veteriner hekimlik, hayvan refahı ve zoonotik hastalıklar üzerine ulusal sempozyum.";
                conf.StartDate = now.AddMonths(5);
                conf.EndDate = now.AddMonths(5).AddDays(3);
                conf.City = "Konya"; conf.Country = "Türkiye"; conf.Venue = "Selçuk Üniversitesi Veteriner Fakültesi";
                conf.IsSubmissionOpen = true; conf.IsRegistrationOpen = true;
                conf.AbstractSubmissionDeadline = now.AddMonths(3);
                conf.FullTextSubmissionDeadline = now.AddMonths(4);
                conf.MaxRegistrations = 150;
                conf.CertificateFirstSignerName = "Prof. Dr. Ahmet Yıldız";
                conf.CertificateFirstSignerTitle = "Kongre Başkanı";
            });

            // ──────────────────────────────────────────────────────────────
            // 4. RegistrationType'lar (her kongre için)
            // ──────────────────────────────────────────────────────────────
            var regTypeA = await EnsureRegType(db, confA.Id, "Akademisyen", 750m);
            var regTypeB = await EnsureRegType(db, confB.Id, "Akademisyen", 850m);
            var regTypeC = await EnsureRegType(db, confC.Id, "Akademisyen", 950m);
            var regTypeD = await EnsureRegType(db, confD.Id, "Akademisyen", 1000m);
            var regTypeDListener = await EnsureRegType(db, confD.Id, "Dinleyici", 400m, "Listener");

            // Kongre E: erken kayıt türünün son tarihi var, normal tür süresizdir
            var regTypeEEarly = await EnsureRegType(db, confE.Id, "Erken Kayıt", 700m,
                deadline: now.AddDays(21));
            var regTypeE = await EnsureRegType(db, confE.Id, "Akademisyen", 1100m);
            var regTypeEListener = await EnsureRegType(db, confE.Id, "Dinleyici", 450m, "Listener");

            var regTypeF = await EnsureRegType(db, confF.Id, "Akademisyen", 1250m);
            var regTypeFListener = await EnsureRegType(db, confF.Id, "Dinleyici", 500m, "Listener");

            // ──────────────────────────────────────────────────────────────
            // 5. ConferenceTopic'ler
            // ──────────────────────────────────────────────────────────────
            var topicA1 = await EnsureTopic(db, confA.Id, "Yapay Zekâ ve Veri Madenciliği");
            var topicA2 = await EnsureTopic(db, confA.Id, "Siber Güvenlik");
            var topicA3 = await EnsureTopic(db, confA.Id, "Yazılım Mühendisliği");

            var topicB1 = await EnsureTopic(db, confB.Id, "Derin Öğrenme");
            var topicB2 = await EnsureTopic(db, confB.Id, "Doğal Dil İşleme");
            var topicB3 = await EnsureTopic(db, confB.Id, "Bilgisayarlı Görü");

            var topicC1 = await EnsureTopic(db, confC.Id, "Malzeme Bilimi");
            var topicC2 = await EnsureTopic(db, confC.Id, "Enerji Sistemleri");
            var topicC3 = await EnsureTopic(db, confC.Id, "Biyomühendislik");

            var topicD1 = await EnsureTopic(db, confD.Id, "Dijital Sağlık");
            var topicD2 = await EnsureTopic(db, confD.Id, "Tıbbi Görüntüleme");

            var topicE1 = await EnsureTopic(db, confE.Id, "İklim Modellemesi");
            var topicE2 = await EnsureTopic(db, confE.Id, "Yenilenebilir Enerji");
            var topicE3 = await EnsureTopic(db, confE.Id, "Sürdürülebilir Kentleşme");

            var topicF1 = await EnsureTopic(db, confF.Id, "Klinik Veteriner Hekimlik");
            var topicF2 = await EnsureTopic(db, confF.Id, "Zoonotik Hastalıklar");
            var topicF3 = await EnsureTopic(db, confF.Id, "Hayvan Refahı");

            // ──────────────────────────────────────────────────────────────
            // 6. Bildirileri Oluştur
            // ──────────────────────────────────────────────────────────────

            // --- Kongre A bildirileri (geçmiş, hepsi karara bağlı) ---
            await EnsureSubmission(db, tenant.Id, confA.Id, author1.Id, "A001",
                "Derin Öğrenme ile Anomali Tespiti: Endüstriyel IoT Uygulamaları",
                SampleAbstract("derin öğrenme", "anomali tespiti", "IoT sensör verisi"),
                "derin öğrenme, anomali tespiti, IoT, evrişimli sinir ağları",
                SubmissionStatus.Accepted, "Sözlü", topicA1.Id,
                reviewer1.Id, "Güçlü metodoloji, literatür katkısı net. Kabul önerilir.", 85,
                author: new[] { ("Fatma", "Demir", "Test Üniversitesi", "yazar1@test.edu.tr", true) });

            await EnsureSubmission(db, tenant.Id, confA.Id, author2.Id, "A002",
                "Blok Zinciri Tabanlı Sağlık Veri Yönetim Sistemi",
                SampleAbstract("blok zinciri", "sağlık verisi", "merkezi olmayan sistem"),
                "blok zinciri, sağlık bilişimi, veri güvenliği, Ethereum",
                SubmissionStatus.Accepted, "Poster", topicA2.Id,
                reviewer1.Id, "Pratik uygulama değeri yüksek. Kabul.", 78,
                author: new[] { ("Mehmet", "Kaya", "Ankara Üniversitesi", "yazar2@test.edu.tr", true) });

            await EnsureSubmission(db, tenant.Id, confA.Id, author3.Id, "A003",
                "Büyük Ölçekli Web Uygulamalarında Performans Optimizasyonu",
                SampleAbstract("mikroservis", "performans", "ölçeklenebilirlik"),
                "mikroservis, konteyner, Kubernetes, performans",
                SubmissionStatus.Accepted, "Sözlü", topicA3.Id,
                reviewer2.Id, "Orijinal çalışma, sunumu kuvvetlendirilebilir. Kabul.", 80,
                author: new[] { ("Zeynep", "Arslan", "İTÜ", "yazar3@test.edu.tr", true) });

            await EnsureSubmission(db, tenant.Id, confA.Id, author1.Id, "A004",
                "Doğal Dil İşlemede Transformer Mimarilerinin Karşılaştırmalı Analizi",
                SampleAbstract("transformer", "BERT", "GPT dil modelleri"),
                "NLP, transformer, BERT, GPT, dil modeli",
                SubmissionStatus.Rejected, "Sözlü", topicA1.Id,
                reviewer2.Id, "Özgün katkı yetersiz, literatür taraması eksik.", 42,
                author: new[] { ("Fatma", "Demir", "Test Üniversitesi", "yazar1@test.edu.tr", true) });

            // --- Kongre B bildirileri ---
            await EnsureSubmission(db, tenant.Id, confB.Id, author2.Id, "B001",
                "ResNet ile Tıbbi Görüntü Sınıflandırması: Akciğer Kanseri Tespiti",
                SampleAbstract("ResNet", "tıbbi görüntü", "akciğer kanseri"),
                "derin öğrenme, ResNet, tıbbi görüntü, kanser tespiti",
                SubmissionStatus.Accepted, "Sözlü", topicB1.Id,
                reviewer1.Id, "Kapsamlı deneysel çalışma. Kesinlikle kabul.", 92,
                author: new[] { ("Mehmet", "Kaya", "Ankara Üniversitesi", "yazar2@test.edu.tr", true) });

            await EnsureSubmission(db, tenant.Id, confB.Id, author3.Id, "B002",
                "Türkçe Metin Sınıflandırması için BERT Tabanlı Yaklaşım",
                SampleAbstract("BERT", "Türkçe NLP", "metin sınıflandırma"),
                "NLP, BERT, Türkçe, metin sınıflandırma, derin öğrenme",
                SubmissionStatus.Accepted, "Sözlü", topicB2.Id,
                reviewer2.Id, "Türkçe NLP literatürüne önemli katkı.", 88,
                author: new[] { ("Zeynep", "Arslan", "İTÜ", "yazar3@test.edu.tr", true) });

            await EnsureSubmission(db, tenant.Id, confB.Id, author1.Id, "B003",
                "Nesne Tespitinde YOLOv8 Uygulaması: Akıllı Trafik Yönetimi",
                SampleAbstract("YOLO", "nesne tespiti", "akıllı trafik"),
                "YOLOv8, nesne tespiti, trafik, bilgisayarlı görü",
                SubmissionStatus.Accepted, "Poster", topicB3.Id,
                reviewer1.Id, "Pratik uygulama odaklı güçlü çalışma.", 83,
                author: new[] { ("Fatma", "Demir", "Test Üniversitesi", "yazar1@test.edu.tr", true) });

            // --- Kongre C bildirileri (devam eden, çeşitli statüler) ---
            await EnsureSubmission(db, tenant.Id, confC.Id, author1.Id, "C001",
                "Grafen Takviyeli Kompozit Malzemelerin Mekanik Özellikleri",
                SampleAbstract("grafen", "kompozit malzeme", "mekanik özellikler"),
                "grafen, kompozit, malzeme bilimi, mekanik test",
                SubmissionStatus.Accepted, "Sözlü", topicC1.Id,
                reviewer2.Id, "Özgün deneysel veri. Kabul.", 87,
                author: new[] { ("Fatma", "Demir", "Test Üniversitesi", "yazar1@test.edu.tr", true) });

            await EnsureSubmission(db, tenant.Id, confC.Id, author2.Id, "C002",
                "Rüzgâr Türbini Verimini Artırmaya Yönelik Kanat Profili Optimizasyonu",
                SampleAbstract("rüzgâr türbini", "kanat profili", "CFD simülasyonu"),
                "rüzgâr enerjisi, CFD, optimizasyon, türbin",
                SubmissionStatus.UnderReview, "Sözlü", topicC2.Id,
                reviewer1.Id, null, 0,
                author: new[] { ("Mehmet", "Kaya", "Ankara Üniversitesi", "yazar2@test.edu.tr", true) });

            await EnsureSubmission(db, tenant.Id, confC.Id, author3.Id, "C003",
                "Biyomedikal Uygulamalar için Biyobozunur Polimer Sentezi",
                SampleAbstract("biyopolimer", "biyobozunur", "ilaç salınım sistemi"),
                "biyopolimer, biyobozunur, ilaç salınımı, biyomühendislik",
                SubmissionStatus.RevisionRequired, "Poster", topicC3.Id,
                reviewer2.Id, "Metodoloji güçlendirilmeli, istatistiksel analiz eklenmeli.", 58,
                author: new[] { ("Zeynep", "Arslan", "İTÜ", "yazar3@test.edu.tr", true) },
                rebuttalText: "Belirtilen eksiklikler için ek deneyler yapılmış olup revize versiyon hazırlanmaktadır. İstatistiksel analizler için SPSS kullanılacaktır.",
                rebuttalDate: now.AddDays(-2));

            await EnsureSubmission(db, tenant.Id, confC.Id, author1.Id, "C004",
                "Güneş Enerjisi Depolama Sistemlerinde Lityum-İyon Pil Performansı",
                SampleAbstract("lityum-iyon pil", "enerji depolama", "güneş enerjisi"),
                "güneş enerjisi, pil, enerji depolama, verimlilik",
                SubmissionStatus.Pending, "Sözlü", topicC2.Id,
                null, null, 0,
                author: new[] { ("Fatma", "Demir", "Test Üniversitesi", "yazar1@test.edu.tr", true) });

            // --- Kongre D bildirileri (yaklaşan, bildiri alma aşaması) ---
            await EnsureSubmission(db, tenant.Id, confD.Id, author2.Id, "D001",
                "Yapay Zekâ Destekli Erken Teşhis Sistemleri: Diyabet Öngörüsü",
                SampleAbstract("yapay zekâ", "erken teşhis", "diyabet"),
                "yapay zekâ, erken teşhis, diyabet, makine öğrenmesi",
                SubmissionStatus.New, "Sözlü", topicD1.Id,
                null, null, 0,
                author: new[] { ("Mehmet", "Kaya", "Ankara Üniversitesi", "yazar2@test.edu.tr", true) });

            await EnsureSubmission(db, tenant.Id, confD.Id, author3.Id, "D002",
                "MRI Görüntülerinde Tümör Segmentasyonu: U-Net Yaklaşımı",
                SampleAbstract("MRI", "tümör segmentasyonu", "U-Net"),
                "MRI, segmentasyon, U-Net, tıbbi görüntü, derin öğrenme",
                SubmissionStatus.Pending, "Sözlü", topicD2.Id,
                null, null, 0,
                author: new[] { ("Zeynep", "Arslan", "İTÜ", "yazar3@test.edu.tr", true) });

            // Eksik bildiri (test için — sadece başlık var)
            await EnsureIncompleteSubmission(db, tenant.Id, confD.Id, author1.Id, "D003",
                "Klinik Karar Destek Sistemlerinde Büyük Dil Modelleri");

            // ──────────────────────────────────────────────────────────────
            // 7. Kayıtlar
            // ──────────────────────────────────────────────────────────────
            await EnsureRegistration(db, confA.Id, author1.Id, regTypeA.Id, 750m, paid: true, now.AddMonths(-9));
            await EnsureRegistration(db, confA.Id, author2.Id, regTypeA.Id, 750m, paid: true, now.AddMonths(-9));
            await EnsureRegistration(db, confA.Id, author3.Id, regTypeA.Id, 750m, paid: true, now.AddMonths(-9));

            await EnsureRegistration(db, confB.Id, author2.Id, regTypeB.Id, 850m, paid: true, now.AddMonths(-4));
            await EnsureRegistration(db, confB.Id, author3.Id, regTypeB.Id, 850m, paid: true, now.AddMonths(-4));
            await EnsureRegistration(db, confB.Id, listener.Id, regTypeB.Id, 850m, paid: true, now.AddMonths(-4));

            await EnsureRegistration(db, confC.Id, author1.Id, regTypeC.Id, 950m, paid: true, now.AddDays(-20));
            await EnsureRegistration(db, confC.Id, author2.Id, regTypeC.Id, 950m, paid: false, now.AddDays(-15));
            await EnsureRegistration(db, confC.Id, listener.Id, regTypeC.Id, 950m, paid: true, now.AddDays(-10));

            await EnsureRegistration(db, confD.Id, listener.Id, regTypeDListener.Id, 400m, paid: true, now.AddDays(-5));

            await db.SaveChangesAsync();
        }

        // ── Yardımcı: Kullanıcı ────────────────────────────────────────────
        private static async Task<AppUser> EnsureUser(
            UserManager<AppUser> um,
            string email, string first, string last,
            string? title, string? department, string? university,
            Guid? tenantId, string role)
        {
            var user = await um.FindByEmailAsync(email);
            if (user != null) return user;

            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = first,
                LastName = last,
                Title = title,
                Department = department,
                University = university,
                Institution = university,
                TenantId = tenantId
            };

            var result = await um.CreateAsync(user, Password);
            if (result.Succeeded)
                await um.AddToRoleAsync(user, role);

            return (await um.FindByEmailAsync(email))!;
        }

        // ── Yardımcı: Kongre ──────────────────────────────────────────────
        private static async Task<Conference> EnsureConference(
            AppDbContext db, Guid tenantId, string slug,
            Action<Conference> configure)
        {
            var conf = await db.Conferences.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Slug == slug);

            if (conf != null) return conf;

            conf = new Conference { TenantId = tenantId, Slug = slug };
            configure(conf);
            db.Conferences.Add(conf);
            await db.SaveChangesAsync();
            return conf;
        }

        // ── Yardımcı: RegistrationType ────────────────────────────────────
        private static async Task<RegistrationType> EnsureRegType(
            AppDbContext db, Guid confId, string name, decimal price,
            string roleName = "Author", DateTime? deadline = null)
        {
            var rt = await db.RegistrationTypes.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.ConferenceId == confId && r.Name == name);

            if (rt != null) return rt;

            rt = new RegistrationType
            {
                ConferenceId = confId,
                Name = name,
                Description = $"{name} kayıt türü",
                Price = price,
                Currency = "TRY",
                RoleName = roleName,
                IsActive = true,
                Deadline = deadline
            };
            db.RegistrationTypes.Add(rt);
            await db.SaveChangesAsync();
            return rt;
        }

        // ── Yardımcı: ConferenceTopic ─────────────────────────────────────
        private static async Task<ConferenceTopic> EnsureTopic(
            AppDbContext db, Guid confId, string name)
        {
            var topic = await db.ConferenceTopics.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.ConferenceId == confId && t.Name == name);

            if (topic != null) return topic;

            topic = new ConferenceTopic { ConferenceId = confId, Name = name, IsActive = true };
            db.ConferenceTopics.Add(topic);
            await db.SaveChangesAsync();
            return topic;
        }

        // ── Yardımcı: Submission (tam) ────────────────────────────────────
        private static async Task EnsureSubmission(
            AppDbContext db,
            Guid tenantId, Guid confId, string authorId, string idCode,
            string title, string abstractText, string keywords,
            SubmissionStatus status, string presentationType, Guid topicId,
            string? reviewerId, string? reviewComment, int score,
            (string first, string last, string institution, string email, bool isCorr)[]? author = null,
            string? rebuttalText = null, DateTime? rebuttalDate = null)
        {
            var exists = await db.Submissions.IgnoreQueryFilters()
                .AnyAsync(s => s.SubmissionIdCode == idCode && s.ConferenceId == confId);

            if (exists) return;

            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ConferenceId = confId,
                AuthorId = authorId,
                SubmissionIdCode = idCode,
                Title = title,
                Abstract = abstractText,
                Keywords = keywords,
                Status = status,
                PresentationType = presentationType,
                ConferenceTopicId = topicId,
                CreatedDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(20, 90)),
                UpdatedDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 15)),
                IsFeedbackGiven = status == SubmissionStatus.Accepted || status == SubmissionStatus.Rejected,
                RebuttalText = rebuttalText,
                RebuttalDate = rebuttalDate
            };

            if (status == SubmissionStatus.Accepted || status == SubmissionStatus.Rejected)
            {
                submission.DecisionDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(5, 30));
                submission.AdminDecisionNote = reviewComment;
            }
            else if (status == SubmissionStatus.RevisionRequired)
            {
                submission.AdminDecisionNote = reviewComment;
            }

            db.Submissions.Add(submission);
            await db.SaveChangesAsync();

            // SubmissionAuthor(lar)
            if (author != null)
            {
                foreach (var (f, l, inst, email, isCorr) in author)
                {
                    db.SubmissionAuthors.Add(new SubmissionAuthor
                    {
                        SubmissionId = submission.Id,
                        FirstName = f,
                        LastName = l,
                        Institution = inst,
                        Email = email,
                        IsCorrespondingAuthor = isCorr,
                        Order = 1
                    });
                }
                await db.SaveChangesAsync();
            }

            // ReviewAssignment + Review
            if (reviewerId != null && (status == SubmissionStatus.Accepted ||
                status == SubmissionStatus.Rejected || status == SubmissionStatus.UnderReview ||
                status == SubmissionStatus.RevisionRequired))
            {
                var assignment = new ReviewAssignment
                {
                    SubmissionId = submission.Id,
                    ReviewerId = reviewerId,
                    AssignedDate = submission.CreatedDate.AddDays(5),
                    EvaluationDate = status == SubmissionStatus.UnderReview ? null : submission.DecisionDate
                };
                db.ReviewAssignments.Add(assignment);
                await db.SaveChangesAsync();

                if (status != SubmissionStatus.UnderReview && !string.IsNullOrEmpty(reviewComment))
                {
                    db.Reviews.Add(new Review
                    {
                        ReviewAssignmentId = assignment.Id,
                        ReviewerName = "Hakem",
                        CommentsToAuthor = reviewComment,
                        Recommendation = status == SubmissionStatus.Accepted ? "accept" : "reject",
                        Score = score,
                        ScoreOriginality = score > 70 ? 4 : 2,
                        ScoreMethodology = score > 70 ? 4 : 2,
                        ScorePresentation = score > 70 ? 4 : 3
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        // ── Yardımcı: Eksik (taslak) Submission ──────────────────────────
        private static async Task EnsureIncompleteSubmission(
            AppDbContext db, Guid tenantId, Guid confId, string authorId,
            string idCode, string title)
        {
            var exists = await db.Submissions.IgnoreQueryFilters()
                .AnyAsync(s => s.SubmissionIdCode == idCode && s.ConferenceId == confId);

            if (exists) return;

            db.Submissions.Add(new Submission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ConferenceId = confId,
                AuthorId = authorId,
                SubmissionIdCode = idCode,
                Title = title,
                Abstract = "",
                Keywords = "",
                Status = SubmissionStatus.New,
                CreatedDate = DateTime.UtcNow.AddDays(-12),
                UpdatedDate = DateTime.UtcNow.AddDays(-12)
            });
            await db.SaveChangesAsync();
        }

        // ── Yardımcı: Registration ────────────────────────────────────────
        private static async Task EnsureRegistration(
            AppDbContext db, Guid confId, string userId,
            Guid regTypeId, decimal amount, bool paid, DateTime date)
        {
            var exists = await db.Registrations.IgnoreQueryFilters()
                .AnyAsync(r => r.ConferenceId == confId && r.AppUserId == userId);

            if (exists) return;

            db.Registrations.Add(new Registration
            {
                ConferenceId = confId,
                AppUserId = userId,
                RegistrationTypeId = regTypeId,
                RegistrationDate = date,
                IsPaid = paid,
                PaymentDate = paid ? date.AddDays(1) : null,
                Amount = amount,
                BillingName = "Test Katılımcı"
            });
            await db.SaveChangesAsync();
        }

        // ── Yardımcı: SystemParameter'lar ────────────────────────────────
        private static async Task EnsureSystemParameters(AppDbContext db)
        {
            var existing = await db.SystemParameters.Select(p => p.Group + "|" + p.Name).ToListAsync();

            var groups = new Dictionary<string, string[]>
            {
                ["Title"] = new[]
                {
                    "Prof. Dr.", "Doç. Dr.", "Dr. Öğr. Üyesi", "Dr.",
                    "Öğr. Gör. Dr.", "Öğr. Gör.", "Araş. Gör. Dr.", "Araş. Gör.",
                    "Uzm.", "Diğer"
                },
                ["University"] = new[]
                {
                    "Ankara Üniversitesi", "Boğaziçi Üniversitesi", "Dokuz Eylül Üniversitesi",
                    "Ege Üniversitesi", "Erciyes Üniversitesi", "Gazi Üniversitesi",
                    "Hacettepe Üniversitesi", "İstanbul Teknik Üniversitesi (İTÜ)",
                    "İstanbul Üniversitesi", "Koç Üniversitesi", "Marmara Üniversitesi",
                    "ODTÜ", "Sabancı Üniversitesi", "Selçuk Üniversitesi",
                    "Test Üniversitesi", "Uludağ Üniversitesi", "Diğer"
                },
                ["Faculty"] = new[]
                {
                    "Mühendislik Fakültesi", "Fen-Edebiyat Fakültesi", "Tıp Fakültesi",
                    "İktisadi ve İdari Bilimler Fakültesi", "Eğitim Fakültesi",
                    "Hukuk Fakültesi", "Mimarlık Fakültesi", "Eczacılık Fakültesi",
                    "Diş Hekimliği Fakültesi", "Veteriner Fakültesi",
                    "Sağlık Bilimleri Fakültesi", "Diğer"
                },
                ["Department"] = new[]
                {
                    "Bilgisayar Mühendisliği", "Biyomedikal Mühendisliği", "Biyomühendislik",
                    "Çevre Mühendisliği", "Elektrik-Elektronik Mühendisliği",
                    "Endüstri Mühendisliği", "İnşaat Mühendisliği",
                    "Kimya Mühendisliği", "Makine Mühendisliği",
                    "Malzeme Bilimi ve Mühendisliği", "Matematik",
                    "Mekatronik Mühendisliği", "Tıp", "Yazılım Mühendisliği", "Diğer"
                }
            };

            bool added = false;
            foreach (var (group, names) in groups)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    var key = group + "|" + names[i];
                    if (!existing.Contains(key))
                    {
                        db.SystemParameters.Add(new SystemParameter
                        {
                            Group = group,
                            Name = names[i],
                            NameEn = names[i],
                            Order = i + 1,
                            IsActive = true
                        });
                        added = true;
                    }
                }
            }

            if (added) await db.SaveChangesAsync();
        }

        // ── Örnek özet metni ──────────────────────────────────────────────
        private static string SampleAbstract(string term1, string term2, string term3) =>
            $"Bu çalışmada {term1} yöntemlerinin {term2} problemine uygulanması incelenmektedir. " +
            $"Literatürde mevcut yaklaşımların sınırlılıkları göz önünde bulundurularak {term3} " +
            $"alanında yeni bir metodoloji önerilmiştir. Önerilen yöntem, kapsamlı deneysel " +
            $"çalışmalarla test edilmiş ve sonuçlar karşılaştırmalı olarak sunulmuştur. " +
            $"Elde edilen bulgular, {term1} tekniklerinin {term2} performansını anlamlı ölçüde " +
            $"iyileştirdiğini ortaya koymaktadır. Çalışma, {term3} alanındaki araştırmacılara " +
            $"pratik ve teorik katkılar sunmayı hedeflemekte olup gelecekteki araştırmalar için " +
            $"yeni yönelimler önermektedir.";
    }
}
