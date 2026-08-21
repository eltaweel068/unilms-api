using System.ComponentModel.DataAnnotations;

namespace UniLMS.API.Models.Entities;

public enum Department
{
    CS,
    IT,
    IS
}

public class Course
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Course code e.g. CS102, MA101</summary>
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TitleAr { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? DescriptionAr { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid CreatedByAdminId { get; set; }

    /// <summary>Academic level: 1, 2, 3, or 4.</summary>
    [Range(1, 4)]
    public int Level { get; set; } = 1;

    /// <summary>
    /// Department for Level 3 &amp; 4 only (CS / IT / IS).
    /// Null for Level 1 &amp; 2.
    /// </summary>
    public Department? Department { get; set; }

    // Navigation
    public User CreatedByAdmin { get; set; } = null!;
    public ICollection<CourseMaterial> Materials { get; set; } = new List<CourseMaterial>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
