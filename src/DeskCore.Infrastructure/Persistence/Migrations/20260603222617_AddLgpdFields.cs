using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeskCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLgpdFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnonymizedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrivacyConsentAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyPolicyVersion",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnonymizedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PrivacyConsentAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PrivacyPolicyVersion",
                table: "AspNetUsers");
        }
    }
}
