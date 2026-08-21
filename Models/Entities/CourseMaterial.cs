using System.ComponentModel.DataAnnotations;

namespace UniLMS.API.Models.Entities;

public class CourseMaterial
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CourseId { get; set; }

    /// <summary>Ordinal position of this lecture within the course (1-based).</summary>
    public int LectureNumber { get; set; }

    /// <summary>
    /// Stored as "Lecture {N} - {original_file_name}" so the label is
    /// consistent with what was sent to the AI engine for indexing.
    /// </summary>
    [Required, MaxLength(350)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>SHA-256 hex digest used for deduplication.</summary>
    [Required, MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// AI-generated summary of the lecture content.
    /// Null when the summarise step was skipped or failed — does not block upload.
    /// </summary>
    public string? SummaryText { get; set; }

    // Navigation
    public Course Course { get; set; } = null!;
}
