using System.ComponentModel.DataAnnotations;

namespace UniLMS.API.Models.DTOs.Chat;

// ── Inbound ──────────────────────────────────────────────────────────────────

public class SendMessageDto
{
    [Required]
    public Guid CourseId { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;
}

// ── Outbound (HTTP) ───────────────────────────────────────────────────────────

/// <summary>
/// Returned immediately by POST /api/chat/send after the full stream
/// has been consumed and persisted. Clients receive the actual tokens
/// incrementally via SignalR ("ReceiveChatToken").
/// </summary>
public class SendMessageAckDto
{
    public Guid UserMessageId { get; set; }
    public Guid CourseId      { get; set; }
    public string Message     { get; set; } = "Message processed and streamed successfully.";
}

// ── Chat history (REST) ───────────────────────────────────────────────────────

public class ChatMessageResponseDto
{
    public Guid     Id          { get; set; }
    public string   Role        { get; set; } = string.Empty; // "User" | "Bot"
    public string   MessageText { get; set; } = string.Empty;
    public DateTime Timestamp   { get; set; }
}

public class ChatHistoryResponseDto
{
    public Guid                          CourseId    { get; set; }
    public string                        CourseTitle { get; set; } = string.Empty;
    public List<ChatMessageResponseDto>  Messages    { get; set; } = new();
}

// ── AI engine wire types ──────────────────────────────────────────────────────

/// <summary>One entry in the conversation history sent to the AI engine.</summary>
public class AiChatHistoryItem
{
    public string Role    { get; set; } = string.Empty; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
}

// ── SignalR events (documentation only — sent as anonymous objects) ───────────
//
//  "ReceiveChatToken"   → string token
//  "ChatStreamComplete" → { messageId, courseId, role, timestamp }
//  "ChatStreamError"    → { error }
