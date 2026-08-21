using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UniLMS.API.Data;
using UniLMS.API.Hubs;
using UniLMS.API.Models.DTOs.Chat;
using UniLMS.API.Models.Entities;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Services.Implementations;

public class ChatService : IChatService
{
    private readonly AppDbContext         _db;
    private readonly HttpClient           _aiClient;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        AppDbContext db,
        IHttpClientFactory httpFactory,
        IHubContext<ChatHub> hub,
        ILogger<ChatService> logger)
    {
        _db       = db;
        _aiClient = httpFactory.CreateClient("AiService");
        _hub      = hub;
        _logger   = logger;
    }

    // ────────────────────────────────────────────────────────────────────────
    // POST /api/chat/send  (also called from ChatHub.SendMessage)
    // ────────────────────────────────────────────────────────────────────────
    public async Task<SendMessageAckDto> SendStreamingMessageAsync(
        Guid userId, SendMessageDto dto, CancellationToken ct = default)
    {
        // ── Step 1: Map Course ID & Persist User Message ─────────────────────
        var course = await _db.Courses
            .AsNoTracking()
            .Select(c => new { c.Id, c.Code })
            .FirstOrDefaultAsync(c => c.Id == dto.CourseId, ct)
            ?? throw new KeyNotFoundException("Course not found.");

        var userMessage = new ChatMessage
        {
            UserId      = userId,
            CourseId    = dto.CourseId,
            Role        = MessageRole.User,
            MessageText = dto.Message,
            Timestamp   = DateTime.UtcNow
        };

        _db.ChatMessages.Add(userMessage);
        await _db.SaveChangesAsync(ct);

        // ── Step 2: Fetch & Format Last 10 Messages as AI Engine History ─────
        // The slice is taken BEFORE we saved the current user message so that
        // the history contains up to 10 prior turns, not the turn being sent.
        var history = await _db.ChatMessages
            .Where(m => m.UserId == userId
                     && m.CourseId == dto.CourseId
                     && m.Id      != userMessage.Id)   // exclude the just-saved message
            .OrderByDescending(m => m.Timestamp)
            .Take(10)
            .OrderBy(m => m.Timestamp)
            .Select(m => new AiChatHistoryItem
            {
                Role    = m.Role == MessageRole.User ? "user" : "assistant",
                Content = m.MessageText
            })
            .ToListAsync(ct);

        // ── Step 3: Open SSE Stream to AI Engine & Pipe Tokens via SignalR ───
        var aiPayload = new
        {
            course_id = course.Code,   // alphanumeric course code, e.g. "IS453"
            message   = dto.Message,
            history
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat/stream");
        httpRequest.Content = JsonContent.Create(aiPayload);

        HttpResponseMessage aiResponse;
        try
        {
            // ResponseHeadersRead = don't buffer the body; we'll stream it manually.
            aiResponse = await _aiClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "AI engine unreachable for user {UserId} course {CourseId}", userId, dto.CourseId);

            await BroadcastError(userId,
                "AI engine is unreachable. Please try again later.", ct);
            throw;
        }

        if (!aiResponse.IsSuccessStatusCode)
        {
            var body = await aiResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "AI engine returned {Status} for user {UserId} course {CourseId}: {Body}",
                (int)aiResponse.StatusCode, userId, dto.CourseId, body);

            await BroadcastError(userId,
                $"AI engine error ({(int)aiResponse.StatusCode}). Please try again.", ct);
            throw new InvalidOperationException(
                $"AI engine returned {(int)aiResponse.StatusCode}: {body}");
        }

        // Read the SSE stream line-by-line, broadcasting each token as it arrives.
        var accumulated = new StringBuilder();

        await using var stream = await aiResponse.Content.ReadAsStreamAsync(ct);
        using var reader       = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            // SSE: blank lines are event boundaries — skip them.
            if (string.IsNullOrWhiteSpace(line)) continue;

            // SSE: lines starting with ':' are keep-alive comments — skip them.
            if (line[0] == ':') continue;

            // SSE: only "data:" fields carry payload.
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;

            var token = ParseSseToken(line);
            if (token is null) continue; // [DONE] sentinel or empty payload

            accumulated.Append(token);

            // Fire-and-forget style: we don't await here so we don't stall the
            // stream reader; SignalR queues the send internally.
            _ = _hub.Clients.User(userId.ToString())
                    .SendAsync("ReceiveChatToken", token, ct);
        }

        // ── Step 4: Persist AI Response & Broadcast Completion ───────────────
        var fullResponse = accumulated.ToString();

        if (!string.IsNullOrWhiteSpace(fullResponse))
        {
            var botMessage = new ChatMessage
            {
                UserId      = userId,
                CourseId    = dto.CourseId,
                Role        = MessageRole.Bot,
                MessageText = fullResponse,
                Timestamp   = DateTime.UtcNow
            };

            _db.ChatMessages.Add(botMessage);
            await _db.SaveChangesAsync(ct);

            await _hub.Clients.User(userId.ToString())
                .SendAsync("ChatStreamComplete", new
                {
                    messageId = botMessage.Id,
                    courseId  = dto.CourseId,
                    role      = "assistant",
                    timestamp = botMessage.Timestamp
                }, ct);
        }

        return new SendMessageAckDto
        {
            UserMessageId = userMessage.Id,
            CourseId      = dto.CourseId
        };
    }

    // ────────────────────────────────────────────────────────────────────────
    // GET /api/chat/history/{courseId}
    // ────────────────────────────────────────────────────────────────────────
    public async Task<ChatHistoryResponseDto> GetHistoryAsync(
        Guid userId, Guid courseId, int page = 1, int pageSize = 50)
    {
        var course = await _db.Courses.FindAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");

        var messages = await _db.ChatMessages
            .Where(m => m.UserId == userId && m.CourseId == courseId)
            .OrderByDescending(m => m.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(m => m.Timestamp)
            .Select(m => new ChatMessageResponseDto
            {
                Id          = m.Id,
                Role        = m.Role.ToString(),
                MessageText = m.MessageText,
                Timestamp   = m.Timestamp
            })
            .ToListAsync();

        return new ChatHistoryResponseDto
        {
            CourseId    = courseId,
            CourseTitle = course.Title,
            Messages    = messages
        };
    }

    // ────────────────────────────────────────────────────────────────────────
    // DELETE /api/chat/history/{courseId}
    // ────────────────────────────────────────────────────────────────────────
    public async Task ClearHistoryAsync(Guid userId, Guid courseId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.UserId == userId && m.CourseId == courseId)
            .ToListAsync();

        _db.ChatMessages.RemoveRange(messages);
        await _db.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the text token from one SSE data line.
    /// Handles three payload shapes produced by typical LLM engines:
    ///   {"token":"hello"}  |  {"text":"hello"}  |  {"content":"hello"}  |  hello
    /// Returns null for the [DONE] sentinel or an empty payload.
    /// </summary>
    private static string? ParseSseToken(string dataLine)
    {
        // Strip "data:" prefix plus any leading whitespace.
        var payload = dataLine["data:".Length..].TrimStart();

        if (string.IsNullOrEmpty(payload) || payload == "[DONE]")
            return null;

        if (payload[0] == '{')
        {
            // Attempt JSON extraction before falling back to raw string.
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                if (root.TryGetProperty("token",   out var t) && t.ValueKind == JsonValueKind.String)
                    return t.GetString();

                if (root.TryGetProperty("text",    out var x) && x.ValueKind == JsonValueKind.String)
                    return x.GetString();

                if (root.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    return c.GetString();
            }
            catch (JsonException)
            {
                // Not valid JSON — fall through to raw string treatment.
            }
        }

        return payload;
    }

    private Task BroadcastError(Guid userId, string message, CancellationToken ct)
        => _hub.Clients.User(userId.ToString())
               .SendAsync("ChatStreamError", new { error = message }, ct);
}
