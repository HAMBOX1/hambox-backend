IF COL_LENGTH('identity.Users', 'PreferredLanguage') IS NULL
BEGIN
    ALTER TABLE [identity].[Users]
    ADD [PreferredLanguage] nvarchar(5) NOT NULL
        CONSTRAINT [DF_identity_Users_PreferredLanguage] DEFAULT ('en');
END;

IF COL_LENGTH('identity.Users', 'PreferredCurrency') IS NULL
BEGIN
    ALTER TABLE [identity].[Users]
    ADD [PreferredCurrency] nvarchar(3) NOT NULL
        CONSTRAINT [DF_identity_Users_PreferredCurrency] DEFAULT ('USD');
END;

IF NOT EXISTS (
    SELECT 1 FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260626120000_AddUserPreferredLanguage')
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260626120000_AddUserPreferredLanguage', N'10.0.9');
END;

IF NOT EXISTS (
    SELECT 1 FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260626140000_AddUserPreferredCurrency')
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260626140000_AddUserPreferredCurrency', N'10.0.9');
END;
