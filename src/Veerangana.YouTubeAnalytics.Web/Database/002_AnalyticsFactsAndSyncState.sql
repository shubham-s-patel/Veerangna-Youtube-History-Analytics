SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ChannelMetadata', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChannelMetadata
    (
        ChannelId NVARCHAR(64) NOT NULL,
        Title NVARCHAR(250) NOT NULL,
        AvatarUrl NVARCHAR(1000) NULL,
        PublishedAtUtc DATETIME2(0) NULL,
        LastSyncedAtUtc DATETIME2(0) NOT NULL,
        CONSTRAINT PK_ChannelMetadata PRIMARY KEY CLUSTERED (ChannelId)
    );
END;

IF OBJECT_ID(N'dbo.ChannelAnalyticsDaily', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChannelAnalyticsDaily
    (
        ChannelId NVARCHAR(64) NOT NULL,
        MetricDate DATE NOT NULL,
        CountryCode VARCHAR(2) NOT NULL CONSTRAINT DF_ChannelAnalyticsDaily_Country DEFAULT (''),
        ContentType VARCHAR(20) NOT NULL,
        Views BIGINT NOT NULL,
        WatchTimeMinutes DECIMAL(20, 4) NOT NULL,
        AverageViewDurationSeconds DECIMAL(18, 4) NOT NULL,
        AverageViewPercentage DECIMAL(18, 4) NOT NULL,
        Likes BIGINT NOT NULL,
        Comments BIGINT NOT NULL,
        Shares BIGINT NOT NULL,
        SubscribersGained BIGINT NOT NULL,
        SubscribersLost BIGINT NOT NULL,
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ChannelAnalyticsDaily_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ChannelAnalyticsDaily PRIMARY KEY CLUSTERED
            (ChannelId, MetricDate, CountryCode, ContentType),
        CONSTRAINT CK_ChannelAnalyticsDaily_ContentType CHECK
            (ContentType IN ('Video', 'Short', 'Live', 'Story', 'Unspecified'))
    );

    CREATE INDEX IX_ChannelAnalyticsDaily_Query
        ON dbo.ChannelAnalyticsDaily (ChannelId, CountryCode, MetricDate)
        INCLUDE (ContentType, Views, WatchTimeMinutes, AverageViewDurationSeconds,
                 AverageViewPercentage, Likes, Comments, Shares,
                 SubscribersGained, SubscribersLost);
END;

IF OBJECT_ID(N'dbo.VideoAnalyticsDaily', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VideoAnalyticsDaily
    (
        VideoId NVARCHAR(32) NOT NULL,
        MetricDate DATE NOT NULL,
        CountryCode VARCHAR(2) NOT NULL CONSTRAINT DF_VideoAnalyticsDaily_Country DEFAULT (''),
        Views BIGINT NOT NULL,
        WatchTimeMinutes DECIMAL(20, 4) NOT NULL,
        AverageViewDurationSeconds DECIMAL(18, 4) NOT NULL,
        AverageViewPercentage DECIMAL(18, 4) NOT NULL,
        Likes BIGINT NOT NULL,
        Comments BIGINT NOT NULL,
        Shares BIGINT NOT NULL,
        SubscribersGained BIGINT NOT NULL,
        SubscribersLost BIGINT NOT NULL,
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_VideoAnalyticsDaily_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_VideoAnalyticsDaily PRIMARY KEY CLUSTERED (VideoId, MetricDate, CountryCode),
        CONSTRAINT FK_VideoAnalyticsDaily_VideoMetadata FOREIGN KEY (VideoId)
            REFERENCES dbo.VideoMetadata (VideoId)
    );

    CREATE INDEX IX_VideoAnalyticsDaily_Country_Date
        ON dbo.VideoAnalyticsDaily (CountryCode, MetricDate, VideoId)
        INCLUDE (Views, WatchTimeMinutes, AverageViewDurationSeconds,
                 AverageViewPercentage, Likes, Comments, Shares,
                 SubscribersGained, SubscribersLost);
END;

IF OBJECT_ID(N'dbo.TrafficSourceAnalyticsDaily', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TrafficSourceAnalyticsDaily
    (
        ChannelId NVARCHAR(64) NOT NULL,
        MetricDate DATE NOT NULL,
        CountryCode VARCHAR(2) NOT NULL CONSTRAINT DF_TrafficSourceDaily_Country DEFAULT (''),
        ContentType VARCHAR(20) NOT NULL,
        TrafficSource VARCHAR(80) NOT NULL,
        Views BIGINT NOT NULL,
        WatchTimeMinutes DECIMAL(20, 4) NOT NULL,
        AverageViewDurationSeconds DECIMAL(18, 4) NOT NULL,
        AverageViewPercentage DECIMAL(18, 4) NOT NULL,
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_TrafficSourceDaily_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TrafficSourceAnalyticsDaily PRIMARY KEY CLUSTERED
            (ChannelId, MetricDate, CountryCode, ContentType, TrafficSource),
        CONSTRAINT CK_TrafficSourceDaily_ContentType CHECK
            (ContentType IN ('Video', 'Short', 'Live', 'Story', 'Unspecified'))
    );

    CREATE INDEX IX_TrafficSourceDaily_Query
        ON dbo.TrafficSourceAnalyticsDaily (ChannelId, CountryCode, MetricDate)
        INCLUDE (ContentType, TrafficSource, Views, WatchTimeMinutes,
                 AverageViewDurationSeconds, AverageViewPercentage);
END;

IF OBJECT_ID(N'dbo.AnalyticsSyncState', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsSyncState
    (
        Id TINYINT NOT NULL,
        IsRunning BIT NOT NULL CONSTRAINT DF_AnalyticsSyncState_Running DEFAULT (0),
        LeaseExpiresAtUtc DATETIME2(0) NULL,
        LastStartedAtUtc DATETIME2(0) NULL,
        LastCompletedAtUtc DATETIME2(0) NULL,
        OldestSyncedDate DATE NULL,
        LatestSyncedDate DATE NULL,
        LastErrorMessage NVARCHAR(1000) NULL,
        CONSTRAINT PK_AnalyticsSyncState PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_AnalyticsSyncState_Singleton CHECK (Id = 1)
    );

    INSERT dbo.AnalyticsSyncState (Id) VALUES (1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 2)
BEGIN
    INSERT dbo.SchemaVersion (VersionNumber, Description)
    VALUES (2, N'Daily analytics facts, channel metadata, and synchronized job state');
END;

COMMIT TRANSACTION;
