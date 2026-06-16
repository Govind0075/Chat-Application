using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.DAL.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id) =>
        _db.Users.FindAsync(id).AsTask();

    // Normalise email to lowercase so lookups are case-insensitive
    public Task<User?> GetByEmailAsync(string email) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());

    public Task<User?> GetByRefreshTokenAsync(string refreshToken) =>
        _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

    public Task<bool> EmailExistsAsync(string email) =>
        _db.Users.AnyAsync(u => u.Email == email.ToLower());

    public Task<bool> UsernameExistsAsync(string username) =>
        _db.Users.AnyAsync(u => u.Username == username);

    public async Task AddAsync(User user) =>
        await _db.Users.AddAsync(user);

    public void Update(User user) =>
        _db.Users.Update(user);
}
