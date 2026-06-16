using ChatApp.BLL.Interfaces;
using ChatApp.Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.API.Controllers;

/// <summary>
/// Handles all authentication endpoints.
/// The refresh token travels as an HttpOnly cookie — JavaScript cannot read it,
/// which protects against XSS attacks stealing long-lived credentials.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private const string RefreshTokenCookie = "refreshToken";

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(dto, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(dto, ct);
        if (!result.IsSuccess)
            return Unauthorized(new { error = result.Error });

        var (response, refreshToken) = result.Value;
        SetRefreshTokenCookie(refreshToken);

        return Ok(response);
    }

    // ── POST /api/auth/refresh ────────────────────────────────────────────────
    // Client sends NO body — the refresh token comes automatically in the cookie.
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var token = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { error = "Refresh token cookie is missing." });

        var result = await _auth.RefreshAsync(token, ct);
        if (!result.IsSuccess)
            return Unauthorized(new { error = result.Error });

        var (response, newRefreshToken) = result.Value;
        SetRefreshTokenCookie(newRefreshToken); // rotate: set brand-new cookie

        return Ok(response);
    }

    // ── POST /api/auth/logout ─────────────────────────────────────────────────
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = Request.Cookies[RefreshTokenCookie];
        if (!string.IsNullOrEmpty(token))
            await _auth.RevokeAsync(token, ct);

        // Delete the cookie on the client regardless
        Response.Cookies.Delete(RefreshTokenCookie);
        return NoContent();
    }

    // ── GET /api/auth/me ──────────────────────────────────────────────────────
    // Protected endpoint — demonstrates [Authorize] + reading JWT claims.
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        // These claims were embedded in the JWT by AuthService.GenerateAccessToken
        var userId   = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                    ?? User.FindFirst("unique_name")?.Value;
        var email    = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? User.FindFirst("email")?.Value;

        return Ok(new { userId, username, email });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the refresh token as an HttpOnly, Secure, SameSite=Strict cookie.
    /// HttpOnly  → JS cannot read it (XSS safe).
    /// Secure    → only sent over HTTPS.
    /// SameSite  → blocks CSRF cross-origin requests.
    /// </summary>
    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append(RefreshTokenCookie, token, new CookieOptions
        {
            HttpOnly  = true,
            Secure    = true,
            SameSite  = SameSiteMode.Strict,
            Expires   = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
