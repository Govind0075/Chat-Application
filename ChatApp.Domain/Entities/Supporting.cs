using ChatApp.Domain.Enums;

namespace ChatApp.Domain.Entities;

public class RoomMember
{
    public int      UserId   { get; set; }
    public int      RoomId   { get; set; }
    public MemberRole Role   { get; set; } = MemberRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Room Room { get; set; } = null!;
}

public class Message : BaseEntity
{
    public string      Content     { get; set; } = string.Empty;
    public MessageType Type        { get; set; } = MessageType.Text;
    public bool        IsEdited    { get; set; } = false;
    public int         UserId      { get; set; }
    public int         RoomId      { get; set; }

    public User User { get; set; } = null!;
    public Room Room { get; set; } = null!;
}

public class UserConnection : BaseEntity
{
    public int      UserId       { get; set; }
    public string   ConnectionId { get; set; } = string.Empty;
    public bool     IsActive     { get; set; } = true;
    public DateTime ConnectedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? DisconnectedAt { get; set; }

    public User User { get; set; } = null!;
}
