using System.ComponentModel.DataAnnotations;
using PortfolioStudio.Models;

namespace PortfolioStudio.ViewModels;

public class HomeViewModel
{
    public SiteProfile Profile { get; set; } = new();
    public List<ServiceItem> Services { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public string? SelectedCategorySlug { get; set; }
    public ContactFormModel ContactForm { get; set; } = new();
}

public class ContactFormModel
{
    [Required(ErrorMessage = "Please enter your name."), MaxLength(100)]
    [Display(Name = "Your Name")]
    public string SenderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a valid email."), EmailAddress, MaxLength(150)]
    [Display(Name = "Email Address")]
    public string SenderEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a subject."), MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please write a message."), MinLength(10, ErrorMessage = "Message should be at least 10 characters.")]
    [Display(Name = "Project Details / Message")]
    public string MessageBody { get; set; } = string.Empty;
}

public class ProjectDetailViewModel
{
    public Project Project { get; set; } = new();
    public List<Project> RelatedProjects { get; set; } = new();
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
