using ChatApp.DAL.Interfaces;
using ChatApp.DAL.Repositories;

namespace ChatApp.DAL;

/// <summary>
/// Concrete Unit of Work.
/// Lazy-initialises repositories so we only create what we actually use.
/// All repositories share the same DbContext instance → one DB connection
/// per HTTP request (scoped lifetime in DI).
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    private IUserRepository? _users;

    public UnitOfWork(AppDbContext db) => _db = db;

    public IUserRepository Users => _users ??= new UserRepository(_db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
