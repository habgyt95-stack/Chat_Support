using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat_Support.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initialcatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KCI_Groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false, comment: "کلید")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "نام گروه کاربری"),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true, comment: "شرح"),
                    ParentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KCI_Groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false, comment: "کلید")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "نام ناحیه/حوزه"),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, comment: "عنوان"),
                    ParentId = table.Column<int>(type: "int", nullable: true, comment: "کد ناحیه بالاتر - کلید به جدول Regions"),
                    RelatedURI = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KeywordsMetaTag = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DescriptionMetaTag = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StoregeLimit = table.Column<int>(type: "int", nullable: true),
                    UsersLimit = table.Column<int>(type: "int", nullable: true),
                    DatabaseLimit = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "GroupFacilities",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false, comment: "کلید")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: true, comment: "کد گروه کاربری - کلید به kci_groups"),
                    TableName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, comment: "نام جدول"),
                    FacilityId = table.Column<int>(type: "int", nullable: true, comment: "کد امکان"),
                    AccessType = table.Column<string>(type: "char(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: true, comment: "نوع دسترسی y/n"),
                    LinkId = table.Column<int>(type: "int", nullable: true, comment: "کد لینک - کلید به جدول CMS_Links"),
                    DLinkId = table.Column<int>(type: "int", nullable: true, comment: "کد لینک مربوطه - کلید به جدول CMSDirectLinks")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupFacilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_GroupFacilities_KCI_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "KCI_Groups",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "KCI_Users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    Enable = table.Column<bool>(type: "bit", nullable: true),
                    StafId = table.Column<int>(type: "int", nullable: true),
                    DateEnter = table.Column<long>(type: "bigint", nullable: true),
                    Sex = table.Column<string>(type: "char(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: true),
                    FatherName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BirthDate = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: true),
                    Degree = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Tel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImageName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RegionId = table.Column<int>(type: "int", nullable: true),
                    Post = table.Column<int>(type: "int", nullable: true),
                    ShowPublic = table.Column<bool>(type: "bit", nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CodeMeli = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    WorkPlace = table.Column<int>(type: "int", nullable: true),
                    CodePosti = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    SecurityQuestion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SecurityAnswer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OrgId = table.Column<int>(type: "int", nullable: true),
                    AccessFlag = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ActiveDirectoryUserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    EndSessionTime = table.Column<int>(type: "int", nullable: true),
                    LastPasswordChangeDate = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    LoginAttemptCount = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    HasLoggedIn = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    AgentStatus = table.Column<int>(type: "int", nullable: false),
                    CurrentActiveChats = table.Column<int>(type: "int", nullable: false),
                    MaxConcurrentChats = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KCI_Users", x => x.id);
                    table.ForeignKey(
                        name: "FK_KCI_Users_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ChatRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsGroup = table.Column<bool>(type: "bit", nullable: false),
                    Avatar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RegionId = table.Column<int>(type: "int", nullable: true),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    GuestIdentifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ChatRoomType = table.Column<int>(type: "int", nullable: false),
                    CreatedById1 = table.Column<int>(type: "int", nullable: true),
                    GuestUserId = table.Column<int>(type: "int", nullable: true),
                    RegionId1 = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRooms_GuestUsers_GuestUserId",
                        column: x => x.GuestUserId,
                        principalTable: "GuestUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatRooms_KCI_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatRooms_KCI_Users_CreatedById1",
                        column: x => x.CreatedById1,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ChatRooms_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatRooms_Regions_RegionId1",
                        column: x => x.RegionId1,
                        principalTable: "Regions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "CMS_UserRegions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, comment: "کلید")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true, comment: "شناسه کاربر"),
                    RegionId = table.Column<int>(type: "int", nullable: true, comment: "شناسه ناحیه ی اختصای به کاربر")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMS_UserRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CMS_UserRegions_KCI_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_CMS_UserRegions_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "KCI_AssignedUsers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false, comment: "کلید")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "کد کاربر - کلید به جدول kci_users"),
                    GroupId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, comment: "گروه کاربری - کلید به جدول kci_groups"),
                    UserId1 = table.Column<int>(type: "int", nullable: true),
                    GroupId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KCI_AssignedUsers", x => x.id);
                    table.ForeignKey(
                        name: "FK_KCI_AssignedUsers_KCI_Groups_GroupId1",
                        column: x => x.GroupId1,
                        principalTable: "KCI_Groups",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_KCI_AssignedUsers_KCI_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "Support_Agents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MaxConcurrentChats = table.Column<int>(type: "int", nullable: true, defaultValue: 5),
                    LastActivityAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Support_Agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Support_Agents_KCI_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ConnectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    KciUserId = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserConnections_KCI_Users_KciUserId",
                        column: x => x.KciUserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_UserConnections_KCI_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFacilities",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false, comment: "کلید")
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Regionid = table.Column<int>(type: "int", nullable: true),
                    UserID = table.Column<int>(type: "int", nullable: true, comment: "کد کاربر مربوطه - کلید به kci_users"),
                    TableName = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true, comment: "نام جدول مربوطه - کلید به databases"),
                    FacilityId = table.Column<int>(type: "int", nullable: true, comment: "ماژول مربوطه - کلید به جدول facilities"),
                    AccessType = table.Column<string>(type: "char(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: true, comment: "نوع دسترسی y/n"),
                    LinkId = table.Column<int>(type: "int", nullable: true, comment: "کد لینک مربوطه - کلید به جدول CMS_Links"),
                    DLinkId = table.Column<int>(type: "int", nullable: true, comment: "کد لینک مربوطه - کلید به جدول CMSDirectLinks")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFacilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_UserFacilities_KCI_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_UserFacilities_Regions_Regionid",
                        column: x => x.Regionid,
                        principalTable: "Regions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ChatFileMetadatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChatRoomId = table.Column<int>(type: "int", nullable: false),
                    UploadedById = table.Column<int>(type: "int", nullable: true),
                    UploadedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageType = table.Column<int>(type: "int", nullable: false),
                    KciUserId = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatFileMetadatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatFileMetadatas_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatFileMetadatas_KCI_Users_KciUserId",
                        column: x => x.KciUserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ChatFileMetadatas_KCI_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: true),
                    ChatRoomId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttachmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ReplyToMessageId = table.Column<int>(type: "int", nullable: true),
                    KciUserId = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatMessages_ReplyToMessageId",
                        column: x => x.ReplyToMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatMessages_KCI_Users_KciUserId",
                        column: x => x.KciUserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ChatMessages_KCI_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatRoomMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ChatRoomId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsMuted = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastReadMessageId = table.Column<int>(type: "int", nullable: true),
                    KciUserId = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRoomMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_KCI_Users_KciUserId",
                        column: x => x.KciUserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_KCI_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequesterUserId = table.Column<int>(type: "int", nullable: true),
                    RequesterGuestId = table.Column<int>(type: "int", nullable: true),
                    AssignedAgentUserId = table.Column<int>(type: "int", nullable: true),
                    RegionId = table.Column<int>(type: "int", nullable: true),
                    ChatRoomId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_GuestUsers_RequesterGuestId",
                        column: x => x.RequesterGuestId,
                        principalTable: "GuestUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_KCI_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_SupportTickets_Support_Agents_AssignedAgentUserId",
                        column: x => x.AssignedAgentUserId,
                        principalTable: "Support_Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    KciUserId = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageReactions_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageReactions_KCI_Users_KciUserId",
                        column: x => x.KciUserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_MessageReactions_KCI_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KciUserId = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageStatuses_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageStatuses_KCI_Users_KciUserId",
                        column: x => x.KciUserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_MessageStatuses_KCI_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketReplies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "(getdate())"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__TicketRe__3214EC077F0EA2C0", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketReplies_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK__TicketRep__UserI__0A9D95DB",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatFileMetadatas_ChatRoomId",
                table: "ChatFileMetadatas",
                column: "ChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFileMetadatas_KciUserId",
                table: "ChatFileMetadatas",
                column: "KciUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFileMetadatas_UploadedById",
                table: "ChatFileMetadatas",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFileMetadatas_UploadedDate",
                table: "ChatFileMetadatas",
                column: "UploadedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatRoomId",
                table: "ChatMessages",
                column: "ChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Created",
                table: "ChatMessages",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_KciUserId",
                table: "ChatMessages",
                column: "KciUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ReplyToMessageId",
                table: "ChatMessages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderId",
                table: "ChatMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_ChatRoomId",
                table: "ChatRoomMembers",
                column: "ChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_KciUserId",
                table: "ChatRoomMembers",
                column: "KciUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_UserId_ChatRoomId",
                table: "ChatRoomMembers",
                columns: new[] { "UserId", "ChatRoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_CreatedById",
                table: "ChatRooms",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_CreatedById1",
                table: "ChatRooms",
                column: "CreatedById1");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_GuestUserId",
                table: "ChatRooms",
                column: "GuestUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_Name",
                table: "ChatRooms",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_RegionId",
                table: "ChatRooms",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_RegionId1",
                table: "ChatRooms",
                column: "RegionId1");

            migrationBuilder.CreateIndex(
                name: "IX_CMS_UserRegions_RegionId",
                table: "CMS_UserRegions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_CMS_UserRegions_UserId",
                table: "CMS_UserRegions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFacilities",
                table: "GroupFacilities",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFacilities_1",
                table: "GroupFacilities",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFacilities_2",
                table: "GroupFacilities",
                column: "TableName");

            migrationBuilder.CreateIndex(
                name: "IX_GroupFacilities_3",
                table: "GroupFacilities",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestUsers_Email",
                table: "GuestUsers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_GuestUsers_SessionId",
                table: "GuestUsers",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KCI_AssignedUsers_GroupId1",
                table: "KCI_AssignedUsers",
                column: "GroupId1");

            migrationBuilder.CreateIndex(
                name: "IX_KCI_AssignedUsers_UserId1",
                table: "KCI_AssignedUsers",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX-GroupID",
                table: "KCI_AssignedUsers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX-ParentID-Includes",
                table: "KCI_Groups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_KCI_UserName",
                table: "KCI_Users",
                column: "UserName",
                unique: true,
                filter: "[UserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KCI_Users_RegionId",
                table: "KCI_Users",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_KciUserId",
                table: "MessageReactions",
                column: "KciUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_MessageId_UserId_Emoji",
                table: "MessageReactions",
                columns: new[] { "MessageId", "UserId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReactions_UserId",
                table: "MessageReactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageStatuses_KciUserId",
                table: "MessageStatuses",
                column: "KciUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageStatuses_MessageId_UserId",
                table: "MessageStatuses",
                columns: new[] { "MessageId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageStatuses_UserId",
                table: "MessageStatuses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Support_Agents_IsActive",
                table: "Support_Agents",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Support_Agents_UserId",
                table: "Support_Agents",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_AssignedAgentUserId",
                table: "SupportTickets",
                column: "AssignedAgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_ChatRoomId",
                table: "SupportTickets",
                column: "ChatRoomId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_RegionId",
                table: "SupportTickets",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_RequesterGuestId",
                table: "SupportTickets",
                column: "RequesterGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_RequesterUserId",
                table: "SupportTickets",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status",
                table: "SupportTickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TicketReplies_TicketId",
                table: "TicketReplies",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketReplies_UserId",
                table: "TicketReplies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_ConnectionId",
                table: "UserConnections",
                column: "ConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_KciUserId",
                table: "UserConnections",
                column: "KciUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_UserId",
                table: "UserConnections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilities",
                table: "UserFacilities",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilities_1",
                table: "UserFacilities",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilities_2",
                table: "UserFacilities",
                column: "TableName");

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilities_3",
                table: "UserFacilities",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFacilities_Regionid",
                table: "UserFacilities",
                column: "Regionid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatFileMetadatas");

            migrationBuilder.DropTable(
                name: "ChatRoomMembers");

            migrationBuilder.DropTable(
                name: "CMS_UserRegions");

            migrationBuilder.DropTable(
                name: "GroupFacilities");

            migrationBuilder.DropTable(
                name: "KCI_AssignedUsers");

            migrationBuilder.DropTable(
                name: "MessageReactions");

            migrationBuilder.DropTable(
                name: "MessageStatuses");

            migrationBuilder.DropTable(
                name: "TicketReplies");

            migrationBuilder.DropTable(
                name: "UserConnections");

            migrationBuilder.DropTable(
                name: "UserFacilities");

            migrationBuilder.DropTable(
                name: "KCI_Groups");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "ChatRooms");

            migrationBuilder.DropTable(
                name: "Support_Agents");

            migrationBuilder.DropTable(
                name: "GuestUsers");

            migrationBuilder.DropTable(
                name: "KCI_Users");

            migrationBuilder.DropTable(
                name: "Regions");
        }
    }
}
