using EWallet.Application.Common;
using EWallet.Application.DTOs;
using MediatR;

namespace EWallet.Application.Queries;

public record GetWalletBalanceQuery(
    Guid UserId) : IRequest<Result<BalanceDto>>;

public record GetTransactionHistoryQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<TransactionDto>>>;

public record GetTransactionByIdQuery(
    Guid TransactionId,
    Guid UserId) : IRequest<Result<TransactionDto>>;

public record GetWalletLookupQuery(
    Guid WalletId) : IRequest<Result<WalletLookupDto>>;

public record GetUserProfileQuery(
    Guid UserId) : IRequest<Result<UserProfileDto>>;
