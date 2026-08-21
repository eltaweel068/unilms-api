using Microsoft.EntityFrameworkCore;
using UniLMS.API.Data;
using UniLMS.API.Models.DTOs.Course;
using UniLMS.API.Models.Entities;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Services.Implementations;

public class CourseService : ICourseService
{
    private readonly AppDbContext _db;

    public CourseService(AppDbContext db) => _db = db;

    public async Task<CourseResponseDto> CreateAsync(Guid adminId, CreateCourseDto dto)
    {
        ValidateLevelDepartment(dto.Level, dto.Department);

        var course = new Course
        {
            Code             = dto.Code,
            Title            = dto.Title,
            TitleAr          = dto.TitleAr,
            Description      = dto.Description,
            DescriptionAr    = dto.DescriptionAr,
            CreatedByAdminId = adminId,
            Level            = dto.Level,
            Department       = dto.Department
        };

        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        return await MapToResponseDto(course.Id);
    }

    public async Task<CourseResponseDto> GetByIdAsync(Guid courseId)
        => await MapToResponseDto(courseId);

    public async Task<List<CourseResponseDto>> GetAllAsync()
        => await BuildQuery(_db.Courses).ToListAsync();

    public async Task<List<CourseResponseDto>> GetByLevelAsync(int level, Department? department)
    {
        var query = _db.Courses.Where(c => c.Level == level);

        if (department.HasValue)
            query = query.Where(c => c.Department == department.Value);

        return await BuildQuery(query).ToListAsync();
    }

    public async Task<CourseResponseDto> UpdateAsync(Guid courseId, Guid adminId, UpdateCourseDto dto)
    {
        var course = await _db.Courses.FindAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");

        if (course.CreatedByAdminId != adminId)
            throw new UnauthorizedAccessException("Only the course creator can update it.");

        if (dto.Code          != null) course.Code          = dto.Code;
        if (dto.Title         != null) course.Title         = dto.Title;
        if (dto.TitleAr       != null) course.TitleAr       = dto.TitleAr;
        if (dto.Description   != null) course.Description   = dto.Description;
        if (dto.DescriptionAr != null) course.DescriptionAr = dto.DescriptionAr;

        var newLevel = dto.Level ?? course.Level;
        var newDept  = dto.Level.HasValue || dto.Department.HasValue
            ? dto.Department
            : course.Department;

        ValidateLevelDepartment(newLevel, newDept);

        course.Level      = newLevel;
        course.Department = newDept;

        await _db.SaveChangesAsync();
        return await MapToResponseDto(courseId);
    }

    public async Task DeleteAsync(Guid courseId, Guid adminId)
    {
        var course = await _db.Courses.FindAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");

        if (course.CreatedByAdminId != adminId)
            throw new UnauthorizedAccessException("Only the course creator can delete it.");

        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static IQueryable<CourseResponseDto> BuildQuery(IQueryable<Course> query)
        => query
            .Include(c => c.CreatedByAdmin)
            .Select(c => new CourseResponseDto
            {
                Id                 = c.Id,
                Code               = c.Code,
                Title              = c.Title,
                TitleAr            = c.TitleAr,
                Description        = c.Description,
                DescriptionAr      = c.DescriptionAr,
                CreatedAt          = c.CreatedAt,
                CreatedByAdminName = c.CreatedByAdmin.Name,
                Level              = c.Level,
                Department         = c.Department == null ? null : c.Department.ToString()
            });

    private async Task<CourseResponseDto> MapToResponseDto(Guid courseId)
        => await BuildQuery(_db.Courses.Where(c => c.Id == courseId))
               .FirstOrDefaultAsync()
           ?? throw new KeyNotFoundException("Course not found.");

    /// <summary>
    /// Levels 1 &amp; 2 → no department.
    /// Levels 3 &amp; 4 → department required.
    /// </summary>
    private static void ValidateLevelDepartment(int level, Department? department)
    {
        if (level <= 2 && department.HasValue)
            throw new InvalidOperationException("Levels 1 and 2 do not belong to a department.");

        if (level >= 3 && !department.HasValue)
            throw new InvalidOperationException("Levels 3 and 4 require a department: CS, IT, or IS.");
    }
}
