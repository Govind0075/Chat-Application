using ChatApp.Domain.Entities;

namespace ChatApp.DAL.Interfaces;

/// <summary>
/// User-specific data access operations.
/// We don't use a generic repository here yet — keeps things explicit and
/// easy to understand. We'll introduce the generic pattern in a later phase.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByRefreshTokenAsync(string refreshToken);
    Task<bool>  EmailExistsAsync(string email);
    Task<bool>  UsernameExistsAsync(string username);
    Task        AddAsync(User user);
    void        Update(User user);
}

/// <summary>
/// Unit of Work — wraps all repositories and a single SaveChanges call.
/// This ensures all DB operations in one business action are atomic
/// (either all succeed or all roll back).
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IUserRepository Users { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
