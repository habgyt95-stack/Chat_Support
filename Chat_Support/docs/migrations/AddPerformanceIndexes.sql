-- =============================================================================
-- Performance Indexes Migration Script
-- بهبود عملکرد query‌های پرکاربرد
-- =============================================================================

-- این script idempotent است - می‌توان چندین بار اجرا کرد بدون مشکل

PRINT N'شروع اعمال Performance Indexes...';
GO

-- =============================================================================
-- 1. Index برای GetChatRooms query (بیشترین استفاده)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatRoomMembers_UserId_IncludeAll' AND object_id = OBJECT_ID('ChatRoomMembers'))
BEGIN
    PRINT N'ایجاد index: IX_ChatRoomMembers_UserId_IncludeAll';
    CREATE NONCLUSTERED INDEX IX_ChatRoomMembers_UserId_IncludeAll
    ON ChatRoomMembers(UserId)
    INCLUDE (ChatRoomId, JoinedAt, IsMuted, Role)
    WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);
    
    PRINT N'✓ Index IX_ChatRoomMembers_UserId_IncludeAll ایجاد شد';
END
ELSE
BEGIN
    PRINT N'⚠ Index IX_ChatRoomMembers_UserId_IncludeAll از قبل وجود دارد';
END
GO

-- =============================================================================
-- 2. Index برای GetChatMessages query
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_RoomId_CreatedAt' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    PRINT N'ایجاد index: IX_ChatMessages_RoomId_CreatedAt';
    CREATE NONCLUSTERED INDEX IX_ChatMessages_RoomId_CreatedAt
    ON ChatMessages(ChatRoomId, CreatedAt DESC)
    INCLUDE (SenderId, Content, MessageType, IsDeleted)
    WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);
    
    PRINT N'✓ Index IX_ChatMessages_RoomId_CreatedAt ایجاد شد';
END
ELSE
BEGIN
    PRINT N'⚠ Index IX_ChatMessages_RoomId_CreatedAt از قبل وجود دارد';
END
GO

-- =============================================================================
-- 3. Index برای unread messages count
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MessageStatus_UserId_IsRead' AND object_id = OBJECT_ID('MessageStatus'))
BEGIN
    PRINT N'ایجاد index: IX_MessageStatus_UserId_IsRead';
    CREATE NONCLUSTERED INDEX IX_MessageStatus_UserId_IsRead
    ON MessageStatus(UserId, IsRead)
    INCLUDE (MessageId, ReadAt)
    WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);
    
    PRINT N'✓ Index IX_MessageStatus_UserId_IsRead ایجاد شد';
END
ELSE
BEGIN
    PRINT N'⚠ Index IX_MessageStatus_UserId_IsRead از قبل وجود دارد';
END
GO

-- =============================================================================
-- 4. Index برای presence tracking queries
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserConnections_UserId_IsActive' AND object_id = OBJECT_ID('UserConnections'))
BEGIN
    PRINT N'ایجاد index: IX_UserConnections_UserId_IsActive';
    CREATE NONCLUSTERED INDEX IX_UserConnections_UserId_IsActive
    ON UserConnections(UserId, IsActive)
    INCLUDE (ConnectionId, ConnectedAt)
    WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);
    
    PRINT N'✓ Index IX_UserConnections_UserId_IsActive ایجاد شد';
END
ELSE
BEGIN
    PRINT N'⚠ Index IX_UserConnections_UserId_IsActive از قبل وجود دارد';
END
GO

-- =============================================================================
-- 5. Index برای support agent queries
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupportAgents_IsActive_RegionId' AND object_id = OBJECT_ID('SupportAgents'))
BEGIN
    PRINT N'ایجاد index: IX_SupportAgents_IsActive_RegionId';
    CREATE NONCLUSTERED INDEX IX_SupportAgents_IsActive_RegionId
    ON SupportAgents(IsActive, RegionId)
    INCLUDE (UserId, MaxConcurrentChats, LastActivityAt)
    WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);
    
    PRINT N'✓ Index IX_SupportAgents_IsActive_RegionId ایجاد شد';
END
ELSE
BEGIN
    PRINT N'⚠ Index IX_SupportAgents_IsActive_RegionId از قبل وجود دارد';
END
GO

-- =============================================================================
-- 6. Index برای chat room lookup by type
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatRooms_Type_IsDeleted' AND object_id = OBJECT_ID('ChatRooms'))
BEGIN
    PRINT N'ایجاد index: IX_ChatRooms_Type_IsDeleted';
    CREATE NONCLUSTERED INDEX IX_ChatRooms_Type_IsDeleted
    ON ChatRooms(Type, IsDeleted)
    INCLUDE (Name, CreatedBy, CreatedAt)
    WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);
    
    PRINT N'✓ Index IX_ChatRooms_Type_IsDeleted ایجاد شد';
END
ELSE
BEGIN
    PRINT N'⚠ Index IX_ChatRooms_Type_IsDeleted از قبل وجود دارد';
END
GO

-- =============================================================================
-- 7. Index برای message reactions (اگر جدول وجود دارد)
-- =============================================================================
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MessageReactions')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MessageReactions_MessageId_UserId' AND object_id = OBJECT_ID('MessageReactions'))
    BEGIN
        PRINT N'ایجاد index: IX_MessageReactions_MessageId_UserId';
        CREATE NONCLUSTERED INDEX IX_MessageReactions_MessageId_UserId
        ON MessageReactions(MessageId, UserId)
        INCLUDE (ReactionType, CreatedAt)
        WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);
        
        PRINT N'✓ Index IX_MessageReactions_MessageId_UserId ایجاد شد';
    END
    ELSE
    BEGIN
        PRINT N'⚠ Index IX_MessageReactions_MessageId_UserId از قبل وجود دارد';
    END
END
GO

-- =============================================================================
-- 8. Composite Index برای Support Chat queries
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatRooms_Type_Status_CreatedAt' AND object_id = OBJECT_ID('ChatRooms'))
BEGIN
    PRINT N'ایجاد index: IX_ChatRooms_Type_Status_CreatedAt';
    CREATE NONCLUSTERED INDEX IX_ChatRooms_Type_Status_CreatedAt
    ON ChatRooms(Type, SupportChatStatus, CreatedAt DESC)
    INCLUDE (Id, Name, CreatedBy, SupportAgentId)
    WITH (ONLINE = ON, SORT_IN_TEMPDB = ON);
    
    PRINT N'✓ Index IX_ChatRooms_Type_Status_CreatedAt ایجاد شد';
END
ELSE
BEGIN
    PRINT N'⚠ Index IX_ChatRooms_Type_Status_CreatedAt از قبل وجود دارد';
END
GO

-- =============================================================================
-- به‌روزرسانی Statistics
-- =============================================================================
PRINT N'به‌روزرسانی Statistics جداول...';

UPDATE STATISTICS ChatRoomMembers WITH FULLSCAN;
UPDATE STATISTICS ChatMessages WITH FULLSCAN;
UPDATE STATISTICS MessageStatus WITH FULLSCAN;
UPDATE STATISTICS UserConnections WITH FULLSCAN;
UPDATE STATISTICS SupportAgents WITH FULLSCAN;
UPDATE STATISTICS ChatRooms WITH FULLSCAN;

PRINT N'✓ Statistics به‌روز شدند';
GO

-- =============================================================================
-- بررسی فضای استفاده شده توسط indexes
-- =============================================================================
PRINT N'';
PRINT N'=============================================================================';
PRINT N'گزارش اندازه Indexes:';
PRINT N'=============================================================================';

SELECT 
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    (SUM(s.used_page_count) * 8) / 1024.0 AS SizeMB
FROM sys.indexes i
INNER JOIN sys.dm_db_partition_stats s 
    ON i.object_id = s.object_id 
    AND i.index_id = s.index_id
WHERE i.name LIKE 'IX_%'
    AND OBJECT_NAME(i.object_id) IN ('ChatRoomMembers', 'ChatMessages', 'MessageStatus', 'UserConnections', 'SupportAgents', 'ChatRooms', 'MessageReactions')
GROUP BY i.object_id, i.name, i.type_desc
ORDER BY SizeMB DESC;
GO

-- =============================================================================
-- ثبت Migration در جدول تاریخچه (اختیاری)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MigrationHistory')
BEGIN
    CREATE TABLE MigrationHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        MigrationName NVARCHAR(255) NOT NULL,
        AppliedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        AppliedBy NVARCHAR(255) NOT NULL DEFAULT SYSTEM_USER,
        Description NVARCHAR(MAX) NULL
    );
END

INSERT INTO MigrationHistory (MigrationName, Description)
VALUES ('AddPerformanceIndexes_v1', 'اضافه کردن Indexes برای بهبود عملکرد query‌های پرکاربرد');
GO

PRINT N'';
PRINT N'✅ Performance Indexes با موفقیت اعمال شدند';
PRINT N'';
GO
