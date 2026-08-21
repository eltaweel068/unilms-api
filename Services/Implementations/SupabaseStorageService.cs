using System.Security.Cryptography;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Services.Implementations;

public class SupabaseStorageService : IFileStorageService
{
    private readonly Supabase.Client _supabase;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(Supabase.Client supabase, ILogger<SupabaseStorageService> logger)
    {
        _supabase = supabase;
        _logger   = logger;
    }

    public async Task<(string url, string hash)> UploadAsync(
        IFormFile file, string bucket, string filePath)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        // SHA-256 hash for deduplication
        var hash = Convert.ToHexString(SHA256.HashData(fileBytes)).ToLower();

        await _supabase.Storage
            .From(bucket)
            .Upload(fileBytes, filePath, new Supabase.Storage.FileOptions
            {
                ContentType = file.ContentType
            });

        var url = _supabase.Storage.From(bucket).GetPublicUrl(filePath);

        return (url, hash);
    }

    public async Task DeleteAsync(string fileUrl, string bucket)
    {
        try
        {
            var uri          = new Uri(fileUrl);
            var pathSegments = uri.AbsolutePath.Split($"/storage/v1/object/public/{bucket}/");
            if (pathSegments.Length > 1)
                await _supabase.Storage.From(bucket).Remove(new List<string> { pathSegments[1] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file from storage: {Url}", fileUrl);
        }
    }
}
