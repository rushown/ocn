using System.Linq.Expressions;
using EWallet.Domain.Entities;
using EWallet.Domain.Enums;

namespace EWallet.Domain.Specifications;

/// <summary>
/// Generic specification base providing criteria, eager-loading, ordering,
/// and pagination parameters for repository queries.
/// </summary>
/// <typeparam name="T">The entity type this specification targets.</typeparam>
public abstract class BaseSpecification<T>
{
    /// <summary>The filter predicate applied to the query, or <c>null</c> to return all rows.</summary>
    public Expression<Func<T, bool>>? Criteria { get; protected set; }

    /// <summary>Navigation properties to eagerly load.</summary>
    public List<Expression<Func<T, object>>> Includes { get; } = new();

    /// <summary>Property by which results are ordered ascending, or <c>null</c> for default ordering.</summary>
    public Expression<Func<T, object>>? OrderBy { get; protected set; }

    /// <summary>Property by which results are ordered descending, or <c>null</c>.</summary>
    public Expression<Func<T, object>>? OrderByDescending { get; protected set; }

    /// <summary>Maximum number of results to return. Only applied when <see cref="IsPagingEnabled"/> is <c>true</c>.</summary>
    public int Take { get; protected set; }

    /// <summary>Number of results to skip. Only applied when <see cref="IsPagingEnabled"/> is <c>true</c>.</summary>
    public int Skip { get; protected set; }

    /// <summary>Whether <see cref="Take"/> and <see cref="Skip"/> should be applied by the repository.</summary>
    public bool IsPagingEnabled { get; protected set; }

    /// <summary>Applies pagination to this specification.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="size">Page size (number of items per page).</param>
    protected void ApplyPaging(int page, int size)
    {
        Skip = (page - 1) * size;
        Take = size;
        IsPagingEnabled = true;
    }
}

/// <summary>
/// Filters <see cref="Transaction"/> records by wallet ID, ordered by <c>CreatedAt</c> descending,
/// with optional pagination.
/// </summary>
public sealed class TransactionByWalletSpec : BaseSpecification<Transaction>
{
    /// <summary>
    /// Creates a specification for all transactions belonging to <paramref name="walletId"/>,
    /// newest first, with optional pagination.
    /// </summary>
    /// <param name="walletId">The wallet whose transactions are requested.</param>
    /// <param name="page">1-based page number (0 disables paging).</param>
    /// <param name="size">Page size. Ignored when <paramref name="page"/> is 0.</param>
    public TransactionByWalletSpec(Guid walletId, int page = 0, int size = 20)
    {
        Criteria = tx => tx.WalletId == walletId;
        OrderByDescending = tx => tx.CreatedAt;

        if (page > 0)
            ApplyPaging(page, size);
    }

    /// <summary>
    /// Creates a specification for transactions belonging to <paramref name="walletId"/>
    /// filtered by <paramref name="status"/>, newest first, with pagination.
    /// </summary>
    public TransactionByWalletSpec(Guid walletId, TransactionStatus status, int page = 1, int size = 20)
    {
        Criteria = tx => tx.WalletId == walletId && tx.Status == status;
        OrderByDescending = tx => tx.CreatedAt;
        ApplyPaging(page, size);
    }
}
