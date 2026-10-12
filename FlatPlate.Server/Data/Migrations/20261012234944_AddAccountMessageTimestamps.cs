using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlatPlate.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountMessageTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerificationSentAtUtc",
                table: "UserAccounts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordResetSentAtUtc",
                table: "UserAccounts",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerificationSentAtUtc",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "PasswordResetSentAtUtc",
                table: "UserAccounts");
        }
    }
}
