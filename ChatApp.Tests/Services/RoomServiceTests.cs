using ChatApp.BLL.Services;
using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Entities;
using ChatApp.Domain.Enums;
using Moq;

namespace ChatApp.Tests.Services;

/// <summary>
/// Tests for all guard-clause failures in RoomService.
/// Success paths that build/mutate rooms are covered by integration tests
/// (they require a real DbContext due to RoomService's internal reflection on IUnitOfWork).
/// </summary>
public class RoomServiceTests
{
    private readonly Mock<IUnitOfWork>      _uow   = new();
    private readonly Mock<IUserRepository>  _users = new();
    private readonly Mock<IRoomRepository>  _rooms = new();
    private readonly RoomService            _sut;

    public RoomServiceTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _uow.Setup(u => u.Rooms).Returns(_rooms.Object);
        _sut = new RoomService(_uow.Object);
    }

    // ── GetOrCreateDmAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateDmAsync_SameUserId_ReturnsFail()
    {
        var result = await _sut.GetOrCreateDmAsync(1, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("yourself", result.Error);
    }

    [Fact]
    public async Task GetOrCreateDmAsync_TargetUserNotFound_ReturnsFail()
    {
        _users.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((User?)null);

        var result = await _sut.GetOrCreateDmAsync(1, 99);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    // ── CreateGroupAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGroupAsync_EmptyName_ReturnsFail()
    {
        var result = await _sut.CreateGroupAsync(1, new("  ", new List<int>()));

        Assert.False(result.IsSuccess);
        Assert.Contains("required", result.Error);
    }

    [Fact]
    public async Task CreateGroupAsync_WhitespaceName_ReturnsFail()
    {
        var result = await _sut.CreateGroupAsync(1, new("\t\n", new List<int>()));

        Assert.False(result.IsSuccess);
    }

    // ── GetRoomAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoomAsync_RoomNotFound_ReturnsFail()
    {
        _rooms.Setup(r => r.GetByIdWithMembersAsync(99, default)).ReturnsAsync((Room?)null);

        var result = await _sut.GetRoomAsync(99, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task GetRoomAsync_UserNotMember_ReturnsFail()
    {
        var room = new Room
        {
            Id = 1, Name = "Test", Type = RoomType.Group, CreatedById = 2,
            Members = new List<RoomMember> { new() { UserId = 2 } }
        };
        _rooms.Setup(r => r.GetByIdWithMembersAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.GetRoomAsync(1, requestingUserId: 99);

        Assert.False(result.IsSuccess);
        Assert.Contains("Access denied", result.Error);
    }

    // ── AddMemberAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddMemberAsync_RoomNotFound_ReturnsFail()
    {
        _rooms.Setup(r => r.GetByIdWithMembersAsync(99, default)).ReturnsAsync((Room?)null);

        var result = await _sut.AddMemberAsync(99, 1, 2);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_DmRoom_ReturnsFail()
    {
        var room = new Room { Id = 1, Type = RoomType.DirectMessage, Members = new List<RoomMember>() };
        _rooms.Setup(r => r.GetByIdWithMembersAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.AddMemberAsync(1, 1, 2);

        Assert.False(result.IsSuccess);
        Assert.Contains("DM", result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_RequesterNotAdmin_ReturnsFail()
    {
        var room = new Room
        {
            Id = 1, Type = RoomType.Group,
            Members = new List<RoomMember>
            {
                new() { UserId = 1, Role = MemberRole.Member }
            }
        };
        _rooms.Setup(r => r.GetByIdWithMembersAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.AddMemberAsync(1, requestingUserId: 1, newUserId: 2);

        Assert.False(result.IsSuccess);
        Assert.Contains("admins", result.Error);
    }

    [Fact]
    public async Task AddMemberAsync_UserAlreadyMember_ReturnsFail()
    {
        var room = new Room
        {
            Id = 1, Type = RoomType.Group,
            Members = new List<RoomMember>
            {
                new() { UserId = 1, Role = MemberRole.Admin },
                new() { UserId = 2, Role = MemberRole.Member }
            }
        };
        _rooms.Setup(r => r.GetByIdWithMembersAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.AddMemberAsync(1, requestingUserId: 1, newUserId: 2);

        Assert.False(result.IsSuccess);
        Assert.Contains("already a member", result.Error);
    }

    // ── RemoveMemberAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveMemberAsync_RoomNotFound_ReturnsFail()
    {
        _rooms.Setup(r => r.GetByIdWithMembersAsync(99, default)).ReturnsAsync((Room?)null);

        var result = await _sut.RemoveMemberAsync(99, 1, 2);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task RemoveMemberAsync_DmRoom_ReturnsFail()
    {
        var room = new Room { Id = 1, Type = RoomType.DirectMessage, Members = new List<RoomMember>() };
        _rooms.Setup(r => r.GetByIdWithMembersAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.RemoveMemberAsync(1, 1, 2);

        Assert.False(result.IsSuccess);
        Assert.Contains("DM", result.Error);
    }

    [Fact]
    public async Task RemoveMemberAsync_NonAdminRemovingOther_ReturnsFail()
    {
        var room = new Room
        {
            Id = 1, Type = RoomType.Group,
            Members = new List<RoomMember>
            {
                new() { UserId = 1, Role = MemberRole.Member },
                new() { UserId = 2, Role = MemberRole.Member }
            }
        };
        _rooms.Setup(r => r.GetByIdWithMembersAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.RemoveMemberAsync(1, requestingUserId: 1, targetUserId: 2);

        Assert.False(result.IsSuccess);
        Assert.Contains("admins", result.Error);
    }

    [Fact]
    public async Task RemoveMemberAsync_TargetNotMember_ReturnsFail()
    {
        var room = new Room
        {
            Id = 1, Type = RoomType.Group,
            Members = new List<RoomMember>
            {
                new() { UserId = 1, Role = MemberRole.Admin }
            }
        };
        _rooms.Setup(r => r.GetByIdWithMembersAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.RemoveMemberAsync(1, requestingUserId: 1, targetUserId: 99);

        Assert.False(result.IsSuccess);
        Assert.Contains("not a member", result.Error);
    }

    // ── DeleteRoomAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRoomAsync_RoomNotFound_ReturnsFail()
    {
        _rooms.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Room?)null);

        var result = await _sut.DeleteRoomAsync(99, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task DeleteRoomAsync_DmRoom_ReturnsFail()
    {
        var room = new Room { Id = 1, Type = RoomType.DirectMessage, CreatedById = 1 };
        _rooms.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.DeleteRoomAsync(1, requestingUserId: 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("DM", result.Error);
    }

    [Fact]
    public async Task DeleteRoomAsync_NotCreator_ReturnsFail()
    {
        var room = new Room { Id = 1, Type = RoomType.Group, CreatedById = 2 };
        _rooms.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.DeleteRoomAsync(1, requestingUserId: 99);

        Assert.False(result.IsSuccess);
        Assert.Contains("creator", result.Error);
    }

    // ── UpdateRoomAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateRoomAsync_RoomNotFound_ReturnsFail()
    {
        _rooms.Setup(r => r.GetByIdWithMembersAsync(99, default)).ReturnsAsync((Room?)null);

        var result = await _sut.UpdateRoomAsync(99, 1, new("New Name"));

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task UpdateRoomAsync_RequesterNotAdmin_ReturnsFail()
    {
        var room = new Room
        {
            Id = 1, Name = "Old", Type = RoomType.Group,
            Members = new List<RoomMember> { new() { UserId = 1, Role = MemberRole.Member } }
        };
        _rooms.Setup(r => r.GetByIdWithMembersAsync(1, default)).ReturnsAsync(room);

        var result = await _sut.UpdateRoomAsync(1, requestingUserId: 1, new("New Name"));

        Assert.False(result.IsSuccess);
        Assert.Contains("admins", result.Error);
    }
}
