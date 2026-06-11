using ChatApp.BLL.Interfaces;
using ChatApp.DAL.Interfaces;
using ChatApp.Domain.Common;
using ChatApp.Domain.DTOs;
using ChatApp.Domain.Entities;
using ChatApp.Domain.Enums;

namespace ChatApp.BLL.Services;

public class MessageService : IMessageService
{
    private readonly IUnitOfWork _uow;
    public MessageService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<MessageDto>> SendAsync(int userId, int roomId, string content,
        MessageType type = MessageType.Text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Result<MessageDto>.Fail("Message cannot be empty.");
        if (content.Length > 4000)
            return Result<MessageDto>.Fail("Message too long (max 4000 chars).");

        var isMember = await _uow.Rooms.IsUserMemberAsync(roomId, userId, ct);
        if (!isMember) return Result<MessageDto>.Fail("You are not a member of this room.");

        var msg = new Message { Content = content.Trim(), Type = type, UserId = userId, RoomId = roomId };
        await _uow.Messages.AddAsync(msg, ct);
        await _uow.SaveChangesAsync(ct);

        var saved = await _uow.Messages.GetByIdWithUserAsync(msg.Id, ct);
        return Result<MessageDto>.Ok(RoomService.MapMessage(saved!, userId));
    }

    public async Task<Result<PagedResult<MessageDto>>> GetHistoryAsync(
        int roomId, int userId, int page, int pageSize, CancellationToken ct = default)
    {
        var isMember = await _uow.Rooms.IsUserMemberAsync(roomId, userId, ct);
        if (!isMember) return Result<PagedResult<MessageDto>>.Fail("Access denied.");

        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var messages = await _uow.Messages.GetPagedAsync(roomId, page, pageSize, ct);
        var total    = await _uow.Messages.CountByRoomAsync(roomId, ct);
        var dtos     = messages.Select(m => RoomService.MapMessage(m, userId)).ToList();

        return Result<PagedResult<MessageDto>>.Ok(
            new PagedResult<MessageDto>(dtos, page, pageSize, total, page * pageSize < total));
    }

    public async Task<Result> EditAsync(int messageId, int userId, string newContent, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newContent))
            return Result.Fail("Content cannot be empty.");

        var msg = await _uow.Messages.GetByIdAsync(messageId, ct);
        if (msg is null)       return Result.Fail("Message not found.");
        if (msg.UserId != userId) return Result.Fail("Cannot edit another user's message.");
        if (msg.Type == MessageType.System) return Result.Fail("Cannot edit system messages.");

        msg.Content   = newContent.Trim();
        msg.IsEdited  = true;
        msg.UpdatedAt = DateTime.UtcNow;
        await _uow.Messages.UpdateAsync(msg, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int messageId, int userId, CancellationToken ct = default)
    {
        var msg = await _uow.Messages.GetByIdAsync(messageId, ct);
        if (msg is null)          return Result.Fail("Message not found.");
        if (msg.UserId != userId) return Result.Fail("Cannot delete another user's message.");

        await _uow.Messages.SoftDeleteAsync(messageId, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
