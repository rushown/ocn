using AutoMapper;
using EWallet.Application.Common;
using EWallet.Application.DTOs;
using EWallet.Application.Interfaces;
using EWallet.Application.Queries;
using EWallet.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class GetWalletBalanceQueryHandler : IRequestHandler<GetWalletBalanceQuery, Result<BalanceDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GetWalletBalanceQueryHandler> _logger;

    public GetWalletBalanceQueryHandler(IUnitOfWork uow, ILogger<GetWalletBalanceQueryHandler> logger)
    {
        _uow = uow; _logger = logger;
    }

    public async Task<Result<BalanceDto>> Handle(GetWalletBalanceQuery request, CancellationToken ct)
    {
        try
        {
            var wallet = await _uow.Wallets.GetByUserIdAsync(request.UserId, ct);
            if (wallet is null)
                return Result<BalanceDto>.Failure("Wallet not found.", ErrorCodes.WalletNotFound);

            var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
            var dailySpent = await _uow.Transactions.GetDailyDebitSumAsync(wallet.Id, DateTime.UtcNow.Date, ct);

            // Determine available limit from KYC level (simplified)
            decimal dailyLimit = 500m; // default
            if (user is not null)
            {
                dailyLimit = user.KycLevel switch
                {
                    EWallet.Domain.Enums.KycLevel.Tier1    => 5_000m,
                    EWallet.Domain.Enums.KycLevel.Tier2    => 50_000m,
                    EWallet.Domain.Enums.KycLevel.Tier3    => 100_000m,
                    _                                      => 500m,
                };
            }

            var dto = new BalanceDto(
                WalletId: wallet.Id,
                Balance: wallet.Balance.Amount,
                Currency: wallet.Balance.Currency,
                AvailableLimit: Math.Max(0, dailyLimit - dailySpent));

            return Result<BalanceDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching balance for user {UserId}", request.UserId);
            return Result<BalanceDto>.Failure("Failed to retrieve wallet balance.");
        }
    }
}

public class GetTransactionHistoryQueryHandler : IRequestHandler<GetTransactionHistoryQuery, Result<PagedResult<TransactionDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<GetTransactionHistoryQueryHandler> _logger;

    public GetTransactionHistoryQueryHandler(IUnitOfWork uow, IMapper mapper, ILogger<GetTransactionHistoryQueryHandler> logger)
    {
        _uow = uow; _mapper = mapper; _logger = logger;
    }

    public async Task<Result<PagedResult<TransactionDto>>> Handle(GetTransactionHistoryQuery request, CancellationToken ct)
    {
        try
        {
            var wallet = await _uow.Wallets.GetByUserIdAsync(request.UserId, ct);
            if (wallet is null)
                return Result<PagedResult<TransactionDto>>.Failure("Wallet not found.", ErrorCodes.WalletNotFound);

            var pageSize = Math.Min(request.PageSize, 100);
            var (transactions, total) = await _uow.Transactions.GetPagedByWalletIdAsync(wallet.Id, request.Page, pageSize, ct);

            var dtos = _mapper.Map<List<TransactionDto>>(transactions);

            var paged = new PagedResult<TransactionDto>
            {
                Items = dtos,
                TotalCount = total,
                Page = request.Page,
                PageSize = pageSize,
            };

            return Result<PagedResult<TransactionDto>>.Success(paged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transactions for user {UserId}", request.UserId);
            return Result<PagedResult<TransactionDto>>.Failure("Failed to retrieve transaction history.");
        }
    }
}

public class GetTransactionByIdQueryHandler : IRequestHandler<GetTransactionByIdQuery, Result<TransactionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<GetTransactionByIdQueryHandler> _logger;

    public GetTransactionByIdQueryHandler(IUnitOfWork uow, IMapper mapper, ILogger<GetTransactionByIdQueryHandler> logger)
    {
        _uow = uow; _mapper = mapper; _logger = logger;
    }

    public async Task<Result<TransactionDto>> Handle(GetTransactionByIdQuery request, CancellationToken ct)
    {
        try
        {
            var transaction = await _uow.Transactions.GetByIdAsync(request.TransactionId, ct);
            if (transaction is null)
                return Result<TransactionDto>.Failure("Transaction not found.");

            // Ownership check: verify the transaction belongs to a wallet owned by this user
            var wallet = await _uow.Wallets.GetByIdAsync(transaction.WalletId, ct);
            if (wallet is null || wallet.UserId != request.UserId)
                return Result<TransactionDto>.Failure("Access denied.", ErrorCodes.UnauthorizedWalletAccess);

            return Result<TransactionDto>.Success(_mapper.Map<TransactionDto>(transaction));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transaction {TransactionId}", request.TransactionId);
            return Result<TransactionDto>.Failure("Failed to retrieve transaction.");
        }
    }
}

public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<GetUserProfileQueryHandler> _logger;

    public GetUserProfileQueryHandler(IUnitOfWork uow, IMapper mapper, ILogger<GetUserProfileQueryHandler> logger)
    {
        _uow = uow; _mapper = mapper; _logger = logger;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken ct)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
            if (user is null)
                return Result<UserProfileDto>.Failure("User not found.", ErrorCodes.UserNotFound);

            return Result<UserProfileDto>.Success(_mapper.Map<UserProfileDto>(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching profile for user {UserId}", request.UserId);
            return Result<UserProfileDto>.Failure("Failed to retrieve user profile.");
        }
    }
}
