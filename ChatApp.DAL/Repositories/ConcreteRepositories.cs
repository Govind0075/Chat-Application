using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.DAL.Repositories;

// ── User ──────────────────────────────────────────────────────────────────────
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db) { }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), ct);

    public Task<User?> GetByConnectionIdAsync(string connectionId, CancellationToken ct = default)
        => _db.UserConnections
              .Include(uc => uc.User)
              .Where(uc => uc.ConnectionId == connectionId && uc.IsActive)
              .Select(uc => uc.User)
              .FirstOrDefaultAsync(ct);

    public Task<List<User>> GetAllUsersAsync(CancellationToken ct = default)
        => _set.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync(ct);
}

// ── Room ──────────────────────────────────────────────────────────────────────
public class RoomRepository : Repository<Room>, IRoomRepository
{
    public RoomRepository(AppDbContext db) : base(db) { }

    public Task<Room?> GetByIdWithMembersAsync(int roomId, CancellationToken ct = default)
        => _set
           .Include(r => r.Members).ThenInclude(m => m.User)
           .Include(r => r.CreatedBy)
           .FirstOrDefaultAsync(r => r.Id == roomId, ct);

    public Task<List<Room>> GetRoomsForUserAsync(int userId, CancellationToken ct = default)
        => _set
           .Include(r => r.Members).ThenInclude(m => m.User)
           .Where(r => r.Members.Any(m => m.UserId == userId))
           .OrderByDescending(r => r.CreatedAt)
           .AsNoTracking()
           .ToListAsync(ct);

    public Task<Room?> GetDirectMessageRoomAsync(int userAId, int userBId, CancellationToken ct = default)
        => _set
           .Include(r => r.Members)
           .Where(r => r.Type == Domain.Enums.RoomType.DirectMessage
                    && r.Members.Any(m => m.UserId == userAId)
                    && r.Members.Any(m => m.UserId == userBId))
           .FirstOrDefaultAsync(ct);

    public Task<bool> IsUserMemberAsync(int roomId, int userId, CancellationToken ct = default)
        => _db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == userId, ct);
}

// ── Message ───────────────────────────────────────────────────────────────────
public class MessageRepository : Repository<Message>, IMessageRepository
{
    public MessageRepository(AppDbContext db) : base(db) { }

    public Task<List<Message>> GetPagedAsync(int roomId, int page, int pageSize, CancellationToken ct = default)
        => _set
           .Include(m => m.User)
           .Where(m => m.RoomId == roomId)
           .OrderByDescending(m => m.CreatedAt)
           .Skip((page - 1) * pageSize).Take(pageSize)
           .OrderBy(m => m.CreatedAt)
           .AsNoTracking()
           .ToListAsync(ct);

    public Task<int> CountByRoomAsync(int roomId, CancellationToken ct = default)
        => _set.CountAsync(m => m.RoomId == roomId, ct);

    public Task<Message?> GetByIdWithUserAsync(int messageId, CancellationToken ct = default)
        => _set.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == messageId, ct);

    public Task<Message?> GetLastMessageAsync(int roomId, CancellationToken ct = default)
        => _set.Include(m => m.User)
               .Where(m => m.RoomId == roomId)
               .OrderByDescending(m => m.CreatedAt)
               .FirstOrDefaultAsync(ct);
}

// ── UserConnection ────────────────────────────────────────────────────────────
public class UserConnectionRepository : Repository<UserConnection>, IUserConnectionRepository
{
    public UserConnectionRepository(AppDbContext db) : base(db) { }

    public Task<UserConnection?> GetActiveAsync(string connectionId, CancellationToken ct = default)
        => _set.Include(uc => uc.User)
               .FirstOrDefaultAsync(uc => uc.ConnectionId == connectionId && uc.IsActive, ct);

    public Task<List<UserConnection>> GetActiveByUserAsync(int userId, CancellationToken ct = default)
        => _set.Where(uc => uc.UserId == userId && uc.IsActive).ToListAsync(ct);

    public async Task DeactivateAsync(string connectionId, CancellationToken ct = default)
    {
        var conn = await _set.FirstOrDefaultAsync(uc => uc.ConnectionId == connectionId, ct);
        if (conn is null) return;
        conn.IsActive        = false;
        conn.DisconnectedAt  = DateTime.UtcNow;
        conn.UpdatedAt       = DateTime.UtcNow;
    }
}
