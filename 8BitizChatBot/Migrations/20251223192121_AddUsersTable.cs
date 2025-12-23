using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _8BitizChatBot.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationContexts",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentIntent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollectedParametersJson = table.Column<string>(type: "text", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Year = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Season = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BrandModelInvalidAttempts = table.Column<int>(type: "int", nullable: false),
                    AwaitingWhatsAppConsent = table.Column<bool>(type: "bit", nullable: false),
                    AwaitingWhatsAppPhone = table.Column<bool>(type: "bit", nullable: false),
                    LastDealerSummary = table.Column<string>(type: "text", nullable: true),
                    LastActivity = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationContexts", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_ConversationContexts_ChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationContexts_LastActivity",
                table: "ConversationContexts",
                column: "LastActivity");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationContexts");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
