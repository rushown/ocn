namespace EWallet.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class InsufficientFundsException : DomainException
{
    public InsufficientFundsException() : base("Insufficient funds.") { }
}

public class WalletLockedException : DomainException
{
    public WalletLockedException() : base("Wallet is locked.") { }
}

public class InvalidTransactionStateException : DomainException
{
    public InvalidTransactionStateException(string message) : base(message) { }
}

public class OtpAlreadyUsedException : DomainException
{
    public OtpAlreadyUsedException() : base("OTP has already been used.") { }
}

public class OtpExpiredException : DomainException
{
    public OtpExpiredException() : base("OTP has expired.") { }
}
