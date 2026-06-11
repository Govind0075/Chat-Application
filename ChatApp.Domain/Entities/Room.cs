using ChatApp.Domain.Enums;

namespace ChatApp.Domain.Entities;

public class Room : BaseEntity
{
    public string   Name        { get; set; } = string.Empty;
    public RoomType Type        { get; set; } = RoomType.Group;
    public int      CreatedById { get; set; }

    public User                CreatedBy   { get; set; } = null!;
    public ICollection<RoomMember> Members { get; set; } = new List<RoomMember>();
    public ICollection<Message>    Messages{ get; set; } = new List<Message>();
}
