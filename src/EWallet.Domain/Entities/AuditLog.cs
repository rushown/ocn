namespace EWallet.Domain.Entities;

/// <summary>
/// Immutable audit record capturing a state-change event on any domain entity.
/// Once created, an <see cref="AuditLog"/> cannot be modified — there are no update methods.
/// </summary>
public sealed class AuditLog : BaseEntity
{
    /// <summary>The primary key of the entity that was changed.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>The CLR type name of the entity that was changed (e.g. "Wallet", "User").</summary>
    public string EntityType { get; private set; } = default!;

    /// <summary>The action performed (e.g. "Create", "Update", "Delete", "StatusChange").</summary>
    public string Action { get; private set; } = default!;

    /// <summary>JSON representation of the entity state before the action, or <c>null</c> for creates.</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON representation of the entity state after the action, or <c>null</c> for deletes.</summary>
    public string? NewValues { get; private set; }

    /// <summary>The user who performed the action, or <c>null</c> for system-initiated changes.</summary>
    public Guid? PerformedByUserId { get; private set; }

    /// <summary>The IP address from which the action was initiated.</summary>
    public string IpAddress { get; private set; } = default!;

    /// <summary>UTC timestamp when the action occurred.</summary>
    public DateTime Timestamp { get; private set; }

    // EF Core parameterless constructor
    private AuditLog() { }

    /// <summary>
    /// Creates an immutable <see cref="AuditLog"/> entry.
    /// </summary>
    /// <param name="entityId">PK of the affected entity.</param>
    /// <param name="entityType">Type name of the affected entity.</param>
    /// <param name="action">The action performed.</param>
    /// <param name="oldValues">JSON of state before action (<c>null</c> for creates).</param>
    /// <param name="newValues">JSON of state after action (<c>null</c> for deletes).</param>
    /// <param name="userId">User who performed the action (<c>null</c> for system actions).</param>
    /// <param name="ip">Client IP address.</param>
    /// <returns>A new, fully populated <see cref="AuditLog"/>.</returns>
    public static AuditLog Create(
        Guid entityId,
        string entityType,
        string action,
        string? oldValues,
        string? newValues,
        Guid? userId,
        string ip)
    {
        return new AuditLog
        {
            EntityId = entityId,
            EntityType = entityType,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
            PerformedByUserId = userId,
            IpAddress = ip,
            Timestamp = DateTime.UtcNow
        };
    }
}
