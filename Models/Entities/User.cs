using System.ComponentModel.DataAnnotations;

namespace UniLMS.API.Models.Entities;

public enum UserRole
{
    Student,
    Admin
}

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; } = UserRole.Student;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
