using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PortfolioStudio.Models;

[Table("ProjectImages")]
public class ProjectImage
{
    [Key]
    public int ImageId { get; set; }

    [Required]
    public int ProjectId { get; set; }

    [Required, MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Caption { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
}
