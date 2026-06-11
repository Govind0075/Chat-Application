using ChatApp.BLL.Interfaces;
using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Common;
using ChatApp.Domain.DTOs;
using ChatApp.Domain.Entities;

namespace ChatApp.BLL.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private static readonly string[] Colors = ["#6366f1","#8b5cf6","#14b8a6","#f59e0b","#ec4899","#10b981","#3b82f6","#ef4444"];

    public UserService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<UserDto>> LoginAsync(string username, CancellationToken ct = default)
    {
        username = username.Trim();
        if (string.IsNullOrEmpty(username) || username.Length > 50)
            return Result<UserDto>.Fail("Username must be 1–50 characters.");

        var user = await _uow.Users.GetByUsernameAsync(username, ct);
        if (user is null)
        {
            var color = Colors[Math.Abs(username.GetHashCode()) % Colors.Length];
            user = new User { Username = username, DisplayName = username, AvatarColor = color };
            await _uow.Users.AddAsync(user, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return Result<UserDto>.Ok(Map(user));
    }

    public async Task<Result<UserDto>> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        return user is null ? Result<UserDto>.Fail("User not found.") : Result<UserDto>.Ok(Map(user));
    }

    public async Task<Result<List<UserDto>>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = await _uow.Users.GetAllUsersAsync(ct);
        return Result<List<UserDto>>.Ok(users.Select(Map).ToList());
    }

    public async Task<Result> SetOnlineAsync(int userId, string connectionId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null) return Result.Fail("User not found.");
        user.IsOnline = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _uow.Users.UpdateAsync(user, ct);

        var conn = new UserConnection { UserId = userId, ConnectionId = connectionId };
        await _uow.UserConnections.AddAsync(conn, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetOfflineAsync(string connectionId, CancellationToken ct = default)
    {
        await _uow.UserConnections.DeactivateAsync(connectionId, ct);
        var user = await _uow.Users.GetByConnectionIdAsync(connectionId, ct);
        if (user is not null)
        {
            var remaining = await _uow.UserConnections.GetActiveByUserAsync(user.Id, ct);
            if (!remaining.Any())
            {
                user.IsOnline   = false;
                user.LastSeenAt = DateTime.UtcNow;
                user.UpdatedAt  = DateTime.UtcNow;
                await _uow.Users.UpdateAsync(user, ct);
            }
        }
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<User?>> GetUserByConnectionIdAsync(string connectionId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByConnectionIdAsync(connectionId, ct);
        return Result<User?>.Ok(user);
    }

    public static UserDto Map(User u) => new(u.Id, u.Username, u.DisplayName, u.AvatarColor, u.IsOnline, u.LastSeenAt);
}
