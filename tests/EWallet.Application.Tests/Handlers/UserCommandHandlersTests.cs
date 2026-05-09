using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using EWallet.Application.Commands;
using EWallet.Application.Handlers;
using EWallet.Application.Interfaces;
using EWallet.Application.Queries;
using EWallet.Application.Tests.Helpers;

namespace EWallet.Application.Tests.Handlers;

public class UserCommandHandlersTests
{
    [Fact]
    public async Task ChangePassword_ValidCurrentPassword_ClearsRefreshToken()
    {
        var (uow, _, _, users, _) = MockUnitOfWorkFactory.Create();
        var hasher = new Mock<IPasswordHasher>();
        var logger = new Mock<ILogger<ChangePasswordCommandHandler>>();
        var handler = new ChangePasswordCommandHandler(uow.Object, hasher.Object, logger.Object);

        var userId = Guid.NewGuid();
        var user = WalletTestData.CreateActiveUser("user@test.com", "oldHash");
        user.UpdateRefreshToken("old-refresh", DateTime.UtcNow.AddDays(1));

        users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        hasher.Setup(h => h.Verify("Old@1234", "oldHash")).Returns(true);
        hasher.Setup(h => h.Hash("New@1234")).Returns("newHash");

        var result = await handler.Handle(
            new ChangePasswordCommand(userId, "Old@1234", "New@1234", "New@1234"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task WalletLookup_ValidWallet_ReturnsOwnerMetadata()
    {
        var (uow, wallets, _, users, _) = MockUnitOfWorkFactory.Create();
        var logger = new Mock<ILogger<GetWalletLookupQueryHandler>>();
        var handler = new GetWalletLookupQueryHandler(uow.Object, logger.Object);

        var owner = WalletTestData.CreateActiveUser("owner@test.com");
        var wallet = WalletTestData.CreateWalletForUser(owner.Id, 100m, "USD");

        wallets.Setup(w => w.GetByIdAsync(wallet.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);
        users.Setup(u => u.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        var result = await handler.Handle(new GetWalletLookupQuery(wallet.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.OwnerName.Should().Be(owner.FullName);
        result.Value!.Currency.Should().Be("USD");
    }
}
