using ChatApp.BLL.Services;
using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Entities;
using Moq;

namespace ChatApp.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUnitOfWork>           _uow   = new();
    private readonly Mock<IUserRepository>       _users = new();
    private readonly Mock<IUserConnectionRepository> _conns = new();
    private readonly UserService                 _sut;

    public UserServiceTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _uow.Setup(u => u.UserConnections).Returns(_conns.Object);
        _sut = new UserService(_uow.Object);
    }

    // ── LoginAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_EmptyUsername_ReturnsFail()
    {
        var result = await _sut.LoginAsync("   ");

        Assert.False(result.IsSuccess);
        Assert.Contains("1–50 characters", result.Error);
    }

    [Fact]
    public async Task LoginAsync_UsernameTooLong_ReturnsFail()
    {
        var result = await _sut.LoginAsync(new string('a', 51));

        Assert.False(result.IsSuccess);
        Assert.Contains("1–50 characters", result.Error);
    }

    [Fact]
    public async Task LoginAsync_NewUser_CreatesUserAndReturnsDto()
    {
        _users.Setup(r => r.GetByUsernameAsync("alice", default)).ReturnsAsync((User?)null);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), default))
              .ReturnsAsync((User u, CancellationToken _) => { u.Id = 1; return u; });
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.LoginAsync("alice");

        Assert.True(result.IsSuccess);
        Assert.Equal("alice", result.Value!.Username);
        _users.Verify(r => r.AddAsync(It.Is<User>(u => u.Username == "alice"), default), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ExistingUser_ReturnsExistingUserDto()
    {
        var existing = new User { Id = 5, Username = "bob", DisplayName = "bob", AvatarColor = "#fff" };
        _users.Setup(r => r.GetByUsernameAsync("bob", default)).ReturnsAsync(existing);

        var result = await _sut.LoginAsync("bob");

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Id);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_TrimsWhitespace_BeforeValidating()
    {
        var existing = new User { Id = 2, Username = "carol", DisplayName = "carol", AvatarColor = "#abc" };
        _users.Setup(r => r.GetByUsernameAsync("carol", default)).ReturnsAsync(existing);

        var result = await _sut.LoginAsync("  carol  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("carol", result.Value!.Username);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_UserNotFound_ReturnsFail()
    {
        _users.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((User?)null);

        var result = await _sut.GetByIdAsync(99);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task GetByIdAsync_UserFound_ReturnsDto()
    {
        var user = new User { Id = 3, Username = "dave", DisplayName = "dave", AvatarColor = "#111" };
        _users.Setup(r => r.GetByIdAsync(3, default)).ReturnsAsync(user);

        var result = await _sut.GetByIdAsync(3);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Id);
    }

    // ── SetOnlineAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task SetOnlineAsync_UserNotFound_ReturnsFail()
    {
        _users.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((User?)null);

        var result = await _sut.SetOnlineAsync(99, "conn-1");

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found.", result.Error);
    }

    [Fact]
    public async Task SetOnlineAsync_UserFound_SetsOnlineAndSavesConnection()
    {
        var user = new User { Id = 1, Username = "eve", DisplayName = "eve", AvatarColor = "#222" };
        _users.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(user);
        _users.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);
        _conns.Setup(r => r.AddAsync(It.IsAny<UserConnection>(), default))
              .ReturnsAsync((UserConnection c, CancellationToken _) => c);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.SetOnlineAsync(1, "conn-1");

        Assert.True(result.IsSuccess);
        Assert.True(user.IsOnline);
        _conns.Verify(r => r.AddAsync(It.Is<UserConnection>(c => c.ConnectionId == "conn-1"), default), Times.Once);
    }

    // ── SetOfflineAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetOfflineAsync_NoRemainingConnections_SetsUserOffline()
    {
        var user = new User { Id = 1, Username = "frank", DisplayName = "frank", AvatarColor = "#333", IsOnline = true };
        _conns.Setup(r => r.DeactivateAsync("conn-1", default)).Returns(Task.CompletedTask);
        _users.Setup(r => r.GetByConnectionIdAsync("conn-1", default)).ReturnsAsync(user);
        _conns.Setup(r => r.GetActiveByUserAsync(1, default)).ReturnsAsync(new List<UserConnection>());
        _users.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.SetOfflineAsync("conn-1");

        Assert.True(result.IsSuccess);
        Assert.False(user.IsOnline);
        Assert.NotNull(user.LastSeenAt);
    }

    [Fact]
    public async Task SetOfflineAsync_RemainingConnectionsExist_KeepsUserOnline()
    {
        var user = new User { Id = 1, Username = "grace", DisplayName = "grace", AvatarColor = "#444", IsOnline = true };
        _conns.Setup(r => r.DeactivateAsync("conn-1", default)).Returns(Task.CompletedTask);
        _users.Setup(r => r.GetByConnectionIdAsync("conn-1", default)).ReturnsAsync(user);
        _conns.Setup(r => r.GetActiveByUserAsync(1, default))
              .ReturnsAsync(new List<UserConnection> { new() { ConnectionId = "conn-2" } });
        _uow.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await _sut.SetOfflineAsync("conn-1");

        Assert.True(user.IsOnline);
        _users.Verify(r => r.UpdateAsync(It.IsAny<User>(), default), Times.Never);
    }

    // ── GetAllUsersAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllMappedUsers()
    {
        var users = new List<User>
        {
            new() { Id = 1, Username = "a", DisplayName = "a", AvatarColor = "#111" },
            new() { Id = 2, Username = "b", DisplayName = "b", AvatarColor = "#222" },
        };
        _users.Setup(r => r.GetAllUsersAsync(default)).ReturnsAsync(users);

        var result = await _sut.GetAllUsersAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }
}
