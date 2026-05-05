namespace EWallet.Application.Interfaces;

public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default);
    Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default);
    Task SendOtpAsync(string phoneNumber, string email, string otp, CancellationToken ct = default);
}
