using ChatApp.BLL.Interfaces;
using ChatApp.Domain.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController(IRoomService roomService) : ControllerBase
{
    private int UserId => int.Parse(Request.Headers["X-User-Id"].FirstOrDefault() ?? "0");

    [HttpGet]
    public async Task<IActionResult> GetMyRooms(CancellationToken ct)
    {
        var r = await roomService.GetUserRoomsAsync(UserId, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpGet("{roomId:int}")]
    public async Task<IActionResult> GetRoom(int roomId, CancellationToken ct)
    {
        var r = await roomService.GetRoomAsync(roomId, UserId, ct);
        return r.IsSuccess ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost("dm/{targetUserId:int}")]
    public async Task<IActionResult> OpenDm(int targetUserId, CancellationToken ct)
    {
        var r = await roomService.GetOrCreateDmAsync(UserId, targetUserId, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("group")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req, CancellationToken ct)
    {
        var r = await roomService.CreateGroupAsync(UserId, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("{roomId:int}/members")]
    public async Task<IActionResult> AddMember(int roomId, [FromBody] AddMemberRequest req, CancellationToken ct)
    {
        var r = await roomService.AddMemberAsync(roomId, UserId, req.UserId, ct);
        return r.IsSuccess ? Ok() : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{roomId:int}/members/{targetUserId:int}")]
    public async Task<IActionResult> RemoveMember(int roomId, int targetUserId, CancellationToken ct)
    {
        var r = await roomService.RemoveMemberAsync(roomId, UserId, targetUserId, ct);
        return r.IsSuccess ? Ok() : BadRequest(new { error = r.Error });
    }

    [HttpPut("{roomId:int}")]
    public async Task<IActionResult> UpdateRoom(int roomId, [FromBody] UpdateRoomRequest req, CancellationToken ct)
    {
        var r = await roomService.UpdateRoomAsync(roomId, UserId, req, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{roomId:int}")]
    public async Task<IActionResult> DeleteRoom(int roomId, CancellationToken ct)
    {
        var r = await roomService.DeleteRoomAsync(roomId, UserId, ct);
        return r.IsSuccess ? Ok() : BadRequest(new { error = r.Error });
    }
}
