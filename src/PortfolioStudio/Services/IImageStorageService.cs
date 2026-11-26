namespace PortfolioStudio.Services;

public interface IImageStorageService
{
    Task<string> SaveImageAsync(IFormFile file, string folder, CancellationToken ct = default);
    Task DeleteImageAsync(string? imageUrl);
    bool IsValidImage(IFormFile? file, out string? errorMessage);
}
