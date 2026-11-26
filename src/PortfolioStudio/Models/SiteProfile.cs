using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PortfolioStudio.Models;

[Table("SiteProfile")]
public class SiteProfile
{
    [Key]
    public int ProfileId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Headline { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string AboutSummary { get; set; } = string.Empty;

    [Required]
    public string AboutBio { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }

    [MaxLength(500)]
    public string? ResumeUrl { get; set; }

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(300)]
    public string? GitHubUrl { get; set; }

    [MaxLength(300)]
    public string? LinkedInUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
