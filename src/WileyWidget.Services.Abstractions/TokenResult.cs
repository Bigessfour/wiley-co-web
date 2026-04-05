using System;

namespace WileyWidget.Services.Abstractions;

/// <summary>
/// Result wrapper for OAuth token operations indicating success or failure.
/// </summary>
public sealed record TokenResult(
    bool IsSuccess,
    string? AccessToken = null,
    string? RefreshToken = null,
    int ExpiresIn = 0,
    string? ErrorMessage = null,
    Exception? Exception = null)
{
    /// <summary>
    /// Creates a successful token result.
    /// </summary>
    public static TokenResult Success(string accessToken, string? refreshToken = null, int expiresIn = 3600) =>
        new(IsSuccess: true, AccessToken: accessToken, RefreshToken: refreshToken, ExpiresIn: expiresIn);

    /// <summary>
    /// Creates a failed token result.
    /// </summary>
    public static TokenResult Failure(string message, Exception? ex = null) =>
        new(IsSuccess: false, ErrorMessage: message, Exception: ex);
}
