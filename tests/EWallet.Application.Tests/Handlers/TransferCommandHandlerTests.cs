using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using EWallet.Application.Commands;
using EWallet.Application.Handlers;
using EWallet.Application.Interfaces;
using EWallet.Application.Tests.Helpers;
using EWallet.Domain.Enums;

namespace EWallet.Application.Tests.Handlers;

public class TransferCommandHandlerTests
{
    [Fact]
    public async Task Handle_AmountAboveThreshold_WithoutOtp_ReturnsFailure()
    {
        var (uow, wallets, transactions, users, _) = MockUnitOfWorkFactory.Create();
        var idempotency = new Mock<IIdempotencyService>();
        var notifications = new Mock<IWalletNotificationService>();
        var currentUser = new Mock<ICurrentUserService>();
        var mapper = new Mock<AutoMapper.IMapper>();
        var logger = new Mock<ILogger<TransferCommandHandler>>();

        var senderUserId = Guid.NewGuid();
        var receiverUserId = Guid.NewGuid();
        var senderWallet = WalletTestData.CreateWalletForUser(senderUserId, balance: 5000m);
        var receiverWallet = WalletTestData.CreateWalletForUser(receiverUserId, balance: 0m);
        var user = WalletTestData.CreateActiveUser("sender@test.com");
        user.UpgradeKyc(KycLevel.Tier1);

        idempotency.Setup(i => i.GetCachedResponseAsync<EWallet.Application.DTOs.TransactionDto>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EWallet.Application.DTOs.TransactionDto?)null);
        wallets.Setup(w => w.GetByUserIdAsync(senderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senderWallet);
        wallets.Setup(w => w.GetByIdAsync(receiverWallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receiverWallet);
        users.Setup(u => u.GetByIdAsync(senderUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        transactions.Setup(t => t.GetDailyDebitSumAsync(senderWallet.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var handler = new TransferCommandHandler(
            uow.Object,
            idempotency.Object,
            notifications.Object,
            currentUser.Object,
            mapper.Object,
            logger.Object);

        var result = await handler.Handle(
            new TransferCommand(senderUserId, receiverWallet.Id, 700m, "USD", "test", null, "idem"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("OTP is required");
    }
}
