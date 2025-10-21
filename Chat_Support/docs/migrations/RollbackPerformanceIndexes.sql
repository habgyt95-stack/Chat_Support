-- =============================================================================
-- Rollback Performance Indexes
-- حذف indexes اضافه شده توسط AddPerformanceIndexes.sql
-- =============================================================================

PRINT N'شروع حذف Performance Indexes...';
GO

-- =============================================================================
-- حذف indexes
-- =============================================================================

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatRoomMembers_UserId_IncludeAll' AND object_id = OBJECT_ID('ChatRoomMembers'))
BEGIN
    PRINT N'حذف index: IX_ChatRoomMembers_UserId_IncludeAll';
    DROP INDEX IX_ChatRoomMembers_UserId_IncludeAll ON ChatRoomMembers;
    PRINT N'✓ Index حذف شد';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_RoomId_CreatedAt' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    PRINT N'حذف index: IX_ChatMessages_RoomId_CreatedAt';
    DROP INDEX IX_ChatMessages_RoomId_CreatedAt ON ChatMessages;
    PRINT N'✓ Index حذف شد';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MessageStatus_UserId_IsRead' AND object_id = OBJECT_ID('MessageStatus'))
BEGIN
    PRINT N'حذف index: IX_MessageStatus_UserId_IsRead';
    DROP INDEX IX_MessageStatus_UserId_IsRead ON MessageStatus;
    PRINT N'✓ Index حذف شد';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserConnections_UserId_IsActive' AND object_id = OBJECT_ID('UserConnections'))
BEGIN
    PRINT N'حذف index: IX_UserConnections_UserId_IsActive';
    DROP INDEX IX_UserConnections_UserId_IsActive ON UserConnections;
    PRINT N'✓ Index حذف شد';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SupportAgents_IsActive_RegionId' AND object_id = OBJECT_ID('SupportAgents'))
BEGIN
    PRINT N'حذف index: IX_SupportAgents_IsActive_RegionId';
    DROP INDEX IX_SupportAgents_IsActive_RegionId ON SupportAgents;
    PRINT N'✓ Index حذف شد';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatRooms_Type_IsDeleted' AND object_id = OBJECT_ID('ChatRooms'))
BEGIN
    PRINT N'حذف index: IX_ChatRooms_Type_IsDeleted';
    DROP INDEX IX_ChatRooms_Type_IsDeleted ON ChatRooms;
    PRINT N'✓ Index حذف شد';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MessageReactions_MessageId_UserId' AND object_id = OBJECT_ID('MessageReactions'))
BEGIN
    PRINT N'حذف index: IX_MessageReactions_MessageId_UserId';
    DROP INDEX IX_MessageReactions_MessageId_UserId ON MessageReactions;
    PRINT N'✓ Index حذف شد';
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatRooms_Type_Status_CreatedAt' AND object_id = OBJECT_ID('ChatRooms'))
BEGIN
    PRINT N'حذف index: IX_ChatRooms_Type_Status_CreatedAt';
    DROP INDEX IX_ChatRooms_Type_Status_CreatedAt ON ChatRooms;
    PRINT N'✓ Index حذف شد';
END
GO

-- =============================================================================
-- ثبت Rollback در تاریخچه
-- =============================================================================
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MigrationHistory')
BEGIN
    INSERT INTO MigrationHistory (MigrationName, Description)
    VALUES ('RollbackPerformanceIndexes_v1', 'Rollback: حذف Performance Indexes');
END
GO

PRINT N'';
PRINT N'✅ Performance Indexes با موفقیت حذف شدند (Rollback انجام شد)';
PRINT N'';
GO
