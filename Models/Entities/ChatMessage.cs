using System.ComponentModel.DataAnnotations;

namespace UniLMS.API.Models.Entities;

public enum MessageRole
{
    User,
    Bot
}

public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }

    [Required]
    public MessageRole Role { get; set; }

    [Required]
    public string MessageText { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Course Course { get; set; } = null!;
}
