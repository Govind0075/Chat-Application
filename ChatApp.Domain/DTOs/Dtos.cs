namespace ChatApp.Domain.DTOs;

// ── Inbound (client → API) ────────────────────────────────────────────────────

public record RegisterDto(string Username, string Email, string Password);

public record LoginDto(string Email, string Password);

public record AuthResponseDto(
    string AccessToken,
    string Username,
    string Email,
    Guid   UserId);
