using System.ComponentModel.DataAnnotations;

namespace UniLMS.API.Models.Entities;

/// <summary>
/// Static metadata for the 4 academic levels.
/// Seeded at startup; admin can update descriptions.
/// </summary>
public class LevelInfo
{
    /// <summary>Primary key — the level number (1–4).</summary>
    [Key]
    public int LevelNumber { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>True for Level 3 &amp; 4; false for Level 1 &amp; 2.</summary>
    public bool HasDepartments { get; set; }
}
