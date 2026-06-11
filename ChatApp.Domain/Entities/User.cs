namespace ChatApp.Domain.Entities;

public class User : BaseEntity
{
    public string Username    { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#6366f1";
    public bool   IsOnline    { get; set; } = false;
    public DateTime? LastSeenAt { get; set; }

    public ICollection<RoomMember>     RoomMemberships { get; set; } = new List<RoomMember>();
    public ICollection<Message>        Messages        { get; set; } = new List<Message>();
    public ICollection<UserConnection> Connections     { get; set; } = new List<UserConnection>();
}
