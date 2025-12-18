-- BAĞLANTILARI KOPARMADAN TEMİZLİK YAPALIM

-- 1. Önce Kim Hangi Rolde bilgisini sil
DELETE FROM AspNetUserRoles;

-- 2. Varsa diğer ilişkili tabloları temizle (Hata almamak için)
-- (Eğer ReviewAssignments tablon doluysa önce onu silmen gerekir, boşsa sorun yok)
-- DELETE FROM ReviewAssignments; 

-- 3. Kullanıcıları Sil
DELETE FROM AspNetUsers;

-- 4. Rolleri Sil
DELETE FROM AspNetRoles;

PRINT '🧹 Veritabanı pırıl pırıl oldu! Tüm kullanıcılar ve roller silindi.';