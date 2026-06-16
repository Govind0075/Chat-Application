using ChatApp.Domain.Common;
using ChatApp.Domain.DTOs;

namespace ChatApp.BLL.Interfaces;

/// <summary>
/// Auth service contract.
/// Returns Result&lt;T&gt; instead of throwing exceptions for business-rule failures
/// (e.g. "Email already registered" or "Wrong password").
/// Actual server errors (DB down, etc.) are allowed to propagate as exceptions
/// and are caught by ExceptionMiddleware.
/// </summary>
public interface IAuthService
{
    /// <summary>Register a new user. Returns a ready-to-use auth response.</summary>
    Task<Result<AuthResponseDto>> RegisterAsync(RegisterDto dto, CancellationToken ct = default);

    /// <summary>
    /// Validate credentials and issue tokens.
    /// The refresh token string is returned separately so the controller can
    /// set it as an HttpOnly cookie.
    /// </summary>
    Task<Result<(AuthResponseDto Response, string RefreshToken)>> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>
    /// Exchange a valid refresh token for a new access token + new refresh token
    /// (token rotation — the old refresh token is invalidated immediately).
    /// </summary>
    Task<Result<(AuthResponseDto Response, string RefreshToken)>> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revoke the refresh token so it can never be reused (logout).</summary>
    Task<Result<bool>> RevokeAsync(string refreshToken, CancellationToken ct = default);
}
