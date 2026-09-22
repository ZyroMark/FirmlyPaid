using FirmlyPaid.Data.Core.Entities;
using FirmlyPaid.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace FirmlyPaid.Data.Core;

/// <summary>
/// FirmlyPaidCore: people, accounts, payments and the audit log. Every service except
/// Matching reads from here. It holds no biometric template of any kind.
/// </summary>
public class FirmlyPaidCoreDbContext(DbContextOptions<FirmlyPaidCoreDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<LinkedAccount> LinkedAccounts => Set<LinkedAccount>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<RiskEvent> RiskEvents => Set<RiskEvent>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    /// <summary>Only the SMS simulator writes here. A real provider keeps no message bodies.</summary>
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Money is rand and cents, everywhere, with no exceptions.
        builder.Properties<decimal>().HavePrecision(18, 2);

        // Strings are bounded by default so nothing silently becomes nvarchar(max).
        builder.Properties<string>().HaveMaxLength(256);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ConfigureCustomers(builder);
        ConfigureMerchants(builder);
        ConfigurePayments(builder);
        ConfigureEnrolments(builder);
        ConfigureAuditLog(builder);

        builder.Entity<SmsMessage>(entity =>
        {
            entity.HasKey(m => m.SmsMessageId);
            entity.Property(m => m.MaskedCellphoneNumber).HasMaxLength(20);
            entity.Property(m => m.Message).HasMaxLength(500);
            entity.HasIndex(m => m.SentAt);
        });

        ForceUtcDateTimes(builder);
    }

    private static void ConfigureCustomers(ModelBuilder builder)
    {
        builder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.CustomerId);
            entity.Property(c => c.FullName).HasMaxLength(200);
            entity.Property(c => c.IdNumberHash).HasMaxLength(128);
            entity.Property(c => c.CellphoneNumber).HasMaxLength(20);
            entity.Property(c => c.PinHash).HasMaxLength(512);

            // One profile per person, and a fast lookup during enrolment.
            entity.HasIndex(c => c.IdNumberHash).IsUnique();

            // Checkout searches one bucket only (FR-07), so this index carries the flow.
            entity.HasIndex(c => new { c.IdDigits7to10Bucket, c.Status });

            entity.HasIndex(c => c.TemplateOwnerId).IsUnique();
            entity.HasIndex(c => c.CellphoneNumber);
        });

        builder.Entity<Consent>(entity =>
        {
            entity.HasKey(c => c.ConsentId);
            entity.Property(c => c.ConsentTextVersion).HasMaxLength(50);

            entity.HasOne(c => c.Customer)
                .WithMany(c => c.Consents)
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.CustomerId);
        });

        builder.Entity<LinkedAccount>(entity =>
        {
            entity.HasKey(a => a.LinkedAccountId);
            entity.Property(a => a.BankCode).HasMaxLength(20);
            entity.Property(a => a.AccountNickname).HasMaxLength(60);
            entity.Property(a => a.AccountLast4).HasMaxLength(4);
            entity.Property(a => a.AccountTokenKeyVersion).HasMaxLength(50);
            entity.Property(a => a.ConfirmationReference).HasMaxLength(100);

            entity.HasOne(a => a.Customer)
                .WithMany(c => c.LinkedAccounts)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(a => new { a.CustomerId, a.Status });
            entity.HasIndex(a => a.ConfirmationReference);
        });
    }

    private static void ConfigureMerchants(ModelBuilder builder)
    {
        builder.Entity<Merchant>(entity =>
        {
            entity.HasKey(m => m.MerchantId);
            entity.Property(m => m.TradingName).HasMaxLength(200);
            entity.Property(m => m.RegistrationNumber).HasMaxLength(50);
            entity.Property(m => m.FeePercent).HasPrecision(5, 2);
            entity.Property(m => m.SettlementAccountTokenKeyVersion).HasMaxLength(50);

            entity.HasIndex(m => m.RegistrationNumber).IsUnique();
        });

        builder.Entity<Store>(entity =>
        {
            entity.HasKey(s => s.StoreId);
            entity.Property(s => s.Name).HasMaxLength(200);
            entity.Property(s => s.Address).HasMaxLength(400);
            entity.Property(s => s.Area).HasMaxLength(100);

            entity.HasOne(s => s.Merchant)
                .WithMany(m => m.Stores)
                .HasForeignKey(s => s.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Terminal>(entity =>
        {
            entity.HasKey(t => t.TerminalId);
            entity.Property(t => t.SerialNumber).HasMaxLength(50);
            entity.Property(t => t.CertificateThumbprint).HasMaxLength(100);

            entity.HasOne(t => t.Store)
                .WithMany(s => s.Terminals)
                .HasForeignKey(t => t.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(t => t.SerialNumber).IsUnique();

            // The Gateway looks a terminal up by thumbprint on every call (rule 10.9).
            entity.HasIndex(t => t.CertificateThumbprint).IsUnique();
        });

        builder.Entity<Settlement>(entity =>
        {
            entity.HasKey(s => s.SettlementId);

            entity.HasOne(s => s.Merchant)
                .WithMany()
                .HasForeignKey(s => s.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.MerchantId, s.PeriodStart, s.PeriodEnd });
        });
    }

    private static void ConfigurePayments(ModelBuilder builder)
    {
        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.PaymentId);
            entity.Property(p => p.MerchantReference).HasMaxLength(100);
            entity.Property(p => p.IdempotencyKey).HasMaxLength(100);
            entity.Property(p => p.DeclineReason).HasMaxLength(50);
            entity.Property(p => p.BankReference).HasMaxLength(100);

            // Rule 10.11: the database itself refuses a second payment for the same key.
            entity.HasIndex(p => p.IdempotencyKey).IsUnique();

            entity.HasIndex(p => new { p.MerchantId, p.CreatedAt });
            entity.HasIndex(p => new { p.StoreId, p.CreatedAt });

            // The risk engine counts a customer's recent payments on every checkout.
            entity.HasIndex(p => new { p.CustomerId, p.CreatedAt });
        });

        builder.Entity<Refund>(entity =>
        {
            entity.HasKey(r => r.RefundId);
            entity.Property(r => r.Reason).HasMaxLength(400);
            entity.Property(r => r.BankReference).HasMaxLength(100);

            entity.HasOne(r => r.Payment)
                .WithMany()
                .HasForeignKey(r => r.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.PaymentId);
        });

        builder.Entity<Dispute>(entity =>
        {
            entity.HasKey(d => d.DisputeId);
            entity.Property(d => d.Reason).HasMaxLength(1000);
            entity.Property(d => d.Resolution).HasMaxLength(1000);

            entity.HasOne(d => d.Payment)
                .WithMany()
                .HasForeignKey(d => d.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(d => new { d.CustomerId, d.Status });
        });

        builder.Entity<RiskEvent>(entity =>
        {
            entity.HasKey(r => r.RiskEventId);
            entity.Property(r => r.Rule).HasMaxLength(100);

            entity.HasIndex(r => new { r.CustomerId, r.CreatedAt });
            entity.HasIndex(r => new { r.TerminalId, r.CreatedAt });
        });
    }

    private static void ConfigureEnrolments(ModelBuilder builder)
    {
        builder.Entity<Enrolment>(entity =>
        {
            entity.HasKey(e => e.EnrolmentId);
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.IdNumberHash).HasMaxLength(128);
            entity.Property(e => e.CellphoneNumber).HasMaxLength(20);
            entity.Property(e => e.HomeAffairsReference).HasMaxLength(100);

            entity.HasIndex(e => e.IdNumberHash);
            entity.HasIndex(e => e.TemplateOwnerId);
            entity.HasIndex(e => new { e.StoreId, e.CreatedAt });
        });
    }

    private static void ConfigureAuditLog(ModelBuilder builder)
    {
        builder.Entity<AuditEntry>(entity =>
        {
            entity.HasKey(a => a.AuditId);

            // The chain is verified in Sequence order, so the database assigns it.
            entity.Property(a => a.Sequence).ValueGeneratedOnAdd();
            entity.HasIndex(a => a.Sequence).IsUnique();

            entity.Property(a => a.Actor).HasMaxLength(200);
            entity.Property(a => a.Action).HasMaxLength(100);
            entity.Property(a => a.EntityType).HasMaxLength(100);
            entity.Property(a => a.EntityId).HasMaxLength(100);
            entity.Property(a => a.Details).HasMaxLength(2000);
            entity.Property(a => a.PreviousHash).HasMaxLength(64).IsFixedLength();
            entity.Property(a => a.Hash).HasMaxLength(64).IsFixedLength();

            entity.HasIndex(a => new { a.EntityType, a.EntityId });
            entity.HasIndex(a => a.CreatedAt);
        });
    }

    /// <summary>
    /// Every stored time is UTC. SQL Server hands back a DateTime with Kind Unspecified,
    /// which silently becomes local time the first time someone formats it, so the read
    /// side is pinned here rather than trusted to each caller.
    /// </summary>
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

    /// <summary>
    /// Appends an audit row, chained to the last one. Rule 10.5: <paramref name="details"/>
    /// must never carry an ID number, PIN, template, account number or token, so the caller
    /// gets a check rather than a chance to forget.
    /// </summary>
    public async Task<AuditEntry> AppendAuditAsync(
        string actor,
        string action,
        string entityType,
        string entityId,
        string details,
        DateTime createdAtUtc,
        CancellationToken ct = default)
    {
        var violation = SensitiveDataPatterns.FindViolation(details);
        if (violation is not null)
        {
            throw new InvalidOperationException(
                "Audit details contained data that may never be written down. Summarise it instead.");
        }

        var previousHash = await AuditLog
            .OrderByDescending(a => a.Sequence)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync(ct) ?? AuditHash.GenesisHash;

        var entry = new AuditEntry
        {
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            PreviousHash = previousHash,
            CreatedAt = createdAtUtc,
            Hash = AuditHash.Compute(previousHash, actor, action, entityType, entityId, details, createdAtUtc),
        };

        AuditLog.Add(entry);
        return entry;
    }
}
