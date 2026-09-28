SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.VideoAnalyticsDaily', N'CountryCode') IS NULL
BEGIN
    ALTER TABLE dbo.VideoAnalyticsDaily
        ADD CountryCode VARCHAR(2) NOT NULL
            CONSTRAINT DF_VideoAnalyticsDaily_Country DEFAULT ('') WITH VALUES;

    ALTER TABLE dbo.VideoAnalyticsDaily DROP CONSTRAINT PK_VideoAnalyticsDaily;
    ALTER TABLE dbo.VideoAnalyticsDaily ADD CONSTRAINT PK_VideoAnalyticsDaily
        PRIMARY KEY CLUSTERED (VideoId, MetricDate, CountryCode);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.VideoAnalyticsDaily')
      AND name = N'IX_VideoAnalyticsDaily_Country_Date'
)
BEGIN
    CREATE INDEX IX_VideoAnalyticsDaily_Country_Date
        ON dbo.VideoAnalyticsDaily (CountryCode, MetricDate, VideoId)
        INCLUDE (Views, WatchTimeMinutes, AverageViewDurationSeconds,
                 AverageViewPercentage, Likes, Comments, Shares,
                 SubscribersGained, SubscribersLost);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 3)
BEGIN
    INSERT dbo.SchemaVersion (VersionNumber, Description)
    VALUES (3, N'Country-specific daily video analytics');
END;

COMMIT TRANSACTION;
