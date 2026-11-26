using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PortfolioStudio.Models;

[Table("ContactMessages")]
public class ContactMessage
{
    [Key]
    public int MessageId { get; set; }

    [Required, MaxLength(100)]
    public string SenderName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    public string SenderEmail { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string MessageBody { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
