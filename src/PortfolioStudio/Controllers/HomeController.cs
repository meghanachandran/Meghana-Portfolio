using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortfolioStudio.Data;
using PortfolioStudio.Models;
using PortfolioStudio.ViewModels;

namespace PortfolioStudio.Controllers;

public class HomeController : Controller
{
    private readonly PortfolioDbContext _db;

    public HomeController(PortfolioDbContext db)
    {
        _db = db;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index(string? category)
    {
        var vm = await BuildHomeViewModelAsync(category);
        return View(vm);
    }

    [HttpPost("/contact")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("contact")]
    public async Task<IActionResult> Contact(ContactFormModel form)
    {
        var isAjax = string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (!ModelState.IsValid)
        {
            if (isAjax)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { success = false, errors });
            }

            var vm = await BuildHomeViewModelAsync(null);
            vm.ContactForm = form;
            return View("Index", vm);
        }

        var message = new ContactMessage
        {
            SenderName = form.SenderName.Trim(),
            SenderEmail = form.SenderEmail.Trim(),
            Subject = form.Subject.Trim(),
            MessageBody = form.MessageBody.Trim(),
            IsRead = false,
            SubmittedAt = DateTime.UtcNow
        };

        _db.ContactMessages.Add(message);
        await _db.SaveChangesAsync();

        if (isAjax)
        {
            return Ok(new
            {
                success = true,
                message = "Thank you! Your message has been received and saved."
            });
        }

        TempData["ContactSuccess"] = "Thank you! Your inquiry has been received. I'll get back to you shortly.";
        return Redirect("/#contact");
    }

    [HttpGet("/portfolio/{slug}")]
    public async Task<IActionResult> ProjectDetail(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound();

        var isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

        var project = await _db.Projects
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.ImageId))
            .FirstOrDefaultAsync(p => p.Slug == slug && (p.IsPublished || isAdmin));

        if (project == null)
            return NotFound();

        var related = await _db.Projects
            .Include(p => p.Category)
            .Where(p => p.IsPublished && p.ProjectId != project.ProjectId)
            .OrderByDescending(p => p.CategoryId == project.CategoryId)
            .ThenByDescending(p => p.IsFeatured)
            .ThenBy(p => p.DisplayOrder)
            .Take(3)
            .ToListAsync();

        var vm = new ProjectDetailViewModel
        {
            Project = project,
            RelatedProjects = related
        };

        return View(vm);
    }

    [HttpGet("/Home/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        ViewBag.StatusCode = statusCode ?? 500;
        return View();
    }

    private async Task<HomeViewModel> BuildHomeViewModelAsync(string? categorySlug)
    {
        var profile = await _db.SiteProfiles.OrderBy(p => p.ProfileId).FirstOrDefaultAsync()
            ?? new SiteProfile { FullName = "Meghana Chandran", Headline = "ASP.NET Full Stack Developer", Email = "meghanachandran.98@gmail.com" };

        var services = await _db.Services
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.ServiceId)
            .ToListAsync();

        var categories = await _db.Categories
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        var projectsQuery = _db.Projects
            .Include(p => p.Category)
            .Where(p => p.IsPublished);

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            projectsQuery = projectsQuery.Where(p => p.Category != null && p.Category.Slug == categorySlug);
        }

        var projects = await projectsQuery
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();

        return new HomeViewModel
        {
            Profile = profile,
            Services = services,
            Categories = categories,
            Projects = projects,
            SelectedCategorySlug = categorySlug
        };
    }
}
