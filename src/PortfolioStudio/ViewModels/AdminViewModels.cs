using System.ComponentModel.DataAnnotations;
using PortfolioStudio.Models;

namespace PortfolioStudio.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalProjects { get; set; }
    public int PublishedProjects { get; set; }
    public int DraftProjects { get; set; }
    public int TotalCategories { get; set; }
    public int ActiveServices { get; set; }
    public int UnreadMessages { get; set; }
    public List<Project> RecentProjects { get; set; } = new();
    public List<ContactMessage> RecentMessages { get; set; } = new();
}

public class ProjectFormViewModel
{
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Select a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(180)]
    [Display(Name = "URL Slug (leave blank to auto-generate)")]
    public string? Slug { get; set; }

    [Required, MaxLength(400)]
    [Display(Name = "Short Summary (shown on portfolio card)")]
    public string ShortDescription { get; set; } = string.Empty;

    public string? CurrentThumbnailUrl { get; set; }

    [Display(Name = "Cover Image Upload")]
    public IFormFile? ThumbnailFile { get; set; }

    [MaxLength(120)]
    [Display(Name = "Client / Organization")]
    public string? ClientName { get; set; }

    [Display(Name = "Completion Date")]
    public DateOnly? ProjectDate { get; set; }

    [MaxLength(300)]
    [Display(Name = "Technologies Used (comma-separated)")]
    public string? TechStack { get; set; }

    [Url, MaxLength(500)]
    [Display(Name = "Live Project URL")]
    public string? LiveUrl { get; set; }

    [Url, MaxLength(500)]
    [Display(Name = "Source Code / GitHub URL")]
    public string? RepoUrl { get; set; }

    [Display(Name = "Case Study: The Challenge")]
    public string? CaseStudyChallenge { get; set; }

    [Display(Name = "Case Study: The Solution & Architecture")]
    public string? CaseStudySolution { get; set; }

    [Display(Name = "Case Study: Key Outcomes & Impact")]
    public string? CaseStudyOutcome { get; set; }

    [Display(Name = "Publish immediately on public website")]
    public bool IsPublished { get; set; } = true;

    [Display(Name = "Feature at top of portfolio")]
    public bool IsFeatured { get; set; }

    [Display(Name = "Display Order")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Add Case Study Gallery Images")]
    public List<IFormFile>? GalleryFiles { get; set; }

    public List<ProjectImage> ExistingImages { get; set; } = new();
    public List<int> RemoveImageIds { get; set; } = new();
    public List<Category> AvailableCategories { get; set; } = new();
}

public class CategoryFormViewModel
{
    public int CategoryId { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Slug { get; set; }

    [Display(Name = "Display Order")]
    public int DisplayOrder { get; set; }
}

public class ServiceFormViewModel
{
    public int ServiceId { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "Bootstrap Icon Class (e.g. bi-layers, bi-cpu)")]
    public string? IconClass { get; set; } = "bi-layers";

    [Display(Name = "Display Order")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Visible on landing page")]
    public bool IsActive { get; set; } = true;
}

public class ProfileFormViewModel
{
    public int ProfileId { get; set; }

    [Required, MaxLength(100)]
    [Display(Name = "Full Name / Brand Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    [Display(Name = "Hero Headline")]
    public string Headline { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    [Display(Name = "Hero / About Lead Summary")]
    public string AboutSummary { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Detailed About Bio")]
    public string AboutBio { get; set; } = string.Empty;

    public string? CurrentProfileImageUrl { get; set; }

    [Display(Name = "Upload Profile / Studio Photo")]
    public IFormFile? ProfileImageFile { get; set; }

    [MaxLength(500)]
    [Display(Name = "Resume / CV URL")]
    public string? ResumeUrl { get; set; }

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [Url, MaxLength(300)]
    [Display(Name = "GitHub URL")]
    public string? GitHubUrl { get; set; }

    [Url, MaxLength(300)]
    [Display(Name = "LinkedIn URL")]
    public string? LinkedInUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Admin Display Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    [Display(Name = "Admin Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
