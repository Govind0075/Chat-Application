using ChatApp.BLL.Services;
using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Entities;
using ChatApp.Domain.Enums;
using Moq;

namespace ChatApp.Tests.Services;

public class MessageServiceTests
{
    private readonly Mock<IUnitOfWork>         _uow      = new();
    private readonly Mock<IRoomRepository>     _rooms    = new();
    private readonly Mock<IMessageRepository>  _messages = new();
    private readonly MessageService            _sut;

    public MessageServiceTests()
    {
        _uow.Setup(u => u.Rooms).Returns(_rooms.Object);
        _uow.Setup(u => u.Messages).Returns(_messages.Object);
        _sut = new MessageService(_uow.Object);
    }

    // ── SendAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_EmptyContent_ReturnsFail()
    {
        var result = await _sut.SendAsync(1, 1, "   ");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error);
    }

    [Fact]
    public async Task SendAsync_ContentTooLong_ReturnsFail()
    {
        var result = await _sut.SendAsync(1, 1, new string('x', 4001));

        Assert.False(result.IsSuccess);
        Assert.Contains("4000", result.Error);
    }

    [Fact]
    public async Task SendAsync_UserNotMember_ReturnsFail()
    {
        _rooms.Setup(r => r.IsUserMemberAsync(1, 1, default)).ReturnsAsync(false);

        var result = await _sut.SendAsync(1, 1, "Hello");

        Assert.False(result.IsSuccess);
        Assert.Contains("not a member", result.Error);
    }

    [Fact]
    public async Task SendAsync_ValidMessage_ReturnsMessageDto()
    {
        var sender = new User { Id = 1, Username = "alice", DisplayName = "Alice", AvatarColor = "#fff" };
        var saved  = new Message
        {
            Id = 10, Content = "Hello", Type = MessageType.Text,
            UserId = 1, RoomId = 1, User = sender
        };

        _rooms.Setup(r => r.IsUserMemberAsync(1, 1, default)).ReturnsAsync(true);
        _messages.Setup(r => r.AddAsync(It.IsAny<Message>(), default))
                 .ReturnsAsync((Message m, CancellationToken _) => { m.Id = 10; return m; });
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        _messages.Setup(r => r.GetByIdWithUserAsync(10, default)).ReturnsAsync(saved);

        var result = await _sut.SendAsync(1, 1, "Hello");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello", result.Value!.Content);
        Assert.Equal(1, result.Value.UserId);
    }

    // ── EditAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task EditAsync_EmptyContent_ReturnsFail()
    {
        var result = await _sut.EditAsync(1, 1, "  ");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error);
    }

    [Fact]
    public async Task EditAsync_MessageNotFound_ReturnsFail()
    {
        _messages.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Message?)null);

        var result = await _sut.EditAsync(99, 1, "Updated");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task EditAsync_NotOwner_ReturnsFail()
    {
        var msg = new Message { Id = 1, UserId = 2, Content = "Original", Type = MessageType.Text };
        _messages.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(msg);

        var result = await _sut.EditAsync(1, userId: 99, "Updated");

        Assert.False(result.IsSuccess);
        Assert.Contains("another user", result.Error);
    }

    [Fact]
    public async Task EditAsync_SystemMessage_ReturnsFail()
    {
        var msg = new Message { Id = 1, UserId = 1, Content = "System event", Type = MessageType.System };
        _messages.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(msg);

        var result = await _sut.EditAsync(1, userId: 1, "Updated");

        Assert.False(result.IsSuccess);
        Assert.Contains("system", result.Error);
    }

    [Fact]
    public async Task EditAsync_ValidEdit_UpdatesMessageAndReturnsOk()
    {
        var msg = new Message { Id = 1, UserId = 1, Content = "Old", Type = MessageType.Text };
        _messages.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(msg);
        _messages.Setup(r => r.UpdateAsync(msg, default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.EditAsync(1, userId: 1, "New content");

        Assert.True(result.IsSuccess);
        Assert.Equal("New content", msg.Content);
        Assert.True(msg.IsEdited);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_MessageNotFound_ReturnsFail()
    {
        _messages.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Message?)null);

        var result = await _sut.DeleteAsync(99, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task DeleteAsync_NotOwner_ReturnsFail()
    {
        var msg = new Message { Id = 1, UserId = 2, Content = "Hello", Type = MessageType.Text };
        _messages.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(msg);

        var result = await _sut.DeleteAsync(1, userId: 99);

        Assert.False(result.IsSuccess);
        Assert.Contains("another user", result.Error);
    }

    [Fact]
    public async Task DeleteAsync_OwnMessage_ReturnsOk()
    {
        var msg = new Message { Id = 1, UserId = 1, Content = "Bye", Type = MessageType.Text };
        _messages.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(msg);
        _messages.Setup(r => r.SoftDeleteAsync(1, default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.DeleteAsync(1, userId: 1);

        Assert.True(result.IsSuccess);
        _messages.Verify(r => r.SoftDeleteAsync(1, default), Times.Once);
    }

    // ── GetHistoryAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistoryAsync_NotMember_ReturnsFail()
    {
        _rooms.Setup(r => r.IsUserMemberAsync(1, 99, default)).ReturnsAsync(false);

        var result = await _sut.GetHistoryAsync(roomId: 1, userId: 99, page: 1, pageSize: 20);

        Assert.False(result.IsSuccess);
        Assert.Contains("denied", result.Error);
    }

    [Fact]
    public async Task GetHistoryAsync_ValidRequest_ReturnsPaged()
    {
        var sender = new User { Id = 1, Username = "alice", DisplayName = "Alice", AvatarColor = "#fff" };
        var msgs = new List<Message>
        {
            new() { Id = 1, Content = "Hi",    UserId = 1, RoomId = 1, Type = MessageType.Text, User = sender },
            new() { Id = 2, Content = "Hello", UserId = 1, RoomId = 1, Type = MessageType.Text, User = sender },
        };

        _rooms.Setup(r => r.IsUserMemberAsync(1, 1, default)).ReturnsAsync(true);
        _messages.Setup(r => r.GetPagedAsync(1, 1, 20, default)).ReturnsAsync(msgs);
        _messages.Setup(r => r.CountByRoomAsync(1, default)).ReturnsAsync(2);

        var result = await _sut.GetHistoryAsync(roomId: 1, userId: 1, page: 1, pageSize: 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal(2, result.Value.Total);
        Assert.False(result.Value.HasMore);
    }

    [Theory]
    [InlineData(0,  1,  1)]   // page below 1 → clamped to 1
    [InlineData(1,  0,  1)]   // pageSize below 1 → clamped to 1
    [InlineData(1, 200, 100)] // pageSize above 100 → clamped to 100
    public async Task GetHistoryAsync_ClampsPageAndPageSize(int page, int pageSize, int expectedPageSize)
    {
        _rooms.Setup(r => r.IsUserMemberAsync(It.IsAny<int>(), It.IsAny<int>(), default)).ReturnsAsync(true);
        _messages.Setup(r => r.GetPagedAsync(1, It.IsAny<int>(), expectedPageSize, default)).ReturnsAsync(new List<Message>());
        _messages.Setup(r => r.CountByRoomAsync(1, default)).ReturnsAsync(0);

        var result = await _sut.GetHistoryAsync(1, 1, page, pageSize);

        Assert.True(result.IsSuccess);
        _messages.Verify(r => r.GetPagedAsync(1, It.IsAny<int>(), expectedPageSize, default), Times.Once);
    }
}
