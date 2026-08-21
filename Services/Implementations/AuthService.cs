using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UniLMS.API.Data;
using UniLMS.API.Models.DTOs.Auth;
using UniLMS.API.Models.Entities;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

    public AuthService(AppDbContext db, IConfiguration config, IEmailService emailService)
    {
        _db = db;
        _config = config;
        _emailService = emailService;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email.ToLower()))
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Name = dto.Name,
            Email = dto.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = UserRole.Student // Default role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = GenerateJwt(user),
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            UserId = user.Id
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower())
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return new AuthResponseDto
        {
            Token = GenerateJwt(user),
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            UserId = user.Id
        };
    }

    public async Task ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower());
        if (user == null) return; // Silent fail to prevent email enumeration

        var otpCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        var otp = new OtpToken
        {
            Email = dto.Email.ToLower(),
            OtpCode = otpCode,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _db.OtpTokens.Add(otp);
        await _db.SaveChangesAsync();

        await _emailService.SendOtpAsync(dto.Email, otpCode);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        var otp = await _db.OtpTokens
            .Where(o => o.Email == dto.Email.ToLower()
                     && o.OtpCode == dto.OtpCode
                     && !o.IsUsed
                     && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Invalid or expired OTP.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower())
            ?? throw new InvalidOperationException("User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        otp.IsUsed = true;

        await _db.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
