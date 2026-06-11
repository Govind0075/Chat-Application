using ChatApp.Domain.Enums;

namespace ChatApp.Domain.DTOs;

// ── Auth ──────────────────────────────────────────────────────────────────────
public record LoginRequest(string Username);

// ── Room ──────────────────────────────────────────────────────────────────────
public record CreateGroupRequest(string Name, List<int> MemberIds);
public record AddMemberRequest(int UserId);
public record UpdateRoomRequest(string Name);

// ── Message ───────────────────────────────────────────────────────────────────
public record SendMessageRequest(string Content, int RoomId, MessageType Type = MessageType.Text);
public record EditMessageRequest(int MessageId, string NewContent);

// ── Hub payloads ──────────────────────────────────────────────────────────────
public record HubSendMessage(string Content, int RoomId);
public record HubTyping(int RoomId, bool IsTyping);

// ── Response DTOs ─────────────────────────────────────────────────────────────
public record UserDto(int Id, string Username, string DisplayName, string AvatarColor, bool IsOnline, DateTime? LastSeenAt);

public record RoomDto(
    int      Id,
    string   Name,
    RoomType Type,
    int      CreatedById,
    List<MemberDto> Members,
    MessageDto?     LastMessage,
    int      UnreadCount,
    DateTime CreatedAt
);

public record MemberDto(int UserId, string DisplayName, string AvatarColor, MemberRole Role, bool IsOnline);

public record MessageDto(
    int         Id,
    string      Content,
    MessageType Type,
    bool        IsEdited,
    bool        IsOwn,
    int         UserId,
    string      SenderName,
    string      SenderAvatar,
    int         RoomId,
    DateTime    Timestamp
);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int Total, bool HasMore);

// ── Hub broadcast payloads ────────────────────────────────────────────────────
public record TypingPayload(int RoomId, int UserId, string DisplayName, bool IsTyping);
public record UserStatusPayload(int UserId, bool IsOnline);
public record MemberRemovedPayload(int RoomId, int UserId);
public record RoomDeletedPayload(int RoomId);
