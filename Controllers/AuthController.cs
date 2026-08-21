using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniLMS.API.Data;
using UniLMS.API.Models.DTOs.Auth;
using UniLMS.API.Models.Entities;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly AppDbContext _db;

    public AuthController(IAuthService auth, AppDbContext db)
    {
        _auth = auth;
        _db   = db;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _auth.RegisterAsync(dto);
        return Created("", result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _auth.LoginAsync(dto);
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        await _auth.ForgotPasswordAsync(dto);
        return Ok(new { message = "If the email exists, an OTP has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        await _auth.ResetPasswordAsync(dto);
        return Ok(new { message = "Password reset successfully." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auth.ChangePasswordAsync(userId, dto);
        return Ok(new { message = "Password changed successfully." });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("students")]
    public async Task<IActionResult> GetStudents()
    {
        var students = await _db.Users
            .Where(u => u.Role == UserRole.Student)
            .OrderBy(u => u.Name)
            .Select(u => new { u.Name, u.Email })
            .ToListAsync();

        return Ok(students);
    }
}
