using System.Text.RegularExpressions;
using FirmlyPaid.Data.Vault.Entities;
using FirmlyPaid.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FirmlyPaid.Data.Tests;

/// <summary>
/// Rule 10.2: templates live only in FirmlyPaidVault, and only the Matching service may
/// read that database. These tests check the structure rather than trusting a convention,
/// because the rule is easy to break by adding one project reference.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class VaultIsolationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task VaultHasOnlyTemplatesAndBuckets()
    {
        await using var database = await fixture.CreateVaultAsync();

        var tables = await database.Database
            .SqlQuery<string>($"""
                SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME <> '__EFMigrationsHistory'
                """)
            .ToListAsync();

        tables.Should().BeEquivalentTo(["VeinTemplates", "Buckets"]);
    }

    [Fact]
    public async Task NoVaultColumnCanIdentifyAPerson()
    {
        await using var database = await fixture.CreateVaultAsync();

        var columns = await database.Database
            .SqlQuery<string>($"SELECT COLUMN_NAME AS Value FROM INFORMATION_SCHEMA.COLUMNS")
            .ToListAsync();

        string[] forbidden = ["Name", "IdNumber", "IdNumberHash", "Cellphone", "CustomerId", "Email"];

        foreach (var column in columns)
        {
            forbidden.Should().NotContain(
                candidate => column.Contains(candidate, StringComparison.OrdinalIgnoreCase),
                $"'{column}' would tie a template to a person");
        }
    }

    [Fact]
    public async Task ATemplateRoundTripsAsCiphertext()
    {
        await using var database = await fixture.CreateVaultAsync();

        var ownerId = Guid.NewGuid();
        var ciphertext = new byte[] { 0x01, 0x02, 0x03, 0xFF };

        database.VeinTemplates.Add(new VeinTemplate
        {
            TemplateOwnerId = ownerId,
            FingerPosition = FingerPosition.RightIndex,
            EncryptedTemplate = ciphertext,
            KeyVersion = "dev-1",
            TransformSeedId = Guid.NewGuid(),
            QualityScore = 82,
            CreatedAt = DateTime.UtcNow,
        });
        database.Buckets.Add(new Bucket { TemplateOwnerId = ownerId, IdDigits7to10Bucket = 5009 });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var stored = await database.VeinTemplates.SingleAsync(t => t.TemplateOwnerId == ownerId);

        stored.EncryptedTemplate.Should().Equal(ciphertext);
        stored.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task BucketLookupFindsOnlyOwnersInThatBucket()
    {
        await using var database = await fixture.CreateVaultAsync();

        var wanted = Guid.NewGuid();
        database.Buckets.AddRange(
            new Bucket { TemplateOwnerId = wanted, IdDigits7to10Bucket = 5009 },
            new Bucket { TemplateOwnerId = Guid.NewGuid(), IdDigits7to10Bucket = 1234 },
            new Bucket { TemplateOwnerId = Guid.NewGuid(), IdDigits7to10Bucket = 9999 });
        await database.SaveChangesAsync();

        var owners = await database.Buckets
            .Where(b => b.IdDigits7to10Bucket == 5009)
            .Select(b => b.TemplateOwnerId)
            .ToListAsync();

        owners.Should().ContainSingle().Which.Should().Be(wanted);
    }

    [Fact]
    public void OnlyTheMatchingServiceReferencesTheVaultProject()
    {
        var sourceRoot = FindSourceRoot();

        var offenders = new List<string>();

        foreach (var projectFile in Directory.GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);

            // The database tool and the vault project itself are allowed to know about it.
            if (projectName is "FirmlyPaid.Data.Vault" or "FirmlyPaid.DbTool" or "FirmlyPaid.Matching.Api")
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(projectFile), @"FirmlyPaid\.Data\.Vault\.csproj"))
            {
                offenders.Add(projectName);
            }
        }

        offenders.Should().BeEmpty(
            "rule 10.2 allows only the Matching service to reach the vault, so no other service may reference it");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must run from inside the repository");
        return Path.Combine(directory!.FullName, "src");
    }
}
