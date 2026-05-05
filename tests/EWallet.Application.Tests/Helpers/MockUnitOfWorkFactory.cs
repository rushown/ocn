using Moq;
using EWallet.Application.Interfaces;
using EWallet.Domain.Interfaces;

namespace EWallet.Application.Tests.Helpers;

/// <summary>
/// Factory for creating consistently wired-up UoW + repository mocks.
/// Use this in every handler test constructor instead of hand-rolling the same setup.
/// </summary>
public static class MockUnitOfWorkFactory
{
    public static (
        Mock<IUnitOfWork> uow,
        Mock<IWalletRepository> wallets,
        Mock<ITransactionRepository> transactions,
        Mock<IUserRepository> users)
        Create()
    {
        var wallets = new Mock<IWalletRepository>();
        var transactions = new Mock<ITransactionRepository>();
        var users = new Mock<IUserRepository>();

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Wallets).Returns(wallets.Object);
        uow.Setup(u => u.Transactions).Returns(transactions.Object);
        uow.Setup(u => u.Users).Returns(users.Object);

        // Default: async operations complete successfully
        uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        uow.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(1);

        return (uow, wallets, transactions, users);
    }
}
