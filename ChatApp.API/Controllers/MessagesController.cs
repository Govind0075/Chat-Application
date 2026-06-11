using ChatApp.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController(IMessageService messageService) : ControllerBase
{
    private int UserId => int.Parse(Request.Headers["X-User-Id"].FirstOrDefault() ?? "0");

    [HttpGet("room/{roomId:int}")]
    public async Task<IActionResult> GetHistory(
        int roomId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var r = await messageService.GetHistoryAsync(roomId, UserId, page, pageSize, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{messageId:int}")]
    public async Task<IActionResult> Edit(int messageId, [FromBody] string newContent, CancellationToken ct)
    {
        var r = await messageService.EditAsync(messageId, UserId, newContent, ct);
        return r.IsSuccess ? Ok() : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{messageId:int}")]
    public async Task<IActionResult> Delete(int messageId, CancellationToken ct)
    {
        var r = await messageService.DeleteAsync(messageId, UserId, ct);
        return r.IsSuccess ? Ok() : BadRequest(new { error = r.Error });
    }
}
