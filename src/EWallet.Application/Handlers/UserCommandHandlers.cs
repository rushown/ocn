using AutoMapper;
using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.DTOs;
using EWallet.Application.Interfaces;
using EWallet.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, Result<UserProfileDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateUserProfileCommandHandler> _logger;

    public UpdateUserProfileCommandHandler(IUnitOfWork uow, IMapper mapper, ILogger<UpdateUserProfileCommandHandler> logger)
    {
        _uow = uow; _mapper = mapper; _logger = logger;
    }

    public async Task<Result<UserProfileDto>> Handle(UpdateUserProfileCommand request, CancellationToken ct)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
            if (user is null)
                return Result<UserProfileDto>.Failure("User not found.", ErrorCodes.UserNotFound);

            user.UpdateProfile(request.FullName, request.PhoneNumber);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Profile updated for user {UserId}", request.UserId);
            return Result<UserProfileDto>.Success(_mapper.Map<UserProfileDto>(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user {UserId}", request.UserId);
            return Result<UserProfileDto>.Failure("Profile update failed.");
        }
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(IUnitOfWork uow, IPasswordHasher passwordHasher, ILogger<ChangePasswordCommandHandler> logger)
    {
        _uow = uow; _passwordHasher = passwordHasher; _logger = logger;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
            if (user is null)
                return Result.Failure("User not found.", ErrorCodes.UserNotFound);

            if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
                return Result.Failure("Current password is incorrect.");

            user.SetPasswordHash(_passwordHasher.Hash(request.NewPassword));
            user.ClearRefreshToken(); // force re-login after password change

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Password changed for user {UserId}", request.UserId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", request.UserId);
            return Result.Failure("Password change failed.");
        }
    }
}

public class EnableTwoFactorCommandHandler : IRequestHandler<EnableTwoFactorCommand, Result<string>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EnableTwoFactorCommandHandler> _logger;

    public EnableTwoFactorCommandHandler(IUnitOfWork uow, ILogger<EnableTwoFactorCommandHandler> logger)
    {
        _uow = uow; _logger = logger;
    }

    public async Task<Result<string>> Handle(EnableTwoFactorCommand request, CancellationToken ct)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
            if (user is null)
                return Result<string>.Failure("User not found.", ErrorCodes.UserNotFound);

            var secret = user.GenerateTotpSecret();
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("2FA enabled for user {UserId}", request.UserId);
            return Result<string>.Success(secret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling 2FA for user {UserId}", request.UserId);
            return Result<string>.Failure("Failed to enable two-factor authentication.");
        }
    }
}

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, Result<bool>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<VerifyOtpCommandHandler> _logger;

    public VerifyOtpCommandHandler(IUnitOfWork uow, ILogger<VerifyOtpCommandHandler> logger)
    {
        _uow = uow; _logger = logger;
    }

    public async Task<Result<bool>> Handle(VerifyOtpCommand request, CancellationToken ct)
    {
        try
        {
            if (!Enum.TryParse<OtpPurpose>(request.Purpose, ignoreCase: true, out var purpose))
                return Result<bool>.Failure("Unknown OTP purpose.", ErrorCodes.InvalidOtp);

            var isValid = await _uow.Users.ValidateOtpAsync(request.UserId, request.Code, purpose, ct);
            if (!isValid)
                return Result<bool>.Failure("Invalid or expired OTP.", ErrorCodes.InvalidOtp);

            await _uow.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying OTP for user {UserId}", request.UserId);
            return Result<bool>.Failure("OTP verification failed.");
        }
    }
}
