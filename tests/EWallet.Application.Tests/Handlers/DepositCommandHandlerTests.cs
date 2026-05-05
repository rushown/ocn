using FluentAssertions;
using Moq;
using EWallet.Application.Commands.Deposit;
using EWallet.Application.Interfaces;
using EWallet.Application.Tests.Helpers;
using EWallet.Domain.Enums;
using EWallet.Domain.Interfaces;

namespace EWallet.Application.Tests.Handlers;

public class DepositCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IWalletRepository> _wallets;
    private readonly Mock<ITransactionRepository> _transactions;
    private readonly Mock<IPaymentGateway> _gateway;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly DepositCommandHandler _handler;

    public DepositCommandHandlerTests()
    {
        (_uow, _wallets, _transactions, _) = MockUnitOfWorkFactory.Create();

        _gateway     = new Mock<IPaymentGateway>();
        _currentUser = new Mock<ICurrentUserService>();

        _handler = new DepositCommandHandler(
            _uow.Object,
            _gateway.Object,
            _currentUser.Object);
    }

    private DepositCommand BuildCommand(decimal amount, string? gatewayToken = "tok_valid") =>
        new()
        {
            Amount       = amount,
            GatewayToken = gatewayToken!,
        };

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GatewaySuccess_CreditsWalletAndReturnsSuccess()
    {
        // Arrange
        var (user, wallet) = WalletTestData.CreateUserWithWallet(balance: 0m);

        _currentUser.Setup(c => c.UserId).Returns(user.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(wallet);

        _gateway.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GatewayResult.Succeeded());

        var command = BuildCommand(amount: 100m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _wallets.Verify(w => w.CreditAsync(wallet.Id, 100m, It.IsAny<CancellationToken>()), Times.Once,
            because: "successful gateway charge must credit the wallet");

        // The resulting transaction record should be Completed
        _transactions.Verify(t => t.AddAsync(
            It.Is<Domain.Entities.Transaction>(tx => tx.Status == TransactionStatus.Completed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Gateway failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_GatewayFailure_DoesNotCreditWalletAndMarksTransactionFailed()
    {
        // Arrange
        var (user, wallet) = WalletTestData.CreateUserWithWallet(balance: 0m);

        _currentUser.Setup(c => c.UserId).Returns(user.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(wallet);

        _gateway.Setup(g => g.ChargeAsync(It.IsAny<GatewayChargeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GatewayResult.Failed("Card declined"));

        var command = BuildCommand(amount: 100m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();

        _wallets.Verify(w => w.CreditAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never,
            because: "a failed gateway charge must never credit the wallet");

        _transactions.Verify(t => t.AddAsync(
            It.Is<Domain.Entities.Transaction>(tx => tx.Status == TransactionStatus.Failed),
            It.IsAny<CancellationToken>()), Times.Once,
            because: "even failed deposits must be recorded for audit purposes");
    }
}
