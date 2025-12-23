using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _8BitizChatBot.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSecurityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add columns to Users table (only if they don't exist)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'FailedLoginAttempts')
                BEGIN
                    ALTER TABLE [Users] ADD [FailedLoginAttempts] INT NOT NULL DEFAULT 0;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'LockedUntil')
                BEGIN
                    ALTER TABLE [Users] ADD [LockedUntil] DATETIME2 NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = 'PasswordChangedAt')
                BEGIN
                    ALTER TABLE [Users] ADD [PasswordChangedAt] DATETIME2 NULL;
                END
            ");

            // Create AuditLogs table (only if it doesn't exist)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
                BEGIN
                    CREATE TABLE [AuditLogs] (
                        [Id] BIGINT NOT NULL IDENTITY(1,1),
                        [UserId] NVARCHAR(100) NULL,
                        [Username] NVARCHAR(100) NULL,
                        [Action] NVARCHAR(50) NOT NULL,
                        [EntityType] NVARCHAR(255) NULL,
                        [EntityId] NVARCHAR(100) NULL,
                        [Details] TEXT NULL,
                        [IpAddress] NVARCHAR(45) NULL,
                        [UserAgent] NVARCHAR(500) NULL,
                        [Timestamp] DATETIME2 NOT NULL,
                        [Success] BIT NOT NULL,
                        [ErrorMessage] NVARCHAR(500) NULL,
                        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
                    );

                    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
                    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
                    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "Users");
        }
    }
}
