using EWallet.Domain.Exceptions;

namespace EWallet.Domain.Entities;

public sealed class OtpRecord
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Code { get; private set; } = default!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    private OtpRecord() { }

    public static OtpRecord Create(Guid userId, string code, DateTimeOffset expiresAt)
    {
        return new OtpRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = code,
            ExpiresAt = expiresAt,
            IsUsed = false
        };
    }

    public void MarkUsed()
    {
        if (IsExpired)
            throw new OtpExpiredException();

        if (IsUsed)
            throw new OtpAlreadyUsedException();

        IsUsed = true;
    }
}
