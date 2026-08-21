using Microsoft.EntityFrameworkCore;
using UniLMS.API.Models.Entities;

namespace UniLMS.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<LevelInfo> Levels => Set<LevelInfo>();
    public DbSet<CourseMaterial> CourseMaterials => Set<CourseMaterial>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<OtpToken> OtpTokens => Set<OtpToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ── LevelInfo ──
        modelBuilder.Entity<LevelInfo>(e =>
        {
            e.HasKey(l => l.LevelNumber);
            e.Property(l => l.LevelNumber).ValueGeneratedNever();
        });

        // ── Course ──
        modelBuilder.Entity<Course>(e =>
        {
            // Store Department enum as its string name ("CS" / "IT" / "IS")
            e.Property(c => c.Department)
             .HasConversion<string?>()
             .HasMaxLength(10);

            e.HasOne(c => c.CreatedByAdmin)
             .WithMany()
             .HasForeignKey(c => c.CreatedByAdminId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ChatMessage ──
        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasIndex(m => new { m.UserId, m.CourseId, m.Timestamp });

            e.HasOne(m => m.User)
             .WithMany(u => u.ChatMessages)
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Course)
             .WithMany(c => c.ChatMessages)
             .HasForeignKey(m => m.CourseId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── CourseMaterial ──
        modelBuilder.Entity<CourseMaterial>(e =>
        {
            // Fast lookup by hash for deduplication checks.
            e.HasIndex(m => m.FileHash);

            // Prevent the same file being uploaded twice to the same course.
            e.HasIndex(m => new { m.CourseId, m.FileHash }).IsUnique();

            // Enforce one material per lecture number per course at the DB level.
            e.HasIndex(m => new { m.CourseId, m.LectureNumber }).IsUnique();

            e.HasOne(m => m.Course)
             .WithMany(c => c.Materials)
             .HasForeignKey(m => m.CourseId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── OtpToken ──
        modelBuilder.Entity<OtpToken>(e =>
        {
            e.HasIndex(o => new { o.Email, o.OtpCode });
        });
    }
}
