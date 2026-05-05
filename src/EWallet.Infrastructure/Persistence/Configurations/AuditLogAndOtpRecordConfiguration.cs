using EWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EWallet.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="AuditLog"/>.
/// Audit logs are insert-only; no FK constraints are used so that logs
/// remain intact even if the referenced entity is deleted.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // EntityId stored as a raw Guid — no FK constraint so logs survive entity deletion
        builder.Property(a => a.EntityId).IsRequired();

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(128);

        // JSON blobs stored as TEXT
        builder.Property(a => a.OldValues)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(a => a.NewValues)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(a => a.PerformedByUserId).IsRequired(false);

        builder.Property(a => a.IpAddress)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(a => a.Timestamp)
            .IsRequired()
            .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");

        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("ix_audit_logs_timestamp");

        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("ix_audit_logs_entity");
    }
}

/// <summary>EF Core mapping for the <see cref="OtpRecord"/> entity.</summary>
public sealed class OtpRecordConfiguration : IEntityTypeConfiguration<OtpRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OtpRecord> builder)
    {
        builder.ToTable("otp_records");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.UserId).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(o => o.Code)
            .IsRequired()
            .HasMaxLength(6);

        builder.Property(o => o.Purpose)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.ExpiresAt).IsRequired();

        builder.HasIndex(o => o.ExpiresAt)
            .HasDatabaseName("ix_otp_records_expires_at");

        builder.HasIndex(o => new { o.UserId, o.Purpose, o.IsUsed })
            .HasDatabaseName("ix_otp_records_user_purpose_used");

        builder.Property(o => o.IsUsed)
            .HasDefaultValue(false);

        builder.Property(o => o.CreatedAt).IsRequired();
    }
}
