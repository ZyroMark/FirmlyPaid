using FirmlyPaid.Data.Vault.Entities;
using Microsoft.EntityFrameworkCore;

namespace FirmlyPaid.Data.Vault;

/// <summary>
/// FirmlyPaidVault: encrypted templates and nothing else. Only FirmlyPaid.Matching.Api is
/// given this connection string (rule 10.2), which is visible in docker-compose.yml.
/// </summary>
public class FirmlyPaidVaultDbContext(DbContextOptions<FirmlyPaidVaultDbContext> options)
    : DbContext(options)
{
    public DbSet<VeinTemplate> VeinTemplates => Set<VeinTemplate>();

    public DbSet<Bucket> Buckets => Set<Bucket>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<VeinTemplate>(entity =>
        {
            entity.HasKey(t => t.TemplateId);
            entity.Property(t => t.KeyVersion).HasMaxLength(50);
            entity.Property(t => t.EncryptedTemplate).HasColumnType("varbinary(max)");

            // A match loads one owner's live templates, so the filter is part of the index.
            entity.HasIndex(t => new { t.TemplateOwnerId, t.RevokedAt });
        });

        builder.Entity<Bucket>(entity =>
        {
            entity.HasKey(b => b.TemplateOwnerId);

            // The whole point of the bucket: find candidate owners by four digits.
            entity.HasIndex(b => b.IdDigits7to10Bucket);
        });

        ForceUtcDateTimes(builder);
    }

    /// <summary>Same reason as the core context: SQL Server does not remember the kind.</summary>
    private static void ForceUtcDateTimes(ModelBuilder builder)
    {
        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        var nullableUtcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            value => value.HasValue ? (value.Value.Kind == DateTimeKind.Utc ? value : value.Value.ToUniversalTime()) : null,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }
    }
}
