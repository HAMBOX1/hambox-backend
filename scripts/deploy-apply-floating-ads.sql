-- Floating ads (a small offer card shown over the storefront, with an audience such as "customers without a membership").
-- Idempotent; run on production BEFORE deploying the code that reads this table.
IF OBJECT_ID(N'[commerce].[FloatingAds]', N'U') IS NULL
BEGIN
    CREATE TABLE [commerce].[FloatingAds] (
        [Id] uniqueidentifier NOT NULL,
        [TitleEn] nvarchar(200) NOT NULL,
        [TitleAr] nvarchar(200) NULL,
        [MessageEn] nvarchar(1000) NULL,
        [MessageAr] nvarchar(1000) NULL,
        [ButtonLabelEn] nvarchar(100) NULL,
        [ButtonLabelAr] nvarchar(100) NULL,
        [LinkUrl] nvarchar(500) NULL,
        [Audience] nvarchar(32) NOT NULL,
        [IsActive] bit NOT NULL,
        [StartsOnUtc] datetimeoffset NULL,
        [EndsOnUtc] datetimeoffset NULL,
        [SortOrder] int NOT NULL,
        [DismissHours] int NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_FloatingAds] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FloatingAds_IsActive_SortOrder' AND object_id = OBJECT_ID(N'[commerce].[FloatingAds]'))
BEGIN
    CREATE INDEX [IX_FloatingAds_IsActive_SortOrder] ON [commerce].[FloatingAds] ([IsActive], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT 1 FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030152018_AddFloatingAds')
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030152018_AddFloatingAds', N'10.0.9');
END;
