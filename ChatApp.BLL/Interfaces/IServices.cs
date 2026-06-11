using ChatApp.Domain.Common;
using ChatApp.Domain.DTOs;
using ChatApp.Domain.Entities;

namespace ChatApp.BLL.Interfaces;

public interface IUserService
{
    Task<Result<UserDto>>       LoginAsync(string username, CancellationToken ct = default);
    Task<Result<UserDto>>       GetByIdAsync(int userId, CancellationToken ct = default);
    Task<Result<List<UserDto>>> GetAllUsersAsync(CancellationToken ct = default);
    Task<Result>                SetOnlineAsync(int userId, string connectionId, CancellationToken ct = default);
    Task<Result>                SetOfflineAsync(string connectionId, CancellationToken ct = default);
    Task<Result<User?>>         GetUserByConnectionIdAsync(string connectionId, CancellationToken ct = default);
}

public interface IRoomService
{
    Task<Result<RoomDto>>       GetOrCreateDmAsync(int requestingUserId, int targetUserId, CancellationToken ct = default);
    Task<Result<RoomDto>>       CreateGroupAsync(int creatorId, CreateGroupRequest request, CancellationToken ct = default);
    Task<Result<List<RoomDto>>> GetUserRoomsAsync(int userId, CancellationToken ct = default);
    Task<Result<RoomDto>>       GetRoomAsync(int roomId, int requestingUserId, CancellationToken ct = default);
    Task<Result>                AddMemberAsync(int roomId, int requestingUserId, int newUserId, CancellationToken ct = default);
    Task<Result>                RemoveMemberAsync(int roomId, int requestingUserId, int targetUserId, CancellationToken ct = default);
    Task<Result>                DeleteRoomAsync(int roomId, int requestingUserId, CancellationToken ct = default);
    Task<Result<RoomDto>>       UpdateRoomAsync(int roomId, int requestingUserId, UpdateRoomRequest req, CancellationToken ct = default);
}

public interface IMessageService
{
    Task<Result<MessageDto>>         SendAsync(int userId, int roomId, string content, Domain.Enums.MessageType type = Domain.Enums.MessageType.Text, CancellationToken ct = default);
    Task<Result<PagedResult<MessageDto>>> GetHistoryAsync(int roomId, int userId, int page, int pageSize, CancellationToken ct = default);
    Task<Result>                     EditAsync(int messageId, int userId, string newContent, CancellationToken ct = default);
    Task<Result>                     DeleteAsync(int messageId, int userId, CancellationToken ct = default);
}
