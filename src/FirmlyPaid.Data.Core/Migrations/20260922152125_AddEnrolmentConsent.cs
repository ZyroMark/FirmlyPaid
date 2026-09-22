using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmlyPaid.Data.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrolmentConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentAcceptedAt",
                table: "Enrolments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ConsentTextVersion",
                table: "Enrolments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentAcceptedAt",
                table: "Enrolments");

            migrationBuilder.DropColumn(
                name: "ConsentTextVersion",
                table: "Enrolments");
        }
    }
}
