using EWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EWallet.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for the <see cref="Wallet"/> entity.</summary>
public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallets");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        // Money value-object — owned entity stored as columns on this table
        builder.OwnsOne(w => w.Balance, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("balance_amount")
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0m)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("balance_currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD")
                .IsRequired();
        });

        builder.Property(w => w.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(w => w.IsLocked)
            .HasDefaultValue(false);

        builder.Property(w => w.UserId)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One wallet per user (initially; extend with multi-currency wallets later)
        builder.HasIndex(w => w.UserId)
            .IsUnique()
            .HasDatabaseName("ix_wallets_user_id");

        // Optimistic concurrency — EF Core manages this automatically with PostgreSQL xmin
        builder.Property(w => w.RowVersion)
            .IsRowVersion()
            .IsRequired(false);

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt).IsRequired(false);
    }
}
