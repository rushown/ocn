using FluentAssertions;
using Moq;
using EWallet.Application.Commands.Auth;
using EWallet.Application.Interfaces;
using EWallet.Application.Tests.Helpers;
using EWallet.Domain.Interfaces;

namespace EWallet.Application.Tests.Handlers;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow;
    private readonly Mock<IUserRepository> _users;
    private readonly Mock<ITokenService> _tokenService;
    private readonly LoginCommandHandler _handler;

    private const string ValidEmail    = "test@test.com";
    private const string CorrectPassword = "C0rrectP@ss!";
    private const string WrongPassword   = "wr0ngP@ss!";

    public LoginCommandHandlerTests()
    {
        (_uow, _, _, _users) = MockUnitOfWorkFactory.Create();
        _tokenService = new Mock<ITokenService>();

        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<Domain.Entities.User>()))
                     .Returns("mocked.access.token");
        _tokenService.Setup(t => t.GenerateRefreshToken())
                     .Returns("mocked-refresh-token");

        _handler = new LoginCommandHandler(_uow.Object, _tokenService.Object);
    }

    private static LoginCommand BuildCommand(string email, string password) =>
        new() { Email = email, Password = password };

    // ─── Valid credentials ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccessWithAccessToken()
    {
        // Arrange
        var user = WalletTestData.CreateTier1User(isActive: true);
        // Overwrite the random hash with one that matches CorrectPassword
        user.Email        = ValidEmail;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword);

        _users.Setup(u => u.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(BuildCommand(ValidEmail, CorrectPassword), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().NotBeNullOrWhiteSpace();

        _users.Verify(u => u.UpdateAsync(
            It.Is<Domain.Entities.User>(u => u.RefreshToken != null),
            It.IsAny<CancellationToken>()), Times.Once,
            because: "a successful login must persist the new refresh token");
    }

    // ─── Wrong password ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WrongPassword_ReturnsFailureWithoutToken()
    {
        // Arrange
        var user = WalletTestData.CreateTier1User(isActive: true);
        user.Email        = ValidEmail;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword);

        _users.Setup(u => u.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(BuildCommand(ValidEmail, WrongPassword), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    // ─── User not found ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserNotFound_ReturnsFailure()
    {
        // Arrange – repository returns null for unknown email
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Domain.Entities.User?)null);

        // Act
        var result = await _handler.Handle(
            BuildCommand("nobody@test.com", "anyPass123!"),
            CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    // ─── Inactive account ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_InactiveUser_ReturnsUserInactiveError()
    {
        // Arrange
        var user = WalletTestData.CreateTier1User(isActive: false); // disabled account
        user.Email        = ValidEmail;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword);

        _users.Setup(u => u.GetByEmailAsync(ValidEmail, It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(BuildCommand(ValidEmail, CorrectPassword), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("USER_INACTIVE");
    }
}
