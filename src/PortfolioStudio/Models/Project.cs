using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PortfolioStudio.Models;

[Table("Projects")]
public class Project
{
    [Key]
    public int ProjectId { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(180)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string ShortDescription { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string ThumbnailUrl { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? ClientName { get; set; }

    public DateOnly? ProjectDate { get; set; }

    [MaxLength(300)]
    public string? TechStack { get; set; }

    [MaxLength(500)]
    public string? LiveUrl { get; set; }

    [MaxLength(500)]
    public string? RepoUrl { get; set; }

    public string? CaseStudyChallenge { get; set; }

    public string? CaseStudySolution { get; set; }

    public string? CaseStudyOutcome { get; set; }

    public bool IsPublished { get; set; }

    public bool IsFeatured { get; set; }

    public int DisplayOrder { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Category? Category { get; set; }

    public AdminUser? CreatedByUser { get; set; }

    public ICollection<ProjectImage> Images { get; set; } = new List<ProjectImage>();
}
