using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniLMS.API.Models.DTOs.Chat;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;

    public ChatController(IChatService chat) => _chat = chat;

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ────────────────────────────────────────────────────────────────────
    // POST /api/chat/send
    //
    // Saves the user message, streams AI tokens to the caller via SignalR
    // ("ReceiveChatToken"), persists the full AI response, then returns 200.
    //
    // The HTTP response is intentionally held open until the stream finishes
    // so that callers without a SignalR connection still get a deterministic
    // result. Clients that ARE connected via SignalR receive tokens in real-
    // time and the final "ChatStreamComplete" event before this 200 arrives.
    // ────────────────────────────────────────────────────────────────────
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendMessageDto dto,
        CancellationToken ct)
    {
        var ack = await _chat.SendStreamingMessageAsync(GetUserId(), dto, ct);
        return Ok(ack);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /api/chat/history/{courseId}?page=1&pageSize=50
    // ────────────────────────────────────────────────────────────────────
    [HttpGet("history/{courseId}")]
    public async Task<IActionResult> GetHistory(
        Guid courseId,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _chat.GetHistoryAsync(GetUserId(), courseId, page, pageSize);
        return Ok(result);
    }

    // ────────────────────────────────────────────────────────────────────
    // DELETE /api/chat/history/{courseId}
    // ────────────────────────────────────────────────────────────────────
    [HttpDelete("history/{courseId}")]
    public async Task<IActionResult> ClearHistory(Guid courseId)
    {
        await _chat.ClearHistoryAsync(GetUserId(), courseId);
        return NoContent();
    }
}
