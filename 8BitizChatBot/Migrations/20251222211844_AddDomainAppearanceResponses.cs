using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _8BitizChatBot.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainAppearanceResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoodbyeResponse",
                table: "DomainAppearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GreetingResponse",
                table: "DomainAppearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HowAreYouResponse",
                table: "DomainAppearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThanksResponse",
                table: "DomainAppearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatCanYouDoResponse",
                table: "DomainAppearances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhoAreYouResponse",
                table: "DomainAppearances",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoodbyeResponse",
                table: "DomainAppearances");

            migrationBuilder.DropColumn(
                name: "GreetingResponse",
                table: "DomainAppearances");

            migrationBuilder.DropColumn(
                name: "HowAreYouResponse",
                table: "DomainAppearances");

            migrationBuilder.DropColumn(
                name: "ThanksResponse",
                table: "DomainAppearances");

            migrationBuilder.DropColumn(
                name: "WhatCanYouDoResponse",
                table: "DomainAppearances");

            migrationBuilder.DropColumn(
                name: "WhoAreYouResponse",
                table: "DomainAppearances");
        }
    }
}
