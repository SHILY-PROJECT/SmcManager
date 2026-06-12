namespace SmcManager.Core.Models;

/// <summary>
/// Результат проверки cookies / сессии аккаунта.
/// </summary>
public class SocialAccountValidationResult
{
    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? Username { get; init; }

    public static SocialAccountValidationResult Ok(string message, string? username = null) => new()
    {
        IsValid = true,
        Message = message,
        Username = username
    };

    public static SocialAccountValidationResult Fail(string message) => new()
    {
        IsValid = false,
        Message = message
    };
}
