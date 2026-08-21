using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UniLMS.API.Models.DTOs.Chat;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chat;

    public ChatHub(IChatService chat) => _chat = chat;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Client joins a course room to receive future upload/notification events.
    /// </summary>
    public async Task JoinCourseRoom(string courseId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"course_{courseId}");

    public async Task LeaveCourseRoom(string courseId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"course_{courseId}");

    /// <summary>
    /// Real-time chat entry point. Tokens stream back via "ReceiveChatToken";
    /// the finished message is signalled with "ChatStreamComplete".
    /// Errors are signalled with "ChatStreamError".
    /// </summary>
    public async Task SendMessage(SendMessageDto dto)
    {
        var userId = Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Use the hub connection's abort token so the stream is cancelled if
        // the client disconnects mid-generation.
        await _chat.SendStreamingMessageAsync(userId, dto, Context.ConnectionAborted);
    }
}

/// <summary>
/// Hub for pushing administrative notifications (new uploads, announcements, etc.)
/// to connected students without going through the chat flow.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        await base.OnConnectedAsync();
    }
}
