using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat_Support.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentStatusManagementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserRefreshTokens_KCI_Users_UserId",
                table: "UserRefreshTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRefreshTokens",
                table: "UserRefreshTokens");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "KCI_Users");

            migrationBuilder.RenameTable(
                name: "UserRefreshTokens",
                newName: "ChatUserRefreshTokens");

            migrationBuilder.RenameIndex(
                name: "IX_UserRefreshTokens_UserId",
                table: "ChatUserRefreshTokens",
                newName: "IX_ChatUserRefreshTokens_UserId");

            migrationBuilder.AddColumn<int>(
                name: "AutoDetectedStatus",
                table: "Support_Agents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManualStatusExpiry",
                table: "Support_Agents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManualStatusSetAt",
                table: "Support_Agents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChatUserRefreshTokens",
                table: "ChatUserRefreshTokens",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AbrikChatUsersTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbrikChatUsersTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbrikChatUsersTokens_KCI_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportGuestChatRoomMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestUserId = table.Column<int>(type: "int", nullable: false),
                    ChatRoomId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsMuted = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastReadMessageId = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportGuestChatRoomMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportGuestChatRoomMembers_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupportGuestChatRoomMembers_GuestUsers_GuestUserId",
                        column: x => x.GuestUserId,
                        principalTable: "GuestUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFcmTokenInfoMobileAbrikChat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    FcmToken = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddedDate = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFcmTokenInfoMobileAbrikChat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFcmTokenInfoMobileAbrikChat_KCI_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "KCI_Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbrikChatUsersTokens_UserId_DeviceId",
                table: "AbrikChatUsersTokens",
                columns: new[] { "UserId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportGuestChatRoomMembers_ChatRoomId",
                table: "SupportGuestChatRoomMembers",
                column: "ChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportGuestChatRoomMembers_GuestUserId_ChatRoomId",
                table: "SupportGuestChatRoomMembers",
                columns: new[] { "GuestUserId", "ChatRoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFcmTokenInfoMobileAbrikChat_UserId_DeviceId",
                table: "UserFcmTokenInfoMobileAbrikChat",
                columns: new[] { "UserId", "DeviceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatUserRefreshTokens_KCI_Users_UserId",
                table: "ChatUserRefreshTokens",
                column: "UserId",
                principalTable: "KCI_Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatUserRefreshTokens_KCI_Users_UserId",
                table: "ChatUserRefreshTokens");

            migrationBuilder.DropTable(
                name: "AbrikChatUsersTokens");

            migrationBuilder.DropTable(
                name: "SupportGuestChatRoomMembers");

            migrationBuilder.DropTable(
                name: "UserFcmTokenInfoMobileAbrikChat");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChatUserRefreshTokens",
                table: "ChatUserRefreshTokens");

            migrationBuilder.DropColumn(
                name: "AutoDetectedStatus",
                table: "Support_Agents");

            migrationBuilder.DropColumn(
                name: "ManualStatusExpiry",
                table: "Support_Agents");

            migrationBuilder.DropColumn(
                name: "ManualStatusSetAt",
                table: "Support_Agents");

            migrationBuilder.RenameTable(
                name: "ChatUserRefreshTokens",
                newName: "UserRefreshTokens");

            migrationBuilder.RenameIndex(
                name: "IX_ChatUserRefreshTokens_UserId",
                table: "UserRefreshTokens",
                newName: "IX_UserRefreshTokens_UserId");

            migrationBuilder.AddColumn<long>(
                name: "BirthDate",
                table: "KCI_Users",
                type: "bigint",
                maxLength: 22,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRefreshTokens",
                table: "UserRefreshTokens",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRefreshTokens_KCI_Users_UserId",
                table: "UserRefreshTokens",
                column: "UserId",
                principalTable: "KCI_Users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
