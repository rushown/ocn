using FluentAssertions;
using Moq;
using EWallet.Application.Commands;
using EWallet.Application.Interfaces;
using EWallet.Application.Tests.Helpers;
using EWallet.Application.Handlers;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Tests.Handlers;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<EWallet.Domain.Interfaces.IUserRepository> _users;
    private readonly Mock<IPasswordHasher> _passwordHasher;
    private readonly Mock<IJwtService> _jwtService;
    private readonly LoginCommandHandler _handler;

    private const string ValidEmail = "test@test.com";
    private const string CorrectPassword = "C0rrectP@ss!";

    public LoginCommandHandlerTests()
    {
        (_uow, _, _, _users, _) = MockUnitOfWorkFactory.Create();
        _passwordHasher = new Mock<IPasswordHasher>();
        _jwtService = new Mock<IJwtService>();
        var logger = new Mock<ILogger<LoginCommandHandler>>();

        _jwtService.Setup(t => t.GenerateRefreshToken()).Returns("mocked-refresh-token");
        _jwtService.Setup(t => t.GenerateAccessToken(It.IsAny<EWallet.Domain.Entities.User>())).Returns("mocked.access.token");

        _handler = new LoginCommandHandler(_uow.Object, _passwordHasher.Object, _jwtService.Object, logger.Object);
    }

    // ─── Valid credentials ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccessWithAccessToken()
    {
        // Arrange
        var user = WalletTestData.CreateActiveUser(ValidEmail, passwordHash: "hash");

        _users.Setup(u => u.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
        _passwordHasher.Setup(p => p.Verify(CorrectPassword, "hash")).Returns(true);

        // Act
        var result = await _handler.Handle(new LoginCommand(ValidEmail, CorrectPassword), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value!.Email.Should().Be(ValidEmail);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── User not found ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange – repository returns null for unknown email
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((EWallet.Domain.Entities.User?)null);

        // Act
        var result = await _handler.Handle(
            new LoginCommand("nobody@test.com", "anyPass123!"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    // ─── Inactive account ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_InactiveUser_ReturnsUserInactiveError()
    {
        // Arrange
        var user = WalletTestData.CreateInactiveUser(ValidEmail, passwordHash: "hash");

        _users.Setup(u => u.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
        _passwordHasher.Setup(p => p.Verify(CorrectPassword, "hash")).Returns(true);

        // Act
        var result = await _handler.Handle(new LoginCommand(ValidEmail, CorrectPassword), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disabled");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsFailure_AndDoesNotSaveChanges()
    {
        // Arrange
        var user = WalletTestData.CreateActiveUser(ValidEmail, passwordHash: "hash");

        _users.Setup(u => u.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
        _passwordHasher.Setup(p => p.Verify("Wrong@123!", "hash")).Returns(false);

        // Act
        var result = await _handler.Handle(
            new LoginCommand(ValidEmail, "Wrong@123!"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid credentials");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
