using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using EWallet.Application.Commands;
using EWallet.Application.DTOs;
using EWallet.Application.Handlers;
using EWallet.Application.Interfaces;
using EWallet.Application.Tests.Helpers;
using EWallet.Domain.Enums;
using EWallet.Domain.ValueObjects;

namespace EWallet.Application.Tests.Handlers;

public class WithdrawCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IPaymentGateway> _gateway;
    private readonly Mock<IWalletNotificationService> _notifications;
    private readonly Mock<AutoMapper.IMapper> _mapper;
    private readonly WithdrawCommandHandler _handler;

    public WithdrawCommandHandlerTests()
    {
        (_uow, _, _, _, _) = MockUnitOfWorkFactory.Create();
        _gateway = new Mock<IPaymentGateway>();
        _notifications = new Mock<IWalletNotificationService>();
        _mapper = new Mock<AutoMapper.IMapper>();
        var logger = new Mock<ILogger<WithdrawCommandHandler>>();

        _handler = new WithdrawCommandHandler(
            _uow.Object,
            _gateway.Object,
            _notifications.Object,
            _mapper.Object,
            logger.Object);
    }

    [Fact]
    public async Task Handle_InsufficientFunds_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var wallet = WalletTestData.CreateWalletForUser(userId, balance: 10m);
        _uow.Setup(u => u.Wallets.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        var result = await _handler.Handle(
            new WithdrawCommand(userId, 100m, "USD", "ext", "idem"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Insufficient funds");
    }

    [Fact]
    public async Task Handle_GatewaySuccess_DebitsWalletAndReturnsSuccess()
    {
        var userId = Guid.NewGuid();
        var wallet = WalletTestData.CreateWalletForUser(userId, balance: 100m);

        _uow.Setup(u => u.Wallets.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        _gateway.Setup(g => g.ProcessWithdrawalAsync(
                wallet.Id,
                It.IsAny<Money>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "ref-ok", null));
        _mapper.Setup(m => m.Map<TransactionDto>(It.IsAny<EWallet.Domain.Entities.Transaction>()))
            .Returns((EWallet.Domain.Entities.Transaction tx) => new TransactionDto(
                tx.Id, tx.WalletId, tx.Amount.Amount, tx.Amount.Currency, tx.Type, tx.Status,
                tx.Description, tx.IdempotencyKey, tx.CreatedAt, tx.CompletedAt));

        var result = await _handler.Handle(
            new WithdrawCommand(userId, 25m, "USD", "ext", "idem"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        wallet.Balance.Amount.Should().Be(75m);
        result.Value!.Status.Should().Be(TransactionStatus.Completed);
    }
}
