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

