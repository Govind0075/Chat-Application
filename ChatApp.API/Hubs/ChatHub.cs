using System.Collections.Concurrent;
using ChatApp.BLL.Interfaces;
using ChatApp.Domain.DTOs;
using ChatApp.Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.API.Hubs;

public class ChatHub(IUserService userService, IMessageService messageService,
                     IRoomService roomService, ILogger<ChatHub> log) : Hub
{
    // typing: roomId -> { userId -> displayName }
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, string>> _typing = new();

    // connectionId -> userId (fast lookup)
    private static readonly ConcurrentDictionary<string, int> _connToUser = new();

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        log.LogInformation("Disconnected: {Id}", Context.ConnectionId);
        var userResult = await userService.GetUserByConnectionIdAsync(Context.ConnectionId);
        if (userResult.IsSuccess && userResult.Value is not null)
        {
            var user = userResult.Value;
            await userService.SetOfflineAsync(Context.ConnectionId);
            _connToUser.TryRemove(Context.ConnectionId, out _);

            // Remove from all typing states
            foreach (var room in _typing.Values)
                room.TryRemove(user.Id, out _);

            // Broadcast offline status
            await Clients.All.SendAsync("UserStatus", new UserStatusPayload(user.Id, false));
        }
        await base.OnDisconnectedAsync(ex);
    }

    // ── Called once after login ───────────────────────────────────────────────
    public async Task Register(int userId)
    {
        await userService.SetOnlineAsync(userId, Context.ConnectionId);
        _connToUser[Context.ConnectionId] = userId;

        // Join a personal group so we can send targeted messages
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        // Join all room groups this user belongs to
        var roomsResult = await roomService.GetUserRoomsAsync(userId);
        if (roomsResult.IsSuccess)
        {
            foreach (var room in roomsResult.Value!)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{room.Id}");
        }

        await Clients.All.SendAsync("UserStatus", new UserStatusPayload(userId, true));
        await Clients.Caller.SendAsync("Registered", new { userId });
    }

    // ── Join a room group (called when user opens a conversation) ─────────────
    public async Task JoinRoomGroup(int roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomId}");
    }

    // ── Send message ──────────────────────────────────────────────────────────
    public async Task SendMessage(HubSendMessage payload)
    {
        if (!_connToUser.TryGetValue(Context.ConnectionId, out var userId))
        {
            await Clients.Caller.SendAsync("Error", "Not registered.");
            return;
        }

        var result = await messageService.SendAsync(userId, payload.RoomId, payload.Content);
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("Error", result.Error);
            return;
        }

        var msg = result.Value!;
        // Send to everyone in the room (including caller — caller will get IsOwn=true)
        await Clients.GroupExcept($"room_{payload.RoomId}", Context.ConnectionId)
                     .SendAsync("NewMessage", msg);
        await Clients.Caller.SendAsync("NewMessage", msg);

        // Clear typing
        TypingOff(payload.RoomId, userId);
        await BroadcastTyping(payload.RoomId);
    }

    // ── Typing ────────────────────────────────────────────────────────────────
    public async Task Typing(HubTyping payload)
    {
        if (!_connToUser.TryGetValue(Context.ConnectionId, out var userId)) return;

        var userResult = await userService.GetUserByConnectionIdAsync(Context.ConnectionId);
        var name = userResult.Value?.DisplayName ?? "Someone";

        if (payload.IsTyping)
        {
            _typing.GetOrAdd(payload.RoomId, _ => new ConcurrentDictionary<int, string>())
                   .TryAdd(userId, name);
        }
        else TypingOff(payload.RoomId, userId);

        await BroadcastTyping(payload.RoomId);
    }

    // ── Room management via Hub (for real-time side effects) ──────────────────
    public async Task NotifyMemberAdded(int roomId, int newUserId)
    {
        // Add new member's connection to the room SignalR group
        await Clients.Group($"user_{newUserId}").SendAsync("AddedToRoom", new { roomId });
    }

    public async Task NotifyMemberRemoved(int roomId, int removedUserId)
    {
        await Clients.Group($"room_{roomId}").SendAsync("MemberRemoved",
            new MemberRemovedPayload(roomId, removedUserId));
        await Clients.Group($"user_{removedUserId}").SendAsync("RemovedFromRoom", new { roomId });
    }

    public async Task NotifyRoomDeleted(int roomId)
    {
        await Clients.Group($"room_{roomId}").SendAsync("RoomDeleted",
            new RoomDeletedPayload(roomId));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static void TypingOff(int roomId, int userId)
    {
        if (_typing.TryGetValue(roomId, out var room))
            room.TryRemove(userId, out _);
    }

    private async Task BroadcastTyping(int roomId)
    {
        var names = _typing.GetValueOrDefault(roomId)?.Values.ToList() ?? [];
        await Clients.Group($"room_{roomId}")
                     .SendAsync("Typing", new TypingPayload(roomId, 0, string.Join(",", names), names.Count > 0));
    }
}
