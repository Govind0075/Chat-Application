using System.Linq.Expressions;
using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.DAL.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T>    _set;

    public Repository(AppDbContext db) { _db = db; _set = db.Set<T>(); }

    public virtual Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public virtual Task<List<T>> GetAllAsync(CancellationToken ct = default)
        => _set.ToListAsync(ct);

    public virtual Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => _set.Where(predicate).ToListAsync(ct);

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        await _set.AddAsync(entity, ct);
        return entity;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public virtual async Task SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is null) return;
        entity.IsDeleted  = true;
        entity.UpdatedAt  = DateTime.UtcNow;
        _db.Entry(entity).State = EntityState.Modified;
    }

    public virtual Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => _set.AnyAsync(predicate, ct);
}
