IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [CongressTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CongressTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [ScientificFields] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ScientificFields] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [SystemParameters] (
        [Id] int NOT NULL IDENTITY,
        [Group] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Order] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_SystemParameters] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Slug] nvarchar(max) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [ThemeJson] nvarchar(max) NULL,
        [LogoUrl] nvarchar(max) NULL,
        [ScientificFieldId] int NULL,
        [CongressTypeId] int NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tenants_CongressTypes_CongressTypeId] FOREIGN KEY ([CongressTypeId]) REFERENCES [CongressTypes] ([Id]),
        CONSTRAINT [FK_Tenants_ScientificFields_ScientificFieldId] FOREIGN KEY ([ScientificFieldId]) REFERENCES [ScientificFields] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(50) NULL,
        [LastName] nvarchar(50) NULL,
        [IdentityNumber] nvarchar(20) NULL,
        [AlternativeEmail] nvarchar(max) NULL,
        [City] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [University] nvarchar(max) NULL,
        [Institution] nvarchar(200) NULL,
        [Title] nvarchar(100) NULL,
        [Profession] nvarchar(max) NULL,
        [ExpertiseAreas] nvarchar(500) NULL,
        [DisplayName] nvarchar(max) NULL,
        [ProfileImagePath] nvarchar(max) NULL,
        [OrcidId] nvarchar(50) NULL,
        [ResearcherId] nvarchar(100) NULL,
        [GoogleScholarLink] nvarchar(200) NULL,
        [TenantId] uniqueidentifier NULL,
        [Faculty] nvarchar(max) NULL,
        [Department] nvarchar(max) NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUsers_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Conferences] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [City] nvarchar(max) NULL,
        [Country] nvarchar(max) NULL,
        [Venue] nvarchar(max) NULL,
        [LogoPath] nvarchar(max) NULL,
        [BannerPath] nvarchar(max) NULL,
        [Slug] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [WritingRulesPath] nvarchar(max) NULL,
        [AbstractTemplatePath] nvarchar(max) NULL,
        [FullTextTemplatePath] nvarchar(max) NULL,
        CONSTRAINT [PK_Conferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conferences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] uniqueidentifier NOT NULL,
        [Subject] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [SentDate] datetime2 NOT NULL,
        [IsRead] bit NOT NULL,
        [SenderId] nvarchar(450) NOT NULL,
        [ReceiverId] nvarchar(450) NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Messages_AspNetUsers_ReceiverId] FOREIGN KEY ([ReceiverId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Messages_AspNetUsers_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [Link] nvarchar(max) NOT NULL,
        [IsRead] bit NOT NULL,
        [Icon] nvarchar(max) NOT NULL,
        [Color] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Certificates] (
        [Id] uniqueidentifier NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Type] int NOT NULL,
        [EligibleAt] datetime2 NOT NULL,
        [GeneratedAt] datetime2 NULL,
        [FilePath] nvarchar(max) NULL,
        [FileName] nvarchar(max) NULL,
        [ContentType] nvarchar(max) NULL,
        [EmailSentAt] datetime2 NULL,
        [EmailTo] nvarchar(max) NULL,
        [EmailSendCount] int NOT NULL,
        [LastEmailError] nvarchar(max) NULL,
        [CertificateNo] nvarchar(max) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Certificates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Certificates_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Certificates_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [ConferenceAttendances] (
        [Id] uniqueidentifier NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [FirstJoinedAt] datetime2 NOT NULL,
        [LastPingAt] datetime2 NULL,
        [TotalSeconds] int NOT NULL,
        [RequiredSeconds] int NOT NULL,
        [CompletedAt] datetime2 NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ConferenceAttendances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConferenceAttendances_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ConferenceAttendances_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [ConferencePageBlocks] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] uniqueidentifier NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [Page] nvarchar(30) NOT NULL,
        [Culture] nvarchar(10) NOT NULL,
        [BlockType] int NOT NULL,
        [Title] nvarchar(200) NULL,
        [Subtitle] nvarchar(400) NULL,
        [ContentJson] nvarchar(max) NULL,
        [Order] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ConferencePageBlocks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConferencePageBlocks_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Hotels] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [PhotoPath] nvarchar(max) NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Hotels] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Hotels_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        [PaymentMethod] nvarchar(50) NOT NULL,
        [TransactionId] nvarchar(150) NULL,
        [PaymentDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [BillingName] nvarchar(200) NULL,
        [BillingAddress] nvarchar(500) NULL,
        [TaxOffice] nvarchar(100) NULL,
        [TaxNumber] nvarchar(50) NULL,
        [AppUserId] nvarchar(450) NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [RelatedSubmissionId] uniqueidentifier NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_AspNetUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Payments_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [RegistrationTypes] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NOT NULL DEFAULT N'',
        [Price] decimal(18,2) NOT NULL,
        [Currency] nvarchar(10) NOT NULL,
        [Deadline] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RegistrationTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RegistrationTypes_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Sessions] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [SessionDate] datetime2 NOT NULL,
        [Location] nvarchar(100) NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Sessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sessions_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [TransferOptions] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_TransferOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TransferOptions_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [RoomTypes] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [Capacity] int NOT NULL,
        [TotalQuota] int NOT NULL,
        [HotelId] uniqueidentifier NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_RoomTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoomTypes_Hotels_HotelId] FOREIGN KEY ([HotelId]) REFERENCES [Hotels] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Registrations] (
        [Id] uniqueidentifier NOT NULL,
        [AppUserId] nvarchar(450) NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [RegistrationTypeId] uniqueidentifier NOT NULL,
        [RegistrationDate] datetime2 NOT NULL,
        [IsPaid] bit NOT NULL,
        [PaymentDate] datetime2 NULL,
        [PaymentTransactionId] nvarchar(max) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [BillingName] nvarchar(250) NULL,
        [TaxOffice] nvarchar(100) NULL,
        [TaxNumber] nvarchar(50) NULL,
        [BillingAddress] nvarchar(500) NULL,
        CONSTRAINT [PK_Registrations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Registrations_AspNetUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Registrations_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Registrations_RegistrationTypes_RegistrationTypeId] FOREIGN KEY ([RegistrationTypeId]) REFERENCES [RegistrationTypes] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Submissions] (
        [Id] uniqueidentifier NOT NULL,
        [SubmissionIdCode] nvarchar(20) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Abstract] nvarchar(max) NOT NULL,
        [Keywords] nvarchar(max) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [DecisionDate] datetime2 NULL,
        [Status] int NOT NULL,
        [IsFeedbackGiven] bit NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [AuthorId] nvarchar(450) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NULL,
        [Topic] nvarchar(100) NOT NULL,
        [PresentationType] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_Submissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Submissions_AspNetUsers_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Submissions_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Submissions_Sessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [Sessions] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [AccommodationBookings] (
        [Id] uniqueidentifier NOT NULL,
        [AppUserId] nvarchar(450) NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [RoomTypeId] uniqueidentifier NOT NULL,
        [TransferOptionId] uniqueidentifier NULL,
        [CheckInDate] datetime2 NOT NULL,
        [CheckOutDate] datetime2 NOT NULL,
        [RoommateName] nvarchar(max) NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [IsPaid] bit NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [UpdatedDate] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_AccommodationBookings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccommodationBookings_AspNetUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AccommodationBookings_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AccommodationBookings_RoomTypes_RoomTypeId] FOREIGN KEY ([RoomTypeId]) REFERENCES [RoomTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AccommodationBookings_TransferOptions_TransferOptionId] FOREIGN KEY ([TransferOptionId]) REFERENCES [TransferOptions] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [ReviewAssignments] (
        [Id] int NOT NULL IDENTITY,
        [AssignedDate] datetime2 NOT NULL,
        [EvaluationDate] datetime2 NULL,
        [SubmissionId] uniqueidentifier NOT NULL,
        [ReviewerId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_ReviewAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewAssignments_AspNetUsers_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReviewAssignments_Submissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [Submissions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [SubmissionAuthors] (
        [Id] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Institution] nvarchar(200) NULL,
        [Email] nvarchar(200) NULL,
        [ORCID] nvarchar(50) NULL,
        [IsCorrespondingAuthor] bit NOT NULL,
        [Order] int NOT NULL,
        [SubmissionId] uniqueidentifier NOT NULL,
        [AppUserId] nvarchar(450) NULL,
        CONSTRAINT [PK_SubmissionAuthors] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubmissionAuthors_AspNetUsers_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_SubmissionAuthors_Submissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [Submissions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [SubmissionFiles] (
        [Id] int NOT NULL IDENTITY,
        [FileName] nvarchar(max) NOT NULL,
        [StoredFileName] nvarchar(max) NOT NULL,
        [FilePath] nvarchar(max) NOT NULL,
        [Type] int NOT NULL,
        [Version] int NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        [SubmissionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SubmissionFiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubmissionFiles_Submissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [Submissions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] int NOT NULL IDENTITY,
        [ReviewAssignmentId] int NOT NULL,
        [ReviewerName] nvarchar(max) NOT NULL,
        [CommentsToAuthor] nvarchar(max) NOT NULL,
        [Recommendation] nvarchar(max) NOT NULL,
        [Score] int NOT NULL,
        [ReviewedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reviews_ReviewAssignments_ReviewAssignmentId] FOREIGN KEY ([ReviewAssignmentId]) REFERENCES [ReviewAssignments] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccommodationBookings_AppUserId] ON [AccommodationBookings] ([AppUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccommodationBookings_ConferenceId] ON [AccommodationBookings] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccommodationBookings_RoomTypeId] ON [AccommodationBookings] ([RoomTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccommodationBookings_TransferOptionId] ON [AccommodationBookings] ([TransferOptionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_TenantId] ON [AspNetUsers] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Certificates_ConferenceId_UserId_Type] ON [Certificates] ([ConferenceId], [UserId], [Type]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Certificates_UserId] ON [Certificates] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ConferenceAttendances_ConferenceId_UserId] ON [ConferenceAttendances] ([ConferenceId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ConferenceAttendances_UserId] ON [ConferenceAttendances] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ConferencePageBlocks_ConferenceId] ON [ConferencePageBlocks] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ConferencePageBlocks_TenantId_ConferenceId_Page_Culture_Order] ON [ConferencePageBlocks] ([TenantId], [ConferenceId], [Page], [Culture], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Conferences_TenantId] ON [Conferences] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Hotels_ConferenceId] ON [Hotels] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_ReceiverId] ON [Messages] ([ReceiverId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Messages_SenderId] ON [Messages] ([SenderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_AppUserId] ON [Payments] ([AppUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_ConferenceId] ON [Payments] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Registrations_AppUserId] ON [Registrations] ([AppUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Registrations_ConferenceId] ON [Registrations] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Registrations_RegistrationTypeId] ON [Registrations] ([RegistrationTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RegistrationTypes_ConferenceId] ON [RegistrationTypes] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReviewAssignments_ReviewerId] ON [ReviewAssignments] ([ReviewerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReviewAssignments_SubmissionId] ON [ReviewAssignments] ([SubmissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reviews_ReviewAssignmentId] ON [Reviews] ([ReviewAssignmentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RoomTypes_HotelId] ON [RoomTypes] ([HotelId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sessions_ConferenceId] ON [Sessions] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SubmissionAuthors_AppUserId] ON [SubmissionAuthors] ([AppUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SubmissionAuthors_SubmissionId] ON [SubmissionAuthors] ([SubmissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SubmissionFiles_SubmissionId] ON [SubmissionFiles] ([SubmissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Submissions_AuthorId] ON [Submissions] ([AuthorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Submissions_ConferenceId] ON [Submissions] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Submissions_SessionId] ON [Submissions] ([SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_CongressTypeId] ON [Tenants] ([CongressTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tenants_ScientificFieldId] ON [Tenants] ([ScientificFieldId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TransferOptions_ConferenceId] ON [TransferOptions] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422145750_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260422145750_InitialCreate', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428091958_AddEnglishFieldsToRegistrationType'
)
BEGIN
    ALTER TABLE [RegistrationTypes] ADD [DescriptionEn] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428091958_AddEnglishFieldsToRegistrationType'
)
BEGIN
    ALTER TABLE [RegistrationTypes] ADD [NameEn] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428091958_AddEnglishFieldsToRegistrationType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428091958_AddEnglishFieldsToRegistrationType', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429135926_AddConferenceTopicsToSubmissions'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Submissions]') AND [c].[name] = N'Topic');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Submissions] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Submissions] ALTER COLUMN [Topic] nvarchar(150) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429135926_AddConferenceTopicsToSubmissions'
)
BEGIN
    ALTER TABLE [Submissions] ADD [ConferenceTopicId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429135926_AddConferenceTopicsToSubmissions'
)
BEGIN
    CREATE TABLE [ConferenceTopics] (
        [Id] uniqueidentifier NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [NameEn] nvarchar(150) NULL,
        [Description] nvarchar(500) NULL,
        [DescriptionEn] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [SortOrder] int NOT NULL DEFAULT 0,
        [CreatedDate] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_ConferenceTopics] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConferenceTopics_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429135926_AddConferenceTopicsToSubmissions'
)
BEGIN
    CREATE INDEX [IX_Submissions_ConferenceTopicId] ON [Submissions] ([ConferenceTopicId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429135926_AddConferenceTopicsToSubmissions'
)
BEGIN
    CREATE INDEX [IX_ConferenceTopics_ConferenceId] ON [ConferenceTopics] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429135926_AddConferenceTopicsToSubmissions'
)
BEGIN
    ALTER TABLE [Submissions] ADD CONSTRAINT [FK_Submissions_ConferenceTopics_ConferenceTopicId] FOREIGN KEY ([ConferenceTopicId]) REFERENCES [ConferenceTopics] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429135926_AddConferenceTopicsToSubmissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260429135926_AddConferenceTopicsToSubmissions', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175210_AddNameEnToSystemParameters'
)
BEGIN
    ALTER TABLE [SystemParameters] ADD [NameEn] nvarchar(250) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175210_AddNameEnToSystemParameters'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513175210_AddNameEnToSystemParameters', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516151233_AddSiteSectionTemplates'
)
BEGIN
    CREATE TABLE [SiteSectionTemplates] (
        [Id] int NOT NULL IDENTITY,
        [BlockType] int NOT NULL,
        [Order] int NOT NULL,
        [NameTr] nvarchar(150) NOT NULL,
        [NameEn] nvarchar(150) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_SiteSectionTemplates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516151233_AddSiteSectionTemplates'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SiteSectionTemplates_BlockType] ON [SiteSectionTemplates] ([BlockType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516151233_AddSiteSectionTemplates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516151233_AddSiteSectionTemplates', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [CreatedDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [Description] nvarchar(2000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [DescriptionEn] nvarchar(2000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [EndTime] time NOT NULL DEFAULT '00:00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [PresentationTitle] nvarchar(250) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [PresentationTitleEn] nvarchar(250) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [SpeakerName] nvarchar(150) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [StartTime] time NOT NULL DEFAULT '00:00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [TitleEn] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    ALTER TABLE [Sessions] ADD [UpdatedDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518110600_UpdateSessionForProgramModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518110600_UpdateSessionForProgramModule', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518203029_AddProceedingBookFieldsToConference'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsProceedingBookPublished] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518203029_AddProceedingBookFieldsToConference'
)
BEGIN
    ALTER TABLE [Conferences] ADD [ProceedingBookFilePath] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518203029_AddProceedingBookFieldsToConference'
)
BEGIN
    ALTER TABLE [Conferences] ADD [ProceedingBookPublishedDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518203029_AddProceedingBookFieldsToConference'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518203029_AddProceedingBookFieldsToConference', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523190805_AddCertificateSignersToConference'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523190805_AddCertificateSignersToConference', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524162502_AddCertificateSignersToConferences'
)
BEGIN
    ALTER TABLE [Conferences] ADD [CertificateFirstSignerName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524162502_AddCertificateSignersToConferences'
)
BEGIN
    ALTER TABLE [Conferences] ADD [CertificateFirstSignerTitle] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524162502_AddCertificateSignersToConferences'
)
BEGIN
    ALTER TABLE [Conferences] ADD [CertificateSecondSignerName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524162502_AddCertificateSignersToConferences'
)
BEGIN
    ALTER TABLE [Conferences] ADD [CertificateSecondSignerTitle] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524162502_AddCertificateSignersToConferences'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524162502_AddCertificateSignersToConferences', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524203000_AddReviewerProfileFieldsToUsers'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ReviewerConflictInstitutions] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524203000_AddReviewerProfileFieldsToUsers'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ReviewerConflictPeople] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524203000_AddReviewerProfileFieldsToUsers'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ReviewerUnavailableEndDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524203000_AddReviewerProfileFieldsToUsers'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ReviewerUnavailableReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524203000_AddReviewerProfileFieldsToUsers'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ReviewerUnavailableStartDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524203000_AddReviewerProfileFieldsToUsers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524203000_AddReviewerProfileFieldsToUsers', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614221632_AddRegistrationTypeRoleName'
)
BEGIN
    ALTER TABLE [ConferenceAttendances] DROP CONSTRAINT [FK_ConferenceAttendances_AspNetUsers_UserId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614221632_AddRegistrationTypeRoleName'
)
BEGIN
    ALTER TABLE [RegistrationTypes] ADD [RoleName] nvarchar(50) NOT NULL DEFAULT N'Author';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614221632_AddRegistrationTypeRoleName'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConferenceAttendances]') AND [c].[name] = N'UserAgent');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ConferenceAttendances] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [ConferenceAttendances] ALTER COLUMN [UserAgent] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614221632_AddRegistrationTypeRoleName'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConferenceAttendances]') AND [c].[name] = N'TotalSeconds');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ConferenceAttendances] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [ConferenceAttendances] ADD DEFAULT 0 FOR [TotalSeconds];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614221632_AddRegistrationTypeRoleName'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConferenceAttendances]') AND [c].[name] = N'RequiredSeconds');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ConferenceAttendances] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [ConferenceAttendances] ADD DEFAULT 600 FOR [RequiredSeconds];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614221632_AddRegistrationTypeRoleName'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConferenceAttendances]') AND [c].[name] = N'IpAddress');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [ConferenceAttendances] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [ConferenceAttendances] ALTER COLUMN [IpAddress] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614221632_AddRegistrationTypeRoleName'
)
BEGIN
    ALTER TABLE [ConferenceAttendances] ADD CONSTRAINT [FK_ConferenceAttendances_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614221632_AddRegistrationTypeRoleName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260614221632_AddRegistrationTypeRoleName', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614223249_AddEmailTemplates'
)
BEGIN
    CREATE TABLE [EmailTemplates] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(100) NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [HtmlBody] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailTemplates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614223249_AddEmailTemplates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260614223249_AddEmailTemplates', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614224940_AddReceiptFieldsToRegistration'
)
BEGIN
    ALTER TABLE [Registrations] ADD [AdminPaymentNote] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614224940_AddReceiptFieldsToRegistration'
)
BEGIN
    ALTER TABLE [Registrations] ADD [ReceiptFilePath] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614224940_AddReceiptFieldsToRegistration'
)
BEGIN
    ALTER TABLE [Registrations] ADD [ReceiptUploadedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260614224940_AddReceiptFieldsToRegistration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260614224940_AddReceiptFieldsToRegistration', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615120304_AddConferenceDeadlines'
)
BEGIN
    ALTER TABLE [Conferences] ADD [AbstractSubmissionDeadline] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615120304_AddConferenceDeadlines'
)
BEGIN
    ALTER TABLE [Conferences] ADD [FullTextSubmissionDeadline] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615120304_AddConferenceDeadlines'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsSubmissionOpen] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615120304_AddConferenceDeadlines'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615120304_AddConferenceDeadlines', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615123633_AddConferenceRegistrationQuota'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsRegistrationOpen] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615123633_AddConferenceRegistrationQuota'
)
BEGIN
    ALTER TABLE [Conferences] ADD [MaxRegistrations] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615123633_AddConferenceRegistrationQuota'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615123633_AddConferenceRegistrationQuota', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615131944_AddSubmissionAdminDecisionNote'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615131944_AddSubmissionAdminDecisionNote', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615162448_AddReviewSubScores'
)
BEGIN
    ALTER TABLE [Reviews] ADD [ScoreMethodology] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615162448_AddReviewSubScores'
)
BEGIN
    ALTER TABLE [Reviews] ADD [ScoreOriginality] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615162448_AddReviewSubScores'
)
BEGIN
    ALTER TABLE [Reviews] ADD [ScorePresentation] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615162448_AddReviewSubScores'
)
BEGIN
    ALTER TABLE [Reviews] ADD [ScoreRelevance] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615162448_AddReviewSubScores'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615162448_AddReviewSubScores', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164232_AddReviewAssignmentDeclineHistory'
)
BEGIN
    ALTER TABLE [ReviewAssignments] ADD [DeclineReason] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164232_AddReviewAssignmentDeclineHistory'
)
BEGIN
    ALTER TABLE [ReviewAssignments] ADD [DeclinedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164232_AddReviewAssignmentDeclineHistory'
)
BEGIN
    ALTER TABLE [ReviewAssignments] ADD [IsDeclined] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164232_AddReviewAssignmentDeclineHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615164232_AddReviewAssignmentDeclineHistory', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164413_AddAuditLog'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] nvarchar(max) NULL,
        [UserName] nvarchar(max) NULL,
        [Category] nvarchar(max) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [EntityType] nvarchar(max) NULL,
        [EntityId] nvarchar(max) NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [ConferenceId] uniqueidentifier NULL,
        [IpAddress] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164413_AddAuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615164413_AddAuditLog', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164541_AddSurveyAnswers'
)
BEGIN
    CREATE TABLE [SurveyAnswers] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [SubmissionId] uniqueidentifier NULL,
        [Answer1] nvarchar(2000) NULL,
        [Answer2] nvarchar(2000) NULL,
        [Answer3] nvarchar(2000) NULL,
        [Answer4] nvarchar(2000) NULL,
        [Answer5] nvarchar(2000) NULL,
        [SubmittedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SurveyAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SurveyAnswers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SurveyAnswers_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_SurveyAnswers_Submissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [Submissions] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164541_AddSurveyAnswers'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_ConferenceId] ON [SurveyAnswers] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164541_AddSurveyAnswers'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_SubmissionId] ON [SurveyAnswers] ([SubmissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164541_AddSurveyAnswers'
)
BEGIN
    CREATE INDEX [IX_SurveyAnswers_UserId] ON [SurveyAnswers] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615164541_AddSurveyAnswers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615164541_AddSurveyAnswers', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615170521_AddRegistrationQrToken'
)
BEGIN
    ALTER TABLE [Registrations] ADD [CheckedInAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615170521_AddRegistrationQrToken'
)
BEGIN
    ALTER TABLE [Registrations] ADD [CheckedInByUserId] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615170521_AddRegistrationQrToken'
)
BEGIN
    ALTER TABLE [Registrations] ADD [QrToken] nvarchar(64) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615170521_AddRegistrationQrToken'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615170521_AddRegistrationQrToken', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615171022_AddReviewCriteria'
)
BEGIN
    CREATE TABLE [ReviewCriteria] (
        [Id] uniqueidentifier NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [NameEn] nvarchar(150) NULL,
        [Description] nvarchar(500) NULL,
        [Weight] int NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ReviewCriteria] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewCriteria_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615171022_AddReviewCriteria'
)
BEGIN
    CREATE TABLE [ReviewCriterionScores] (
        [Id] uniqueidentifier NOT NULL,
        [ReviewCriterionId] uniqueidentifier NOT NULL,
        [ReviewId] int NOT NULL,
        [Score] int NOT NULL,
        [Comment] nvarchar(500) NULL,
        CONSTRAINT [PK_ReviewCriterionScores] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewCriterionScores_ReviewCriteria_ReviewCriterionId] FOREIGN KEY ([ReviewCriterionId]) REFERENCES [ReviewCriteria] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReviewCriterionScores_Reviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [Reviews] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615171022_AddReviewCriteria'
)
BEGIN
    CREATE INDEX [IX_ReviewCriteria_ConferenceId] ON [ReviewCriteria] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615171022_AddReviewCriteria'
)
BEGIN
    CREATE INDEX [IX_ReviewCriterionScores_ReviewCriterionId] ON [ReviewCriterionScores] ([ReviewCriterionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615171022_AddReviewCriteria'
)
BEGIN
    CREATE INDEX [IX_ReviewCriterionScores_ReviewId] ON [ReviewCriterionScores] ([ReviewId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615171022_AddReviewCriteria'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615171022_AddReviewCriteria', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615171424_AddRegistrationStatus'
)
BEGIN
    ALTER TABLE [Registrations] ADD [Status] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260615171424_AddRegistrationStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260615171424_AddRegistrationStatus', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Conferences]') AND [c].[name] = N'Slug');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Conferences] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Conferences] ALTER COLUMN [Slug] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditLogs]') AND [c].[name] = N'UserId');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [AuditLogs] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [AuditLogs] ALTER COLUMN [UserId] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditLogs]') AND [c].[name] = N'Category');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [AuditLogs] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [AuditLogs] ALTER COLUMN [Category] nvarchar(450) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Submissions_ConferenceId_Status] ON [Submissions] ([ConferenceId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Submissions_TenantId_ConferenceId] ON [Submissions] ([TenantId], [ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReviewAssignments_SubmissionId_ReviewerId] ON [ReviewAssignments] ([SubmissionId], [ReviewerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Registrations_ConferenceId_Status] ON [Registrations] ([ConferenceId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [Notifications] ([UserId], [IsRead]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_Conferences_Slug] ON [Conferences] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Category] ON [AuditLogs] ([Category]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ConferenceId] ON [AuditLogs] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ConferenceId_CreatedAt] ON [AuditLogs] ([ConferenceId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616011125_AddPerformanceIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260616011125_AddPerformanceIndexes', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616013947_AddPaymentHistoryAndWebhookLog'
)
BEGIN
    CREATE TABLE [PaymentStatusHistories] (
        [Id] int NOT NULL IDENTITY,
        [PaymentId] uniqueidentifier NOT NULL,
        [OldStatus] int NULL,
        [NewStatus] int NOT NULL,
        [ChangedByUserId] nvarchar(100) NULL,
        [ChangedByUserName] nvarchar(200) NULL,
        [ChangedAt] datetime2 NOT NULL,
        [Note] nvarchar(500) NULL,
        [Source] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_PaymentStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentStatusHistories_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616013947_AddPaymentHistoryAndWebhookLog'
)
BEGIN
    CREATE TABLE [StripeWebhookEvents] (
        [Id] int NOT NULL IDENTITY,
        [StripeEventId] nvarchar(100) NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [ReceivedAt] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [PaymentId] uniqueidentifier NULL,
        [StripeObjectId] nvarchar(200) NULL,
        [PayloadPreview] nvarchar(4000) NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [IsDuplicate] bit NOT NULL,
        CONSTRAINT [PK_StripeWebhookEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616013947_AddPaymentHistoryAndWebhookLog'
)
BEGIN
    CREATE INDEX [IX_PaymentStatusHistories_PaymentId] ON [PaymentStatusHistories] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616013947_AddPaymentHistoryAndWebhookLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260616013947_AddPaymentHistoryAndWebhookLog', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616091646_AddEmailLog'
)
BEGIN
    CREATE TABLE [EmailLogs] (
        [Id] int NOT NULL IDENTITY,
        [ToEmail] nvarchar(320) NOT NULL,
        [Subject] nvarchar(500) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [TemplateKey] nvarchar(100) NULL,
        [Source] nvarchar(100) NULL,
        CONSTRAINT [PK_EmailLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616091646_AddEmailLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260616091646_AddEmailLog', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618154254_AddReviewBidAndBiddingPhase'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsBiddingOpen] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618154254_AddReviewBidAndBiddingPhase'
)
BEGIN
    CREATE TABLE [ReviewBids] (
        [Id] int NOT NULL IDENTITY,
        [SubmissionId] uniqueidentifier NOT NULL,
        [ReviewerId] nvarchar(450) NOT NULL,
        [Preference] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ReviewBids] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewBids_AspNetUsers_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ReviewBids_Submissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [Submissions] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618154254_AddReviewBidAndBiddingPhase'
)
BEGIN
    CREATE INDEX [IX_ReviewBids_ReviewerId] ON [ReviewBids] ([ReviewerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618154254_AddReviewBidAndBiddingPhase'
)
BEGIN
    CREATE INDEX [IX_ReviewBids_SubmissionId] ON [ReviewBids] ([SubmissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618154254_AddReviewBidAndBiddingPhase'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618154254_AddReviewBidAndBiddingPhase', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618160509_AddRebuttalToSubmission'
)
BEGIN
    ALTER TABLE [Submissions] ADD [RebuttalDate] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618160509_AddRebuttalToSubmission'
)
BEGIN
    ALTER TABLE [Submissions] ADD [RebuttalText] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618160509_AddRebuttalToSubmission'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618160509_AddRebuttalToSubmission', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618190239_AddIsFullTextOpenToConference'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsFullTextOpen] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618190239_AddIsFullTextOpenToConference'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618190239_AddIsFullTextOpenToConference', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618191913_AddReviewerInvitation'
)
BEGIN
    CREATE TABLE [ReviewerInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [FirstName] nvarchar(max) NULL,
        [LastName] nvarchar(max) NULL,
        [Institution] nvarchar(max) NULL,
        [InvitedUserId] nvarchar(450) NULL,
        [Token] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RespondedAt] datetime2 NULL,
        [DeclineReason] nvarchar(max) NULL,
        [CreatedReviewerUserId] nvarchar(max) NULL,
        CONSTRAINT [PK_ReviewerInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReviewerInvitations_AspNetUsers_InvitedUserId] FOREIGN KEY ([InvitedUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ReviewerInvitations_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618191913_AddReviewerInvitation'
)
BEGIN
    CREATE INDEX [IX_ReviewerInvitations_ConferenceId] ON [ReviewerInvitations] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618191913_AddReviewerInvitation'
)
BEGIN
    CREATE INDEX [IX_ReviewerInvitations_InvitedUserId] ON [ReviewerInvitations] ([InvitedUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618191913_AddReviewerInvitation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618191913_AddReviewerInvitation', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618201807_AddConferencePaymentConfig'
)
BEGIN
    ALTER TABLE [Conferences] ADD [BankAccountName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618201807_AddConferencePaymentConfig'
)
BEGIN
    ALTER TABLE [Conferences] ADD [BankBranch] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618201807_AddConferencePaymentConfig'
)
BEGIN
    ALTER TABLE [Conferences] ADD [BankIban] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618201807_AddConferencePaymentConfig'
)
BEGIN
    ALTER TABLE [Conferences] ADD [BankName] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618201807_AddConferencePaymentConfig'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsBankTransferEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618201807_AddConferencePaymentConfig'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsPayTREnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618201807_AddConferencePaymentConfig'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsStripeEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618201807_AddConferencePaymentConfig'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618201807_AddConferencePaymentConfig', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618204658_AddInvitedSpeakers'
)
BEGIN
    CREATE TABLE [InvitedSpeakers] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Title] nvarchar(200) NULL,
        [Institution] nvarchar(300) NULL,
        [Country] nvarchar(100) NULL,
        [PhotoUrl] nvarchar(500) NULL,
        [TalkTitle] nvarchar(300) NULL,
        [Bio] nvarchar(3000) NULL,
        [WebsiteUrl] nvarchar(200) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_InvitedSpeakers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvitedSpeakers_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618204658_AddInvitedSpeakers'
)
BEGIN
    CREATE INDEX [IX_InvitedSpeakers_ConferenceId] ON [InvitedSpeakers] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618204658_AddInvitedSpeakers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618204658_AddInvitedSpeakers', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618205104_AddSponsors'
)
BEGIN
    CREATE TABLE [Sponsors] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [LogoUrl] nvarchar(500) NULL,
        [WebsiteUrl] nvarchar(200) NULL,
        [Tier] int NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ConferenceId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Sponsors] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sponsors_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618205104_AddSponsors'
)
BEGIN
    CREATE INDEX [IX_Sponsors_ConferenceId] ON [Sponsors] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260618205104_AddSponsors'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260618205104_AddSponsors', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619104556_AddScheduledBroadcast'
)
BEGIN
    CREATE TABLE [ScheduledBroadcasts] (
        [Id] int NOT NULL IDENTITY,
        [ConferenceId] uniqueidentifier NOT NULL,
        [TargetGroup] nvarchar(50) NOT NULL,
        [Subject] nvarchar(500) NOT NULL,
        [HtmlBody] nvarchar(max) NOT NULL,
        [ScheduledAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [SentAt] datetime2 NULL,
        [RecipientCount] int NOT NULL,
        [Status] int NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ScheduledBroadcasts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ScheduledBroadcasts_AspNetUsers_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_ScheduledBroadcasts_Conferences_ConferenceId] FOREIGN KEY ([ConferenceId]) REFERENCES [Conferences] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619104556_AddScheduledBroadcast'
)
BEGIN
    CREATE INDEX [IX_ScheduledBroadcasts_ConferenceId] ON [ScheduledBroadcasts] ([ConferenceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619104556_AddScheduledBroadcast'
)
BEGIN
    CREATE INDEX [IX_ScheduledBroadcasts_CreatedByUserId] ON [ScheduledBroadcasts] ([CreatedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619104556_AddScheduledBroadcast'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619104556_AddScheduledBroadcast', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619161456_AddIsBlindReviewToConference'
)
BEGIN
    ALTER TABLE [Conferences] ADD [IsBlindReview] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619161456_AddIsBlindReviewToConference'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619161456_AddIsBlindReviewToConference', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619162701_AddPlagiarismReports'
)
BEGIN
    CREATE TABLE [PlagiarismReports] (
        [Id] uniqueidentifier NOT NULL,
        [SubmissionId] uniqueidentifier NOT NULL,
        [SubmissionFileId] int NULL,
        [ExternalId] nvarchar(200) NULL,
        [Status] int NOT NULL,
        [SimilarityScore] int NULL,
        [ReportUrl] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [Provider] nvarchar(50) NOT NULL,
        [RequestedByUserId] nvarchar(450) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PlagiarismReports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlagiarismReports_AspNetUsers_RequestedByUserId] FOREIGN KEY ([RequestedByUserId]) REFERENCES [AspNetUsers] ([Id]),
        CONSTRAINT [FK_PlagiarismReports_SubmissionFiles_SubmissionFileId] FOREIGN KEY ([SubmissionFileId]) REFERENCES [SubmissionFiles] ([Id]),
        CONSTRAINT [FK_PlagiarismReports_Submissions_SubmissionId] FOREIGN KEY ([SubmissionId]) REFERENCES [Submissions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619162701_AddPlagiarismReports'
)
BEGIN
    CREATE INDEX [IX_PlagiarismReports_RequestedByUserId] ON [PlagiarismReports] ([RequestedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619162701_AddPlagiarismReports'
)
BEGIN
    CREATE INDEX [IX_PlagiarismReports_SubmissionFileId] ON [PlagiarismReports] ([SubmissionFileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619162701_AddPlagiarismReports'
)
BEGIN
    CREATE INDEX [IX_PlagiarismReports_SubmissionId] ON [PlagiarismReports] ([SubmissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619162701_AddPlagiarismReports'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619162701_AddPlagiarismReports', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619181558_AddWebhookEventProvider'
)
BEGIN
    ALTER TABLE [StripeWebhookEvents] ADD [Provider] nvarchar(20) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619181558_AddWebhookEventProvider'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619181558_AddWebhookEventProvider', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619212325_AddDoiUrlToSubmission'
)
BEGIN
    ALTER TABLE [Submissions] ADD [DoiUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260619212325_AddDoiUrlToSubmission'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260619212325_AddDoiUrlToSubmission', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621211637_AddLiveStreamAndMessageReply'
)
BEGIN
    ALTER TABLE [Sessions] ADD [LiveStreamPlatform] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621211637_AddLiveStreamAndMessageReply'
)
BEGIN
    ALTER TABLE [Sessions] ADD [LiveStreamUrl] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621211637_AddLiveStreamAndMessageReply'
)
BEGIN
    ALTER TABLE [Messages] ADD [ParentMessageId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621211637_AddLiveStreamAndMessageReply'
)
BEGIN
    CREATE INDEX [IX_Messages_ParentMessageId] ON [Messages] ([ParentMessageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621211637_AddLiveStreamAndMessageReply'
)
BEGIN
    ALTER TABLE [Messages] ADD CONSTRAINT [FK_Messages_Messages_ParentMessageId] FOREIGN KEY ([ParentMessageId]) REFERENCES [Messages] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621211637_AddLiveStreamAndMessageReply'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621211637_AddLiveStreamAndMessageReply', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623182750_AddDoiWorkflowColumns'
)
BEGIN

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Submissions') AND name = 'DoiStatus')
                    BEGIN
                        ALTER TABLE [Submissions] ADD [DoiStatus] int NOT NULL DEFAULT 0;
                        ALTER TABLE [Submissions] ADD [DoiProvider] nvarchar(50) NULL;
                        ALTER TABLE [Submissions] ADD [DoiErrorMessage] nvarchar(1000) NULL;
                        ALTER TABLE [Submissions] ADD [DoiRequestedAt] datetime2 NULL;
                        ALTER TABLE [Submissions] ADD [DoiAssignedAt] datetime2 NULL;
                    END
                
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260623182750_AddDoiWorkflowColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260623182750_AddDoiWorkflowColumns', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624193816_ConferenceSlugRequiredAndExternalUrl'
)
BEGIN
    DROP INDEX [IX_Conferences_Slug] ON [Conferences];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624193816_ConferenceSlugRequiredAndExternalUrl'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Conferences]') AND [c].[name] = N'Slug');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Conferences] DROP CONSTRAINT [' + @var8 + '];');
    EXEC(N'UPDATE [Conferences] SET [Slug] = N'''' WHERE [Slug] IS NULL');
    ALTER TABLE [Conferences] ALTER COLUMN [Slug] nvarchar(200) NOT NULL;
    ALTER TABLE [Conferences] ADD DEFAULT N'' FOR [Slug];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624193816_ConferenceSlugRequiredAndExternalUrl'
)
BEGIN
    ALTER TABLE [Conferences] ADD [ExternalWebsiteUrl] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624193816_ConferenceSlugRequiredAndExternalUrl'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Conferences_Slug] ON [Conferences] ([Slug]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260624193816_ConferenceSlugRequiredAndExternalUrl'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260624193816_ConferenceSlugRequiredAndExternalUrl', N'8.0.20');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625205029_AddConferenceEnglishFields'
)
BEGIN
    ALTER TABLE [Conferences] ADD [CityEn] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625205029_AddConferenceEnglishFields'
)
BEGIN
    ALTER TABLE [Conferences] ADD [CountryEn] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625205029_AddConferenceEnglishFields'
)
BEGIN
    ALTER TABLE [Conferences] ADD [DescriptionEn] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625205029_AddConferenceEnglishFields'
)
BEGIN
    ALTER TABLE [Conferences] ADD [TitleEn] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625205029_AddConferenceEnglishFields'
)
BEGIN
    ALTER TABLE [Conferences] ADD [VenueEn] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625205029_AddConferenceEnglishFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260625205029_AddConferenceEnglishFields', N'8.0.20');
END;
GO

COMMIT;
GO

