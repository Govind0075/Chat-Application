using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ChatApp.BLL.Interfaces;
using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Common;
using ChatApp.Domain.DTOs;
using ChatApp.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ChatApp.BLL.Services;

/// <summary>
/// Implements registration, login, refresh-token rotation, and logout.
///
/// JWT FLOW (worth understanding):
///   1.  Client logs in  → receives a short-lived ACCESS TOKEN (15 min JWT)
///                          + a long-lived REFRESH TOKEN (7-day opaque string)
///                          stored in an HttpOnly cookie.
///   2.  Client uses ACCESS TOKEN in Authorization: Bearer header for every API call.
///   3.  When the access token expires, the client sends a /refresh request.
///       The server reads the HttpOnly cookie, validates the refresh token
///       against the DB, issues a NEW access token + NEW refresh token
///       (token rotation), and invalidates the old refresh token.
///   4.  Logout → server clears the refresh token from the DB.  Even if
///       someone steals the access token it will expire in 15 min.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork    _uow;
    private readonly IConfiguration _config;

    public AuthService(IUnitOfWork uow, IConfiguration config)
    {
        _uow    = uow;
        _config = config;
    }

    // ── Register ──────────────────────────────────────────────────────────────

    public async Task<Result<AuthResponseDto>> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (await _uow.Users.EmailExistsAsync(dto.Email.ToLower()))
            return Result<AuthResponseDto>.Failure("Email is already registered.");

        if (await _uow.Users.UsernameExistsAsync(dto.Username))
            return Result<AuthResponseDto>.Failure("Username is already taken.");

        var user = new User
        {
            Username     = dto.Username,
            Email        = dto.Email.ToLower(),
            // BCrypt automatically generates a salt and embeds it in the hash
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync(ct);

        var accessToken = GenerateAccessToken(user);
        return Result<AuthResponseDto>.Success(
            new AuthResponseDto(accessToken, user.Username, user.Email, user.Id));
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    public async Task<Result<(AuthResponseDto Response, string RefreshToken)>> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByEmailAsync(dto.Email.ToLower());

        // Intentionally vague error — don't reveal which field is wrong
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Result<(AuthResponseDto, string)>.Failure("Invalid email or password.");

        var (accessToken, refreshToken) = await IssueTokensAsync(user, ct);

        return Result<(AuthResponseDto, string)>.Success(
            (new AuthResponseDto(accessToken, user.Username, user.Email, user.Id), refreshToken));
    }

    // ── Refresh (token rotation) ───────────────────────────────────────────────

    public async Task<Result<(AuthResponseDto Response, string RefreshToken)>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByRefreshTokenAsync(refreshToken);

        if (user is null)
            return Result<(AuthResponseDto, string)>.Failure("Invalid refresh token.");

        if (user.RefreshTokenExpiry < DateTime.UtcNow)
            return Result<(AuthResponseDto, string)>.Failure("Refresh token has expired. Please log in again.");

        // Token rotation: issue a brand-new pair and invalidate the old refresh token
        var (accessToken, newRefreshToken) = await IssueTokensAsync(user, ct);

        return Result<(AuthResponseDto, string)>.Success(
            (new AuthResponseDto(accessToken, user.Username, user.Email, user.Id), newRefreshToken));
    }

    // ── Revoke (logout) ───────────────────────────────────────────────────────

    public async Task<Result<bool>> RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByRefreshTokenAsync(refreshToken);
        if (user is null)
            return Result<bool>.Failure("Token not found.");

        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Generates a new access + refresh token pair, persists the refresh token,
    /// and returns both strings.
    /// </summary>
    private async Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(
        User user, CancellationToken ct)
    {
        var accessToken   = GenerateAccessToken(user);
        var refreshToken  = GenerateRefreshToken();
        var expiryDays    = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

        user.RefreshToken       = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(expiryDays);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        return (accessToken, refreshToken);
    }

    /// <summary>
    /// Builds a signed JWT containing the user's identity as claims.
    /// Claims are readable by the client (base64-decoded), but the signature
    /// is verified by the server — so the client cannot forge or tamper with them.
    /// </summary>
    private string GenerateAccessToken(User user)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry  = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        // Claims are the "payload" of the JWT — metadata about the authenticated user
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()) // unique token ID
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// A refresh token is just a cryptographically random opaque string —
    /// it has no embedded data. We look it up in the DB to find which user it belongs to.
    /// </summary>
    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
