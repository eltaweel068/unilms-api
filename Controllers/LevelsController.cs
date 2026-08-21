using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniLMS.API.Data;
using UniLMS.API.Models.DTOs.Course;
using UniLMS.API.Models.Entities;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LevelsController : ControllerBase
{
    private readonly AppDbContext    _db;
    private readonly ICourseService  _courses;

    public LevelsController(AppDbContext db, ICourseService courses)
    {
        _db      = db;
        _courses = courses;
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /api/levels
    // Returns all 4 academic levels with their metadata.
    // ────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var levels = await _db.Levels
            .OrderBy(l => l.LevelNumber)
            .ToListAsync();

        // Attach subject counts
        var result = new List<LevelInfoResponseDto>();
        foreach (var l in levels)
        {
            var count = await _db.Courses.CountAsync(c => c.Level == l.LevelNumber);
            result.Add(new LevelInfoResponseDto
            {
                LevelNumber    = l.LevelNumber,
                Name           = l.Name,
                Description    = l.Description,
                HasDepartments = l.HasDepartments,
                SubjectsCount  = count
            });
        }

        return Ok(result);
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /api/levels/{levelNumber}/subjects
    //
    // Level 1 & 2  →  call without query param  (no department)
    // Level 3 & 4  →  ?department=CS | IT | IS
    //
    // Examples:
    //   GET /api/levels/1/subjects
    //   GET /api/levels/3/subjects?department=CS
    // ────────────────────────────────────────────────────────────────────
    [HttpGet("{levelNumber}/subjects")]
    public async Task<IActionResult> GetSubjects(
        [FromRoute] int     levelNumber,
        [FromQuery] string? department = null)
    {
        if (levelNumber < 1 || levelNumber > 4)
            return BadRequest("Level must be between 1 and 4.");

        Department? dept = null;

        if (!string.IsNullOrWhiteSpace(department))
        {
            if (!Enum.TryParse<Department>(department.Trim(), ignoreCase: true, out var parsed))
                return BadRequest($"Invalid department '{department}'. Valid values: CS, IT, IS.");

            dept = parsed;
        }

        // Business-rule guard
        if (levelNumber <= 2 && dept.HasValue)
            return BadRequest("Levels 1 and 2 do not have departments.");

        if (levelNumber >= 3 && !dept.HasValue)
            return BadRequest("Please specify a department for Level 3 or 4: CS, IT, or IS.");

        var subjects = await _courses.GetByLevelAsync(levelNumber, dept);
        return Ok(subjects);
    }
}
