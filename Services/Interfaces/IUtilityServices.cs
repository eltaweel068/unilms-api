namespace UniLMS.API.Services.Interfaces;

public interface IEmailService
{
    Task SendOtpAsync(string email, string otpCode);
    Task SendNotificationAsync(string email, string subject, string body);
}

public interface IFileStorageService
{
    /// <summary>Uploads a file and returns its public URL and SHA-256 hash.</summary>
    Task<(string url, string hash)> UploadAsync(IFormFile file, string bucket, string filePath);

    Task DeleteAsync(string fileUrl, string bucket);
}
