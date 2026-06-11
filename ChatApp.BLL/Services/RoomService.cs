using ChatApp.BLL.Interfaces;
using ChatApp.DAL;
using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Common;
using ChatApp.Domain.DTOs;
using ChatApp.Domain.Entities;
using ChatApp.Domain.Enums;

namespace ChatApp.BLL.Services;

public class RoomService : IRoomService
{
    private readonly IUnitOfWork _uow;
    public RoomService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<RoomDto>> GetOrCreateDmAsync(int requestingUserId, int targetUserId, CancellationToken ct = default)
    {
        if (requestingUserId == targetUserId)
            return Result<RoomDto>.Fail("Cannot create DM with yourself.");

        var target = await _uow.Users.GetByIdAsync(targetUserId, ct);
        if (target is null) return Result<RoomDto>.Fail("Target user not found.");

        var existing = await _uow.Rooms.GetDirectMessageRoomAsync(requestingUserId, targetUserId, ct);
        if (existing is not null)
            return Result<RoomDto>.Ok(await MapRoomAsync(existing, requestingUserId, ct));

        var requester = await _uow.Users.GetByIdAsync(requestingUserId, ct);
        var room = new Room { Name = $"{requester!.DisplayName},{target.DisplayName}", Type = RoomType.DirectMessage, CreatedById = requestingUserId };
        await _uow.Rooms.AddAsync(room, ct);
        await _uow.SaveChangesAsync(ct);

        _uow.Rooms.GetType(); // ensure context
        await AddMemberInternalAsync(room.Id, requestingUserId, MemberRole.Admin, ct);
        await AddMemberInternalAsync(room.Id, targetUserId, MemberRole.Admin, ct);
        await _uow.SaveChangesAsync(ct);

        var full = await _uow.Rooms.GetByIdWithMembersAsync(room.Id, ct);
        return Result<RoomDto>.Ok(await MapRoomAsync(full!, requestingUserId, ct));
    }

    public async Task<Result<RoomDto>> CreateGroupAsync(int creatorId, CreateGroupRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<RoomDto>.Fail("Group name is required.");

        var room = new Room { Name = request.Name.Trim(), Type = RoomType.Group, CreatedById = creatorId };
        await _uow.Rooms.AddAsync(room, ct);
        await _uow.SaveChangesAsync(ct);

        await AddMemberInternalAsync(room.Id, creatorId, MemberRole.Admin, ct);
        foreach (var memberId in request.MemberIds.Distinct().Where(id => id != creatorId))
            await AddMemberInternalAsync(room.Id, memberId, MemberRole.Member, ct);
        await _uow.SaveChangesAsync(ct);

        var full = await _uow.Rooms.GetByIdWithMembersAsync(room.Id, ct);
        return Result<RoomDto>.Ok(await MapRoomAsync(full!, creatorId, ct));
    }

    public async Task<Result<List<RoomDto>>> GetUserRoomsAsync(int userId, CancellationToken ct = default)
    {
        var rooms = await _uow.Rooms.GetRoomsForUserAsync(userId, ct);
        var dtos  = new List<RoomDto>();
        foreach (var r in rooms)
            dtos.Add(await MapRoomAsync(r, userId, ct));
        return Result<List<RoomDto>>.Ok(dtos);
    }

    public async Task<Result<RoomDto>> GetRoomAsync(int roomId, int requestingUserId, CancellationToken ct = default)
    {
        var room = await _uow.Rooms.GetByIdWithMembersAsync(roomId, ct);
        if (room is null) return Result<RoomDto>.Fail("Room not found.");
        if (!room.Members.Any(m => m.UserId == requestingUserId))
            return Result<RoomDto>.Fail("Access denied.");
        return Result<RoomDto>.Ok(await MapRoomAsync(room, requestingUserId, ct));
    }

    public async Task<Result> AddMemberAsync(int roomId, int requestingUserId, int newUserId, CancellationToken ct = default)
    {
        var room = await _uow.Rooms.GetByIdWithMembersAsync(roomId, ct);
        if (room is null) return Result.Fail("Room not found.");
        if (room.Type == RoomType.DirectMessage) return Result.Fail("Cannot add members to a DM.");

        var requesterMember = room.Members.FirstOrDefault(m => m.UserId == requestingUserId);
        if (requesterMember?.Role != MemberRole.Admin) return Result.Fail("Only admins can add members.");
        if (room.Members.Any(m => m.UserId == newUserId)) return Result.Fail("User already a member.");

        var user = await _uow.Users.GetByIdAsync(newUserId, ct);
        if (user is null) return Result.Fail("User not found.");

        await AddMemberInternalAsync(roomId, newUserId, MemberRole.Member, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveMemberAsync(int roomId, int requestingUserId, int targetUserId, CancellationToken ct = default)
    {
        var room = await _uow.Rooms.GetByIdWithMembersAsync(roomId, ct);
        if (room is null) return Result.Fail("Room not found.");
        if (room.Type == RoomType.DirectMessage) return Result.Fail("Cannot remove members from a DM.");

        var requesterMember = room.Members.FirstOrDefault(m => m.UserId == requestingUserId);
        if (requesterMember?.Role != MemberRole.Admin && requestingUserId != targetUserId)
            return Result.Fail("Only admins can remove members.");

        var target = room.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (target is null) return Result.Fail("User is not a member.");
        if (target.Role == MemberRole.Admin && room.CreatedById != requestingUserId)
            return Result.Fail("Cannot remove another admin.");

        _uow.Rooms.GetType(); // force context
        var ctx = GetContext();
        ctx.Set<RoomMember>().Remove(target);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteRoomAsync(int roomId, int requestingUserId, CancellationToken ct = default)
    {
        var room = await _uow.Rooms.GetByIdAsync(roomId, ct);
        if (room is null) return Result.Fail("Room not found.");
        if (room.Type == RoomType.DirectMessage) return Result.Fail("Cannot delete a DM.");
        if (room.CreatedById != requestingUserId) return Result.Fail("Only the creator can delete this group.");

        await _uow.Rooms.SoftDeleteAsync(roomId, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<RoomDto>> UpdateRoomAsync(int roomId, int requestingUserId, UpdateRoomRequest req, CancellationToken ct = default)
    {
        var room = await _uow.Rooms.GetByIdWithMembersAsync(roomId, ct);
        if (room is null) return Result<RoomDto>.Fail("Room not found.");
        var member = room.Members.FirstOrDefault(m => m.UserId == requestingUserId);
        if (member?.Role != MemberRole.Admin) return Result<RoomDto>.Fail("Only admins can rename the group.");

        room.Name      = req.Name.Trim();
        room.UpdatedAt = DateTime.UtcNow;
        await _uow.Rooms.UpdateAsync(room, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<RoomDto>.Ok(await MapRoomAsync(room, requestingUserId, ct));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task AddMemberInternalAsync(int roomId, int userId, MemberRole role, CancellationToken ct)
    {
        var ctx = GetContext();
        var already = await ctx.Set<RoomMember>().FindAsync(new object[] { userId, roomId }, ct);
        if (already is not null) return;
        ctx.Set<RoomMember>().Add(new RoomMember { UserId = userId, RoomId = roomId, Role = role });
    }

    private AppDbContext GetContext()
    {
        var field = _uow.GetType().GetField("_db", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (AppDbContext)field!.GetValue(_uow)!;
    }

    private async Task<RoomDto> MapRoomAsync(Room room, int requestingUserId, CancellationToken ct)
    {
        var last    = await _uow.Messages.GetLastMessageAsync(room.Id, ct);
        var total   = await _uow.Messages.CountByRoomAsync(room.Id, ct);

        var members = room.Members.Select(m => new MemberDto(
            m.UserId, m.User.DisplayName, m.User.AvatarColor, m.Role, m.User.IsOnline
        )).ToList();

        MessageDto? lastDto = last is null ? null : MapMessage(last, requestingUserId);

        return new RoomDto(room.Id, room.Name, room.Type, room.CreatedById, members, lastDto, 0, room.CreatedAt);
    }

    public static MessageDto MapMessage(Message m, int requestingUserId) => new(
        m.Id, m.Content, m.Type, m.IsEdited,
        m.UserId == requestingUserId,
        m.UserId, m.User.DisplayName, m.User.AvatarColor,
        m.RoomId, m.CreatedAt
    );
}
