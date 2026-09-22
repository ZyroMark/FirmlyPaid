using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmlyPaid.Data.Vault.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Buckets",
                columns: table => new
                {
                    TemplateOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdDigits7to10Bucket = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buckets", x => x.TemplateOwnerId);
                });

            migrationBuilder.CreateTable(
                name: "VeinTemplates",
                columns: table => new
                {
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateOwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FingerPosition = table.Column<int>(type: "int", nullable: false),
                    EncryptedTemplate = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    KeyVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TransformSeedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualityScore = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeinTemplates", x => x.TemplateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Buckets_IdDigits7to10Bucket",
                table: "Buckets",
                column: "IdDigits7to10Bucket");

            migrationBuilder.CreateIndex(
                name: "IX_VeinTemplates_TemplateOwnerId_RevokedAt",
                table: "VeinTemplates",
                columns: new[] { "TemplateOwnerId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Buckets");

            migrationBuilder.DropTable(
                name: "VeinTemplates");
        }
    }
}
