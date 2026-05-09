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

public class DepositCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IPaymentGateway> _gateway;
    private readonly Mock<IWalletNotificationService> _notifications;
    private readonly Mock<AutoMapper.IMapper> _mapper;
    private readonly DepositCommandHandler _handler;

    public DepositCommandHandlerTests()
    {
        (_uow, _, _, _, _) = MockUnitOfWorkFactory.Create();
        _gateway     = new Mock<IPaymentGateway>();
        _notifications = new Mock<IWalletNotificationService>();
        _mapper = new Mock<AutoMapper.IMapper>();

        var logger = new Mock<ILogger<DepositCommandHandler>>();

        _handler = new DepositCommandHandler(
            _uow.Object,
            _gateway.Object,
            _notifications.Object,
            _mapper.Object,
            logger.Object);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GatewaySuccess_CreditsWalletAndReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = WalletTestData.CreateWalletForUser(userId, balance: 0m);

        _uow.Setup(u => u.Wallets.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        _gateway.Setup(g => g.ProcessDepositAsync(
                wallet.Id,
                It.IsAny<Money>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(true, "ref123", null));
        
        _mapper.Setup(m => m.Map<TransactionDto>(It.IsAny<EWallet.Domain.Entities.Transaction>()))
            .Returns((EWallet.Domain.Entities.Transaction tx) => new TransactionDto(
                tx.Id, tx.WalletId, tx.Amount.Amount, tx.Amount.Currency, tx.Type, tx.Status,
                tx.Description, tx.IdempotencyKey, tx.CreatedAt, tx.CompletedAt));

        // Act
        var result = await _handler.Handle(
            new DepositCommand(userId, 100m, "USD", "ext1", "idem1"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        wallet.Balance.Amount.Should().Be(100m);
        result.Value!.Status.Should().Be(TransactionStatus.Completed);
    }

    // ─── Gateway failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GatewayFailure_DoesNotCreditWalletAndMarksTransactionFailed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wallet = WalletTestData.CreateWalletForUser(userId, balance: 0m);

        _uow.Setup(u => u.Wallets.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        _gateway.Setup(g => g.ProcessDepositAsync(
                wallet.Id,
                It.IsAny<Money>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentResult(false, "ref123", "Card declined"));
        
        _mapper.Setup(m => m.Map<TransactionDto>(It.IsAny<EWallet.Domain.Entities.Transaction>()))
            .Returns((EWallet.Domain.Entities.Transaction tx) => new TransactionDto(
                tx.Id, tx.WalletId, tx.Amount.Amount, tx.Amount.Currency, tx.Type, tx.Status,
                tx.Description, tx.IdempotencyKey, tx.CreatedAt, tx.CompletedAt));

        // Act
        var result = await _handler.Handle(
            new DepositCommand(userId, 100m, "USD", "ext1", "idem1"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        wallet.Balance.Amount.Should().Be(0m);
    }
}
