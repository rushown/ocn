using EWallet.Application.Commands;
using FluentValidation;

namespace EWallet.Application.Validators;

public class TransferCommandValidator : AbstractValidator<TransferCommand>
{
    public TransferCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(100_000);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO code (e.g. USD).");

        RuleFor(x => x.ReceiverWalletId)
            .NotEmpty();

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.OtpCode)
            .NotEmpty()
            .Length(6)
            .When(x => x.Amount > 500)
            .WithMessage("A 6-digit OTP is required for transfers above $500.");

        RuleFor(x => x.SenderUserId)
            .NotEmpty();
    }
}

public class DepositCommandValidator : AbstractValidator<DepositCommand>
{
    public DepositCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0.01m)
            .LessThanOrEqualTo(100_000m);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Z]{3}$");

        RuleFor(x => x.ExternalRef)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

public class WithdrawCommandValidator : AbstractValidator<WithdrawCommand>
{
    public WithdrawCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0.01m)
            .LessThanOrEqualTo(100_000m);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Z]{3}$");

        RuleFor(x => x.ExternalRef)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}

public class LockWalletCommandValidator : AbstractValidator<LockWalletCommand>
{
    public LockWalletCommandValidator()
    {
        RuleFor(x => x.WalletId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}

public class UnlockWalletCommandValidator : AbstractValidator<UnlockWalletCommand>
{
    public UnlockWalletCommandValidator()
    {
        RuleFor(x => x.WalletId).NotEmpty();
    }
}
