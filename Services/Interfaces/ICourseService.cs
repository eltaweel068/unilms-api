using UniLMS.API.Models.DTOs.Course;
using UniLMS.API.Models.Entities;

namespace UniLMS.API.Services.Interfaces;

public interface ICourseService
{
    Task<CourseResponseDto> CreateAsync(Guid adminId, CreateCourseDto dto);
    Task<CourseResponseDto> GetByIdAsync(Guid courseId);
    Task<List<CourseResponseDto>> GetAllAsync();

    /// <summary>
    /// Returns subjects for a given level.
    /// For Level 3 &amp; 4 pass the department; for Level 1 &amp; 2 leave it null.
    /// </summary>
    Task<List<CourseResponseDto>> GetByLevelAsync(int level, Department? department);

    Task<CourseResponseDto> UpdateAsync(Guid courseId, Guid adminId, UpdateCourseDto dto);
    Task DeleteAsync(Guid courseId, Guid adminId);
}
