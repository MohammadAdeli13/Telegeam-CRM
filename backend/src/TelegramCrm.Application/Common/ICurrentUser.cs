namespace TelegramCrm.Application.Common;

/// <summary>Identity of the authenticated caller, derived only from validated claims.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    long? TelegramUserId { get; }

    string? Role { get; }

    bool IsAuthenticated { get; }
}
