using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _8BitizChatBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LlmProvider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OllamaBaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    MaxTokens = table.Column<int>(type: "int", nullable: false),
                    ChatbotName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChatbotLogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondaryColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WelcomeMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChatbotOnline = table.Column<bool>(type: "bit", nullable: false),
                    OpenChatOnLoad = table.Column<bool>(type: "bit", nullable: false),
                    QuickRepliesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GreetingResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HowAreYouResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhoAreYouResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhatCanYouDoResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThanksResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoodbyeResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "DomainApiKeys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainAppearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ChatbotName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChatbotLogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondaryColor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WelcomeMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChatbotOnline = table.Column<bool>(type: "bit", nullable: false),
                    OpenChatOnLoad = table.Column<bool>(type: "bit", nullable: false),
                    QuickRepliesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainAppearances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsUser = table.Column<bool>(type: "bit", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DealersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TiresJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId",
                table: "ChatMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_Timestamp",
                table: "ChatMessages",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_CreatedAt",
                table: "ChatSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_Domain",
                table: "ChatSessions",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_DomainApiKeys_Domain",
                table: "DomainApiKeys",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DomainAppearances_Domain",
                table: "DomainAppearances",
                column: "Domain",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSettings");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "DomainApiKeys");

            migrationBuilder.DropTable(
                name: "DomainAppearances");

            migrationBuilder.DropTable(
                name: "ChatSessions");
        }
    }
}
