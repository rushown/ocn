using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using EWallet.Application.Commands.Transfer;
using EWallet.Application.Interfaces;
using EWallet.Application.Tests.Helpers;
using EWallet.Domain.Interfaces;

namespace EWallet.Application.Tests.Handlers;

public class TransferCommandHandlerTests
{
    // ─── Infrastructure ──────────────────────────────────────────────────────

    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IWalletRepository> _wallets;
    private readonly Mock<ITransactionRepository> _transactions;
    private readonly Mock<IUserRepository> _users;
    private readonly Mock<ICacheService> _cache;
    private readonly Mock<IWalletNotificationService> _notifications;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly TransferCommandHandler _handler;

    public TransferCommandHandlerTests()
    {
        (_uow, _wallets, _transactions, _users) = MockUnitOfWorkFactory.Create();

        _cache         = new Mock<ICacheService>();
        _notifications = new Mock<IWalletNotificationService>();
        _currentUser   = new Mock<ICurrentUserService>();

        // Default: no cached idempotency result
        _cache.Setup(c => c.GetAsync<TransferResult>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((TransferResult?)null);

        _handler = new TransferCommandHandler(
            _uow.Object,
            _cache.Object,
            _notifications.Object,
            _currentUser.Object);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static TransferCommand BuildCommand(
        decimal amount,
        Guid? receiverWalletId = null,
        string? idempotencyKey = "unique-key-abc",
        string? otpCode = null) =>
        new()
        {
            Amount           = amount,
            ReceiverWalletId = receiverWalletId ?? Guid.NewGuid(),
            IdempotencyKey   = idempotencyKey!,
            OtpCode          = otpCode,
        };

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidTransfer_ReturnsSuccess()
    {
        // Arrange
        var (sender, senderWallet) = WalletTestData.CreateUserWithWallet(balance: 200m);
        var receiverWallet         = WalletTestData.CreateSolventWallet();

        _currentUser.Setup(c => c.UserId).Returns(sender.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(sender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(senderWallet);
        _wallets.Setup(w => w.GetByIdAsync(receiverWallet.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(receiverWallet);
        _users.Setup(u => u.GetByIdAsync(sender.Id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(sender);
        _transactions.Setup(t => t.GetDailySpentAmountAsync(sender.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(0m);

        var command = BuildCommand(amount: 100m, receiverWalletId: receiverWallet.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _wallets.Verify(w => w.DebitAsync(
            senderWallet.Id, 100m, It.IsAny<CancellationToken>()), Times.Once);

        _wallets.Verify(w => w.CreditAsync(
            receiverWallet.Id, 100m, It.IsAny<CancellationToken>()), Times.Once);

        _uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);

        _notifications.Verify(n => n.NotifyBalanceUpdatedAsync(
            sender.Id, It.IsAny<CancellationToken>()), Times.Once);

        _notifications.Verify(n => n.NotifyBalanceUpdatedAsync(
            receiverWallet.OwnerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Idempotency ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateIdempotencyKey_ReturnsCachedResultWithoutBeginningTransaction()
    {
        // Arrange – cache already holds a successful response for "key1"
        var cachedResult = TransferResult.Success(Guid.NewGuid());
        _cache.Setup(c => c.GetAsync<TransferResult>("key1", It.IsAny<CancellationToken>()))
              .ReturnsAsync(cachedResult);

        var command = BuildCommand(amount: 100m, idempotencyKey: "key1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never,
            because: "idempotent replay must not start a new DB transaction");
    }

    // ─── Insufficient funds ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_InsufficientFunds_ReturnsFailureWithCorrectErrorCode()
    {
        // Arrange
        var (sender, senderWallet) = WalletTestData.CreateUserWithWallet(balance: 10m);

        _currentUser.Setup(c => c.UserId).Returns(sender.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(sender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(senderWallet);
        _wallets.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WalletTestData.CreateSolventWallet());
        _users.Setup(u => u.GetByIdAsync(sender.Id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(sender);
        _transactions.Setup(t => t.GetDailySpentAmountAsync(sender.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(0m);

        var command = BuildCommand(amount: 100m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INSUFFICIENT_FUNDS");

        _uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Daily limit ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DailyLimitExceeded_ReturnsCorrectErrorCode()
    {
        // Arrange – Tier1 limit = $1 000; already spent $900 today; trying $200
        var (sender, senderWallet) = WalletTestData.CreateUserWithWallet(balance: 5_000m);

        _currentUser.Setup(c => c.UserId).Returns(sender.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(sender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(senderWallet);
        _wallets.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WalletTestData.CreateSolventWallet());
        _users.Setup(u => u.GetByIdAsync(sender.Id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(sender);
        _transactions.Setup(t => t.GetDailySpentAmountAsync(sender.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(900m); // already near limit

        var command = BuildCommand(amount: 200m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DAILY_LIMIT_EXCEEDED");
    }

    // ─── OTP – missing ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OtpRequiredButMissing_ReturnsInvalidOtp()
    {
        // Arrange – amounts above $500 require OTP
        var (sender, senderWallet) = WalletTestData.CreateUserWithWallet(balance: 5_000m);

        _currentUser.Setup(c => c.UserId).Returns(sender.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(sender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(senderWallet);
        _wallets.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(WalletTestData.CreateSolventWallet());
        _users.Setup(u => u.GetByIdAsync(sender.Id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(sender);
        _transactions.Setup(t => t.GetDailySpentAmountAsync(sender.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(0m);

        var command = BuildCommand(amount: 600m, otpCode: null); // no OTP supplied

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_OTP");
    }

    // ─── OTP – valid ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OtpProvidedAndValid_ReturnsSuccess()
    {
        // Arrange
        var (sender, senderWallet) = WalletTestData.CreateUserWithWallet(balance: 5_000m);
        var receiverWallet         = WalletTestData.CreateSolventWallet();

        _currentUser.Setup(c => c.UserId).Returns(sender.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(sender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(senderWallet);
        _wallets.Setup(w => w.GetByIdAsync(receiverWallet.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(receiverWallet);
        _users.Setup(u => u.GetByIdAsync(sender.Id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(sender);
        _transactions.Setup(t => t.GetDailySpentAmountAsync(sender.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(0m);

        // Simulate valid OTP stored in cache / OTP service
        _cache.Setup(c => c.GetAsync<string>($"otp:{sender.Id}", It.IsAny<CancellationToken>()))
              .ReturnsAsync("123456");

        var command = BuildCommand(
            amount: 600m,
            receiverWalletId: receiverWallet.Id,
            otpCode: "123456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ─── Concurrency conflict ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ConcurrencyConflict_ReturnsConcurrencyErrorCode()
    {
        // Arrange – commit throws the EF concurrency exception
        var (sender, senderWallet) = WalletTestData.CreateUserWithWallet(balance: 500m);
        var receiverWallet         = WalletTestData.CreateSolventWallet();

        _currentUser.Setup(c => c.UserId).Returns(sender.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(sender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(senderWallet);
        _wallets.Setup(w => w.GetByIdAsync(receiverWallet.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(receiverWallet);
        _users.Setup(u => u.GetByIdAsync(sender.Id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(sender);
        _transactions.Setup(t => t.GetDailySpentAmountAsync(sender.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(0m);
        _cache.Setup(c => c.GetAsync<string>($"otp:{sender.Id}", It.IsAny<CancellationToken>()))
              .ReturnsAsync("123456");

        _uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Row was modified by another user."));

        var command = BuildCommand(amount: 100m, receiverWalletId: receiverWallet.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CONCURRENCY_CONFLICT");
    }

    // ─── Rollback on failure ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CreditThrows_RollsBackTransaction()
    {
        // Arrange – credit step blows up mid-transfer
        var (sender, senderWallet) = WalletTestData.CreateUserWithWallet(balance: 500m);
        var receiverWallet         = WalletTestData.CreateSolventWallet();

        _currentUser.Setup(c => c.UserId).Returns(sender.Id);
        _wallets.Setup(w => w.GetByOwnerIdAsync(sender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(senderWallet);
        _wallets.Setup(w => w.GetByIdAsync(receiverWallet.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(receiverWallet);
        _users.Setup(u => u.GetByIdAsync(sender.Id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(sender);
        _transactions.Setup(t => t.GetDailySpentAmountAsync(sender.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(0m);

        _wallets.Setup(w => w.CreditAsync(receiverWallet.Id, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Credit step failure"));

        var command = BuildCommand(amount: 100m, receiverWalletId: receiverWallet.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once,
            because: "any exception during transfer must trigger a rollback");
    }
}
