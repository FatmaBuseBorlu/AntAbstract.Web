BEGIN TRANSACTION;
BEGIN TRY
    -- 1. Değişkenleri Tanımla
    DECLARE @MyTenantId UNIQUEIDENTIFIER = NEWID();
    DECLARE @MyConfId UNIQUEIDENTIFIER = NEWID();
    DECLARE @SlugName NVARCHAR(50) = 'bilim2025';

    -- 2. Tenant Ekle
    IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Slug = @SlugName)
    BEGIN
        INSERT INTO Tenants (Id, Name, Slug)
        VALUES (@MyTenantId, 'Yeni Bilim Kongresi', @SlugName);
    END
    ELSE
    BEGIN
        SELECT @MyTenantId = Id FROM Tenants WHERE Slug = @SlugName;
    END

    -- 3. Conference Ekle
    INSERT INTO Conferences (Id, TenantId, Title, StartDate, EndDate, City, Country)
    VALUES (
        @MyConfId, 
        @MyTenantId, 
        '1. Uluslararası Bilim ve Teknoloji Kongresi', 
        '2025-10-10', 
        '2025-10-12', 
        'Ankara', 
        'Türkiye'
    );

    -- 4. RegistrationType Ekle (Description eklendi)
    INSERT INTO RegistrationTypes (Id, ConferenceId, Name, Description, Price, Currency)
    VALUES (
        NEWID(), 
        @MyConfId, 
        'Standart Katılım', 
        'Genel katılımcılar için standart kayıt paketidir.', -- Eksik olan Description alanı
        500.00, 
        'TL'
    );

    COMMIT TRANSACTION;
    PRINT 'Başarıyla eklendi: ' + @SlugName;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Hata Oluştu: ' + ERROR_MESSAGE();
END CATCH