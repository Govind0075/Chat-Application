using System.Linq.Expressions;
using ChatApp.Domain.Entities;

namespace ChatApp.DAL.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?>              GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<T>>         GetAllAsync(CancellationToken ct = default);
    Task<List<T>>         FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T>               AddAsync(T entity, CancellationToken ct = default);
    Task                  UpdateAsync(T entity, CancellationToken ct = default);
    Task                  SoftDeleteAsync(int id, CancellationToken ct = default);
    Task<bool>            ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?>       GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?>       GetByConnectionIdAsync(string connectionId, CancellationToken ct = default);
    Task<List<User>>  GetAllUsersAsync(CancellationToken ct = default);
}

public interface IRoomRepository : IRepository<Room>
{
    Task<Room?>       GetByIdWithMembersAsync(int roomId, CancellationToken ct = default);
    Task<List<Room>>  GetRoomsForUserAsync(int userId, CancellationToken ct = default);
    Task<Room?>       GetDirectMessageRoomAsync(int userAId, int userBId, CancellationToken ct = default);
    Task<bool>        IsUserMemberAsync(int roomId, int userId, CancellationToken ct = default);
}

public interface IMessageRepository : IRepository<Message>
{
    Task<List<Message>> GetPagedAsync(int roomId, int page, int pageSize, CancellationToken ct = default);
    Task<int>           CountByRoomAsync(int roomId, CancellationToken ct = default);
    Task<Message?>      GetByIdWithUserAsync(int messageId, CancellationToken ct = default);
    Task<Message?>      GetLastMessageAsync(int roomId, CancellationToken ct = default);
}

public interface IUserConnectionRepository : IRepository<UserConnection>
{
    Task<UserConnection?> GetActiveAsync(string connectionId, CancellationToken ct = default);
    Task<List<UserConnection>> GetActiveByUserAsync(int userId, CancellationToken ct = default);
    Task DeactivateAsync(string connectionId, CancellationToken ct = default);
}

public interface IUnitOfWork : IAsyncDisposable
{
    IUserRepository           Users           { get; }
    IRoomRepository           Rooms           { get; }
    IMessageRepository        Messages        { get; }
    IUserConnectionRepository UserConnections { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
