using ChatApp.DAL.Interfaces;
using ChatApp.DAL.Repositories;

namespace ChatApp.DAL;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    private IUserRepository?           _users;
    private IRoomRepository?           _rooms;
    private IMessageRepository?        _messages;
    private IUserConnectionRepository? _connections;

    public UnitOfWork(AppDbContext db) => _db = db;

    public IUserRepository           Users           => _users        ??= new UserRepository(_db);
    public IRoomRepository           Rooms           => _rooms        ??= new RoomRepository(_db);
    public IMessageRepository        Messages        => _messages     ??= new MessageRepository(_db);
    public IUserConnectionRepository UserConnections => _connections  ??= new UserConnectionRepository(_db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
