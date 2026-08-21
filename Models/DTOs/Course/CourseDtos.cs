using System.ComponentModel.DataAnnotations;
using UniLMS.API.Models.Entities;

namespace UniLMS.API.Models.DTOs.Course;

// ──────────────────────────── Course / Subject ────────────────────────────

public class CreateCourseDto
{
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

    [Required, Range(1, 4)]
    public int Level { get; set; }

    public Department? Department { get; set; }
}

public class UpdateCourseDto
{
    [MaxLength(20)]
    public string? Code { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(200)]
    public string? TitleAr { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? DescriptionAr { get; set; }

    [Range(1, 4)]
    public int? Level { get; set; }

    public Department? Department { get; set; }
}

public class CourseResponseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? TitleAr { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByAdminName { get; set; } = string.Empty;
    public int Level { get; set; }

    /// <summary>"CS" | "IT" | "IS" | null</summary>
    public string? Department { get; set; }
}

// ──────────────────────────── Material upload (form-data) ─────────

public class UploadMaterialDto
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required, Range(1, int.MaxValue, ErrorMessage = "lectureNumber must be 1 or greater.")]
    public int LectureNumber { get; set; }
}

// ──────────────────────────── Material ────────────────────────────

public class CourseMaterialResponseDto
{
    public Guid     Id            { get; set; }
    public int      LectureNumber { get; set; }
    public string   FileName      { get; set; } = string.Empty;
    public string   FileUrl       { get; set; } = string.Empty;
    public long     FileSize      { get; set; }
    public string   ContentType   { get; set; } = string.Empty;
    public DateTime UploadedAt    { get; set; }

    /// <summary>
    /// AI-generated lecture summary. Null when the summarise step failed or was skipped.
    /// </summary>
    public string?  SummaryText   { get; set; }
}

// ──────────────────────────── Level Info ────────────────────────────

public class LevelInfoResponseDto
{
    public int LevelNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool HasDepartments { get; set; }
    public int SubjectsCount { get; set; }
}
