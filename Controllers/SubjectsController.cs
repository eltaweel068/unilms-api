using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniLMS.API.Data;
using UniLMS.API.Models.DTOs.Course;
using UniLMS.API.Models.Entities;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly AppDbContext                _db;
    private readonly IFileStorageService         _storage;
    private readonly IHttpClientFactory          _httpFactory;
    private readonly IEmailService               _email;
    private readonly ILogger<SubjectsController> _logger;

    public SubjectsController(
        AppDbContext db,
        IFileStorageService storage,
        IHttpClientFactory httpFactory,
        IEmailService email,
        ILogger<SubjectsController> logger)
    {
        _db          = db;
        _storage     = storage;
        _httpFactory = httpFactory;
        _email       = email;
        _logger      = logger;
    }

    // ────────────────────────────────────────────────────────────────────
    // GET /api/subjects/{subjectId}/materials
    // Returns all materials ordered by lecture number ascending.
    // ────────────────────────────────────────────────────────────────────
    [HttpGet("{subjectId}/materials")]
    public async Task<IActionResult> GetMaterials(Guid subjectId)
    {
        var exists = await _db.Courses.AnyAsync(c => c.Id == subjectId);
        if (!exists) return NotFound("Subject not found.");

        var materials = await _db.CourseMaterials
            .Where(m => m.CourseId == subjectId)
            .OrderBy(m => m.LectureNumber)
            .Select(m => new CourseMaterialResponseDto
            {
                Id            = m.Id,
                LectureNumber = m.LectureNumber,
                FileName      = m.FileName,
                FileUrl       = m.FileUrl,
                FileSize      = m.FileSize,
                ContentType   = m.ContentType,
                UploadedAt    = m.UploadedAt,
                SummaryText   = m.SummaryText
            })
            .ToListAsync();

        return Ok(materials);
    }

    // ────────────────────────────────────────────────────────────────────
    // POST /api/subjects/{subjectId}/materials
    // Admin only — multipart/form-data: File (IFormFile) + LectureNumber (int)
    //
    // Pipeline:
    //   1. Validate inputs & fetch course.Code (AI engine identifier).
    //   2. SHA-256 hash dedup — abort early if file already exists for course.
    //   3. Lecture-number dedup — one material per lecture per course.
    //   4. Upload to Supabase Storage bucket "lectures".
    //   5. POST /api/ingest → AI RAG indexing.
    //      └─ failure → rollback Supabase + 400.
    //   6. POST /api/summarize → AI summary enrichment (non-critical).
    //      └─ failure → log warning, continue with null summary.
    //   7. Persist CourseMaterial to PostgreSQL → 200 OK.
    // ────────────────────────────────────────────────────────────────────
    [HttpPost("{subjectId}/materials")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMaterial(
        Guid subjectId,
        [FromForm] UploadMaterialDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var file          = dto.File;
        var lectureNumber = dto.LectureNumber;

        if (file.Length == 0)
            return BadRequest("No file provided.");

        // ── Step 1: Fetch course → Code (e.g. "CS201") ──────────────────────
        var course = await _db.Courses
            .AsNoTracking()
            .Select(c => new { c.Id, c.Code, c.Title })
            .FirstOrDefaultAsync(c => c.Id == subjectId);

        if (course == null)
            return NotFound("Subject not found.");

        // ── Step 2: SHA-256 hash — compute before any network call ───────────
        string fileHash;
        using (var hashStream = file.OpenReadStream())
        {
            var hashBytes = await SHA256.HashDataAsync(hashStream);
            fileHash = Convert.ToHexString(hashBytes).ToLower();
        }

        if (await _db.CourseMaterials.AnyAsync(m => m.CourseId == subjectId && m.FileHash == fileHash))
            return Conflict("This file has already been uploaded for this subject.");

        // ── Step 3: Lecture-number uniqueness guard ──────────────────────────
        if (await _db.CourseMaterials.AnyAsync(m => m.CourseId == subjectId && m.LectureNumber == lectureNumber))
            return Conflict($"Lecture {lectureNumber} already has a material uploaded for this subject.");

        // ── Step 4: Upload to Supabase Storage (bucket: "lectures") ──────────
        var materialId  = Guid.NewGuid();
        var storagePath = $"{subjectId}/{materialId}_{file.FileName}";

        string fileUrl;
        try
        {
            (fileUrl, _) = await _storage.UploadAsync(file, "lectures", storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Supabase upload failed for subject {SubjectId}", subjectId);
            return StatusCode(502, new { message = "File storage upload failed.", detail = ex.Message });
        }

        // ── Step 5: AI RAG Ingestion ─────────────────────────────────────────
        // Prefix the filename so the AI can cite the correct lecture in answers.
        var enhancedFileName = $"Lecture {lectureNumber} - {file.FileName}";

        var aiClient = _httpFactory.CreateClient("AiIngestService");

        var ingestPayload = new
        {
            course_id   = course.Code,
            material_id = materialId.ToString(),
            file_name   = enhancedFileName,
            file_url    = fileUrl
        };

        HttpResponseMessage ingestResponse;
        try
        {
            ingestResponse = await aiClient.PostAsJsonAsync("/api/ingest", ingestPayload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AI engine unreachable during ingest for material {MaterialId}", materialId);
            await _storage.DeleteAsync(fileUrl, "lectures");
            return StatusCode(502, new { message = "AI engine is unreachable. File upload was rolled back." });
        }

        if (!ingestResponse.IsSuccessStatusCode)
        {
            var errorBody = await ingestResponse.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "AI ingest rejected material {MaterialId}. Status: {Status}. Body: {Body}",
                materialId, (int)ingestResponse.StatusCode, errorBody);

            await _storage.DeleteAsync(fileUrl, "lectures");

            return BadRequest(new
            {
                message = "AI indexing failed. File upload was rolled back.",
                detail  = errorBody
            });
        }

        // ── Step 6: AI Summary Enrichment (non-critical) ─────────────────────
        // A failure here is logged but never blocks the upload — the material
        // is still saved with SummaryText = null.
        var summaryText = await FetchAiSummaryAsync(
            aiClient, course.Code, enhancedFileName, _logger);

        // ── Step 7: Persist to PostgreSQL ────────────────────────────────────
        var material = new CourseMaterial
        {
            Id            = materialId,
            CourseId      = subjectId,
            LectureNumber = lectureNumber,
            FileName      = enhancedFileName,
            FileUrl       = fileUrl,
            FileHash      = fileHash,
            FileSize      = file.Length,
            ContentType   = file.ContentType,
            UploadedAt    = DateTime.UtcNow,
            SummaryText   = string.IsNullOrWhiteSpace(summaryText) ? null : summaryText
        };

        _db.CourseMaterials.Add(material);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Lecture {LectureNumber} uploaded for subject {SubjectId} " +
            "(course: {Code}, material: {MaterialId}, summary: {HasSummary})",
            lectureNumber, subjectId, course.Code, materialId,
            material.SummaryText is not null);

        // ── Step 8: Notify all students by email (non-critical, fire-and-forget) ─
        // Fetch emails NOW while _db is still in scope — the async task runs after
        // the request scope is disposed, so querying _db inside the task would throw.
        var studentEmails = await _db.Users
            .Where(u => u.Role == UserRole.Student)
            .Select(u => u.Email)
            .ToListAsync();

        _ = NotifyStudentsAsync(course.Title, material, studentEmails, _logger);

        return Ok(new CourseMaterialResponseDto
        {
            Id            = material.Id,
            LectureNumber = material.LectureNumber,
            FileName      = material.FileName,
            FileUrl       = material.FileUrl,
            FileSize      = material.FileSize,
            ContentType   = material.ContentType,
            UploadedAt    = material.UploadedAt,
            SummaryText   = material.SummaryText
        });
    }

    // ────────────────────────────────────────────────────────────────────
    // DELETE /api/subjects/{subjectId}/materials/{materialId}
    // Admin only — removes from Supabase Storage and the database.
    // ────────────────────────────────────────────────────────────────────
    [HttpDelete("{subjectId}/materials/{materialId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteMaterial(Guid subjectId, Guid materialId)
    {
        var material = await _db.CourseMaterials
            .FirstOrDefaultAsync(m => m.Id == materialId && m.CourseId == subjectId);

        if (material == null) return NotFound("Material not found.");

        await _storage.DeleteAsync(material.FileUrl, "lectures");
        _db.CourseMaterials.Remove(material);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Sends a new-lecture notification to every student.
    /// Never throws — all failures are logged so the upload response is not affected.
    /// </summary>
    private async Task NotifyStudentsAsync(
        string courseTitle,
        CourseMaterial material,
        List<string> studentEmails,
        ILogger logger)
    {
        try
        {
            if (studentEmails.Count == 0) return;

            var subject = $"New Lecture Available: {courseTitle} — Lecture {material.LectureNumber}";

            var summarySection = material.SummaryText is not null
                ? $@"<h3 style=""color:#2c3e50"">Lecture Summary</h3>
                     <p style=""background:#f8f9fa;padding:12px;border-left:4px solid #3498db;white-space:pre-line"">{material.SummaryText}</p>"
                : string.Empty;

            var body = $@"
                <div style=""font-family:Arial,sans-serif;max-width:600px;margin:auto"">
                  <h2 style=""color:#2c3e50"">📚 New Lecture Uploaded</h2>
                  <p>A new lecture has been added to <strong>{courseTitle}</strong>.</p>
                  <table style=""border-collapse:collapse;width:100%"">
                    <tr><td style=""padding:6px 0;color:#7f8c8d"">Lecture</td>
                        <td style=""padding:6px 0""><strong>Lecture {material.LectureNumber}</strong></td></tr>
                    <tr><td style=""padding:6px 0;color:#7f8c8d"">File</td>
                        <td style=""padding:6px 0"">{material.FileName}</td></tr>
                  </table>
                  {summarySection}
                  <p style=""margin-top:20px"">
                    <a href=""{material.FileUrl}""
                       style=""background:#3498db;color:#fff;padding:10px 20px;text-decoration:none;border-radius:4px"">
                      Open PDF
                    </a>
                  </p>
                  <hr style=""margin-top:30px""/>
                  <p style=""color:#bdc3c7;font-size:12px"">UniLMS — AI-Powered University Learning Management System</p>
                </div>";

            var tasks = studentEmails.Select(email =>
                _email.SendNotificationAsync(email, subject, body));

            await Task.WhenAll(tasks);

            logger.LogInformation(
                "Lecture {LectureNumber} notification sent to {Count} student(s) for course '{CourseTitle}'.",
                material.LectureNumber, studentEmails.Count, courseTitle);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send lecture notification emails for material {MaterialId}.", material.Id);
        }
    }

    /// <summary>
    /// Calls POST /api/summarize and extracts the summary text.
    /// Never throws — returns null on any failure so the upload pipeline continues.
    ///
    /// Handles three response shapes produced by common LLM APIs:
    ///   {"summary":"..."}  |  {"text":"..."}  |  {"content":"..."}
    /// </summary>
    private static async Task<string?> FetchAiSummaryAsync(
        HttpClient client,
        string courseCode,
        string source,
        ILogger logger)
    {
        const int maxAttempts = 3;
        const int delaySeconds = 5;

        var payload = new { course_id = courseCode, source };

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await client.PostAsJsonAsync("/api/summarize", payload);

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    logger.LogWarning(
                        "AI summarize 503 for '{Source}' — attempt {Attempt}/{Max}. Retrying in {Delay}s.",
                        source, attempt, maxAttempts, delaySeconds);

                    if (attempt < maxAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "AI summarize returned {Status} for source '{Source}' — proceeding without summary.",
                        (int)response.StatusCode, source);
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(body))
                    return null;

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String)
                    return s.GetString();

                if (root.TryGetProperty("text",    out var t) && t.ValueKind == JsonValueKind.String)
                    return t.GetString();

                if (root.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    return c.GetString();

                logger.LogWarning(
                    "AI summarize response for '{Source}' had no recognisable field — body: {Body}",
                    source, body);

                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "AI summary fetch failed for source '{Source}' on attempt {Attempt}/{Max}.",
                    source, attempt, maxAttempts);

                if (attempt < maxAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }

        logger.LogWarning(
            "AI summarize gave up after {Max} attempts for '{Source}' — proceeding without summary.",
            maxAttempts, source);
        return null;
    }
}
