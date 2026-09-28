:setvar DatabaseName "VeeranganaYouTubeAnalytics"

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [$(DatabaseName)]');
END;
GO

USE [$(DatabaseName)];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersion
    (
        VersionNumber INT NOT NULL,
        Description NVARCHAR(250) NOT NULL,
        AppliedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_SchemaVersion_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SchemaVersion PRIMARY KEY CLUSTERED (VersionNumber)
    );
END;

-- YouTube Data API metadata. Analytics API historical results are queried on demand and are not duplicated here.
IF OBJECT_ID(N'dbo.VideoMetadata', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VideoMetadata
    (
        VideoId NVARCHAR(32) NOT NULL,
        ChannelId NVARCHAR(64) NOT NULL,
        Title NVARCHAR(500) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        ThumbnailUrl NVARCHAR(1000) NULL,
        PublishedAtUtc DATETIME2(0) NULL,
        DurationSeconds INT NULL,
        ContentType VARCHAR(20) NOT NULL CONSTRAINT DF_VideoMetadata_ContentType DEFAULT ('Video'),
        IsActive BIT NOT NULL CONSTRAINT DF_VideoMetadata_IsActive DEFAULT (1),
        LastSyncedAtUtc DATETIME2(0) NOT NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_VideoMetadata_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_VideoMetadata_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_VideoMetadata PRIMARY KEY CLUSTERED (VideoId),
        CONSTRAINT CK_VideoMetadata_DurationSeconds CHECK (DurationSeconds IS NULL OR DurationSeconds >= 0),
        CONSTRAINT CK_VideoMetadata_ContentType CHECK (ContentType IN ('Video', 'Short', 'Live'))
    );

    CREATE INDEX IX_VideoMetadata_Channel_Published
        ON dbo.VideoMetadata (ChannelId, PublishedAtUtc DESC);
END;

-- Point-in-time public counters retained for independent trend and comparison reporting.
IF OBJECT_ID(N'dbo.ChannelSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChannelSnapshot
    (
        Id BIGINT IDENTITY(1, 1) NOT NULL,
        ChannelId NVARCHAR(64) NOT NULL,
        SnapshotDate DATE NOT NULL,
        Views BIGINT NOT NULL,
        Subscribers BIGINT NULL,
        VideoCount INT NOT NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ChannelSnapshot_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ChannelSnapshot PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_ChannelSnapshot_Channel_Date UNIQUE (ChannelId, SnapshotDate),
        CONSTRAINT CK_ChannelSnapshot_Views CHECK (Views >= 0),
        CONSTRAINT CK_ChannelSnapshot_Subscribers CHECK (Subscribers IS NULL OR Subscribers >= 0),
        CONSTRAINT CK_ChannelSnapshot_VideoCount CHECK (VideoCount >= 0)
    );
END;

IF OBJECT_ID(N'dbo.VideoSnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VideoSnapshot
    (
        Id BIGINT IDENTITY(1, 1) NOT NULL,
        VideoId NVARCHAR(32) NOT NULL,
        SnapshotDate DATE NOT NULL,
        Views BIGINT NOT NULL,
        Likes BIGINT NULL,
        Comments BIGINT NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_VideoSnapshot_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_VideoSnapshot PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_VideoSnapshot_VideoMetadata FOREIGN KEY (VideoId) REFERENCES dbo.VideoMetadata (VideoId),
        CONSTRAINT UQ_VideoSnapshot_Video_Date UNIQUE (VideoId, SnapshotDate),
        CONSTRAINT CK_VideoSnapshot_Views CHECK (Views >= 0),
        CONSTRAINT CK_VideoSnapshot_Likes CHECK (Likes IS NULL OR Likes >= 0),
        CONSTRAINT CK_VideoSnapshot_Comments CHECK (Comments IS NULL OR Comments >= 0)
    );
END;

IF OBJECT_ID(N'dbo.AnalyticsSyncLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsSyncLog
    (
        Id BIGINT IDENTITY(1, 1) NOT NULL,
        SyncType VARCHAR(40) NOT NULL,
        Status VARCHAR(20) NOT NULL,
        StartedAtUtc DATETIME2(0) NOT NULL,
        CompletedAtUtc DATETIME2(0) NULL,
        RecordsProcessed INT NOT NULL CONSTRAINT DF_AnalyticsSyncLog_RecordsProcessed DEFAULT (0),
        ErrorCode NVARCHAR(100) NULL,
        ErrorMessage NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_AnalyticsSyncLog_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AnalyticsSyncLog PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_AnalyticsSyncLog_Status CHECK (Status IN ('Started', 'Completed', 'Failed', 'Cancelled')),
        CONSTRAINT CK_AnalyticsSyncLog_RecordsProcessed CHECK (RecordsProcessed >= 0)
    );

    CREATE INDEX IX_AnalyticsSyncLog_Status_Started
        ON dbo.AnalyticsSyncLog (Status, StartedAtUtc DESC);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 1)
BEGIN
    INSERT dbo.SchemaVersion (VersionNumber, Description)
    VALUES (1, N'Initial video metadata, public snapshots, and synchronization log schema');
END;

COMMIT TRANSACTION;
GO
