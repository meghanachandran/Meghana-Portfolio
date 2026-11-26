using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PortfolioStudio.Services;

public class ImageStorageService : IImageStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageStorageService> _logger;

    public ImageStorageService(
        IWebHostEnvironment env,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<ImageStorageService> logger)
    {
        _env = env;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsValidImage(IFormFile? file, out string? errorMessage)
    {
        errorMessage = null;
        if (file == null || file.Length == 0)
        {
            errorMessage = "Please select a non-empty image file.";
            return false;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            errorMessage = "Image size must not exceed 5 MB.";
            return false;
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            errorMessage = "Only JPG, PNG, WEBP, and GIF images are allowed.";
            return false;
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Invalid image content type.";
            return false;
        }

        if (!HasValidMagicBytes(file, ext))
        {
            errorMessage = "File contents do not match a valid JPG, PNG, WEBP, or GIF image.";
            return false;
        }

        return true;
    }

    private static bool HasValidMagicBytes(IFormFile file, string ext)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = file.OpenReadStream();
        var bytesRead = stream.Read(header);
        if (bytesRead < 4)
            return false;

        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => bytesRead >= 8 &&
                      header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                      header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            ".gif" => bytesRead >= 6 &&
                      header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'8' &&
                      (header[4] == (byte)'7' || header[4] == (byte)'9') && header[5] == (byte)'a',
            ".webp" => bytesRead >= 12 &&
                       header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                       header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P',
            _ => false
        };
    }

    public async Task<string> SaveImageAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        if (!IsValidImage(file, out var error))
            throw new InvalidOperationException(error ?? "Invalid image file.");

        var normalizedFolder = NormalizeFolderPath(folder);

        var cloudName = _config["Cloudinary:CloudName"];
        var apiKey = _config["Cloudinary:ApiKey"];
        var apiSecret = _config["Cloudinary:ApiSecret"];

        if (!string.IsNullOrWhiteSpace(cloudName) &&
            !string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(apiSecret))
        {
            return await UploadToCloudinaryAsync(file, normalizedFolder, cloudName, apiKey, apiSecret, ct);
        }

        return await SaveLocallyAsync(file, normalizedFolder, ct);
    }

    private static string NormalizeFolderPath(string folder)
    {
        var segments = folder
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(seg => string.Concat(seg.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')))
            .Where(seg => !string.IsNullOrWhiteSpace(seg))
            .ToArray();

        return segments.Length > 0 ? string.Join('/', segments) : "general";
    }

    private async Task<string> SaveLocallyAsync(IFormFile file, string normalizedFolder, CancellationToken ct)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadDir = Path.Combine(webRoot, "uploads", normalizedFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(uploadDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadDir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return $"/uploads/{normalizedFolder}/{fileName}";
    }

    private async Task<string> UploadToCloudinaryAsync(
        IFormFile file,
        string folder,
        string cloudName,
        string apiKey,
        string apiSecret,
        CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var targetFolder = $"portfoliostudio/{folder}";
        var toSign = $"folder={targetFolder}&timestamp={timestamp}{apiSecret}";
        var signatureBytes = SHA1.HashData(Encoding.UTF8.GetBytes(toSign));
        var signature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

        using var form = new MultipartFormDataContent();
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var fileContent = new ByteArrayContent(ms.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

        form.Add(fileContent, "file", file.FileName);
        form.Add(new StringContent(apiKey), "api_key");
        form.Add(new StringContent(timestamp), "timestamp");
        form.Add(new StringContent(targetFolder), "folder");
        form.Add(new StringContent(signature), "signature");

        var client = _httpClientFactory.CreateClient();
        var endpoint = $"https://api.cloudinary.com/v1_1/{cloudName}/image/upload";
        var response = await client.PostAsync(endpoint, form, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Cloudinary upload failed: {Status} {Body}", response.StatusCode, payload);
            throw new InvalidOperationException("Cloud image upload failed. Please try again.");
        }

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("secure_url", out var urlProp))
            return urlProp.GetString() ?? throw new InvalidOperationException("Missing secure_url from Cloudinary.");

        throw new InvalidOperationException("Unexpected Cloudinary response.");
    }

    public Task DeleteImageAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return Task.CompletedTask;

        if (!imageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        try
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var relative = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, relative));
            var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads"));

            if (fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete image file {ImageUrl}", imageUrl);
        }

        return Task.CompletedTask;
    }
}
