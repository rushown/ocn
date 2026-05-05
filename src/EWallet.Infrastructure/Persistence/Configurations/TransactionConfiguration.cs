using EWallet.Domain.Entities;
using EWallet.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EWallet.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Transaction"/> entity.</summary>
public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.WalletId).IsRequired();

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.SenderWalletId).IsRequired(false);
        builder.Property(t => t.ReceiverWalletId).IsRequired(false);

        // Money value-object
        builder.OwnsOne(t => t.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("amount_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(t => t.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<int>()
            .HasDefaultValue(TransactionStatus.Pending)
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_transactions_idempotency_key");

        builder.HasIndex(t => t.WalletId)
            .HasDatabaseName("ix_transactions_wallet_id");

        builder.HasIndex(t => new { t.WalletId, t.CreatedAt })
            .HasDatabaseName("ix_transactions_wallet_created");

        builder.Property(t => t.ExternalReference)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(t => t.Description)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(t => t.FailureReason)
            .HasMaxLength(1024)
            .IsRequired(false);

        builder.Property(t => t.CompletedAt).IsRequired(false);

        builder.Property(t => t.RowVersion)
            .IsRowVersion()
            .IsRequired(false);

        builder.Property(t => t.CreatedAt).IsRequired();
    }
}
