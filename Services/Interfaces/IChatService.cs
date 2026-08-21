using UniLMS.API.Models.DTOs.Chat;

namespace UniLMS.API.Services.Interfaces;

public interface IChatService
{
    /// <summary>
    /// Saves the user message, streams tokens from the AI engine to the caller
    /// via SignalR, then persists the full AI response.
    /// Returns the saved user <see cref="SendMessageAckDto"/> for the HTTP layer.
    /// </summary>
    Task<SendMessageAckDto> SendStreamingMessageAsync(
        Guid userId, SendMessageDto dto, CancellationToken ct = default);

    Task<ChatHistoryResponseDto> GetHistoryAsync(
        Guid userId, Guid courseId, int page = 1, int pageSize = 50);

    Task ClearHistoryAsync(Guid userId, Guid courseId);
}
