using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioStudio.Data;
using PortfolioStudio.Models;
using PortfolioStudio.Services;
using PortfolioStudio.ViewModels;

namespace PortfolioStudio.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AdminController : Controller
{
    private readonly PortfolioDbContext _db;
    private readonly IImageStorageService _storage;
    private readonly IPasswordHasher<AdminUser> _hasher;

    public AdminController(
        PortfolioDbContext db,
        IImageStorageService storage,
        IPasswordHasher<AdminUser> hasher)
    {
        _db = db;
        _storage = storage;
        _hasher = hasher;
    }

    [HttpGet("/Admin")]
    public async Task<IActionResult> Index()
    {
        var vm = new AdminDashboardViewModel
        {
            TotalProjects = await _db.Projects.CountAsync(),
            PublishedProjects = await _db.Projects.CountAsync(p => p.IsPublished),
            DraftProjects = await _db.Projects.CountAsync(p => !p.IsPublished),
            TotalCategories = await _db.Categories.CountAsync(),
            ActiveServices = await _db.Services.CountAsync(s => s.IsActive),
            UnreadMessages = await _db.ContactMessages.CountAsync(m => !m.IsRead),
            RecentProjects = await _db.Projects
                .Include(p => p.Category)
                .OrderByDescending(p => p.UpdatedAt)
                .Take(6)
                .ToListAsync(),
            RecentMessages = await _db.ContactMessages
                .OrderByDescending(m => m.SubmittedAt)
                .Take(5)
                .ToListAsync()
        };

        return View(vm);
    }

    // =========================================================================
    // Projects & Case Studies
    // =========================================================================

    [HttpGet("/Admin/Projects")]
    public async Task<IActionResult> Projects(string? search, int? categoryId, string? status)
    {
        var query = _db.Projects
            .Include(p => p.Category)
            .Include(p => p.Images)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(term) ||
                p.ShortDescription.ToLower().Contains(term) ||
                (p.TechStack != null && p.TechStack.ToLower().Contains(term)));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (status == "published")
            query = query.Where(p => p.IsPublished);
        else if (status == "draft")
            query = query.Where(p => !p.IsPublished);

        ViewBag.Categories = await _db.Categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync();
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.Status = status;

        var list = await query
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.UpdatedAt)
            .ToListAsync();

        return View(list);
    }

    [HttpGet("/Admin/Projects/Create")]
    public async Task<IActionResult> CreateProject()
    {
        var categories = await _db.Categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync();
        var vm = new ProjectFormViewModel
        {
            AvailableCategories = categories,
            CategoryId = categories.FirstOrDefault()?.CategoryId ?? 0,
            ProjectDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsPublished = true
        };
        return View("ProjectForm", vm);
    }

    [HttpPost("/Admin/Projects/Create")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> CreateProject(ProjectFormViewModel model)
    {
        model.AvailableCategories = await _db.Categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync();

        if (!model.AvailableCategories.Any(c => c.CategoryId == model.CategoryId))
            ModelState.AddModelError(nameof(model.CategoryId), "Selected category does not exist.");

        if (!UrlSafetyHelper.IsValidSafeUrlOrEmpty(model.LiveUrl))
            ModelState.AddModelError(nameof(model.LiveUrl), "Live URL must start with http:// or https://.");

        if (!UrlSafetyHelper.IsValidSafeUrlOrEmpty(model.RepoUrl))
            ModelState.AddModelError(nameof(model.RepoUrl), "Repository URL must start with http:// or https://.");

        if (model.ThumbnailFile == null)
        {
            ModelState.AddModelError(nameof(model.ThumbnailFile), "Please upload a cover image for the project.");
        }
        else if (!_storage.IsValidImage(model.ThumbnailFile, out var imgErr))
        {
            ModelState.AddModelError(nameof(model.ThumbnailFile), imgErr ?? "Invalid cover image.");
        }

        ValidateGalleryFiles(model.GalleryFiles);

        if (!ModelState.IsValid)
            return View("ProjectForm", model);

        var slug = await EnsureUniqueProjectSlugAsync(model.Slug, model.Title);
        var thumbnailUrl = await _storage.SaveImageAsync(model.ThumbnailFile!, "projects");

        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : null;

        var project = new Project
        {
            CategoryId = model.CategoryId,
            Title = model.Title.Trim(),
            Slug = slug,
            ShortDescription = model.ShortDescription.Trim(),
            ThumbnailUrl = thumbnailUrl,
            ClientName = model.ClientName?.Trim(),
            ProjectDate = model.ProjectDate,
            TechStack = model.TechStack?.Trim(),
            LiveUrl = model.LiveUrl?.Trim(),
            RepoUrl = model.RepoUrl?.Trim(),
            CaseStudyChallenge = model.CaseStudyChallenge?.Trim(),
            CaseStudySolution = model.CaseStudySolution?.Trim(),
            CaseStudyOutcome = model.CaseStudyOutcome?.Trim(),
            IsPublished = model.IsPublished,
            IsFeatured = model.IsFeatured,
            DisplayOrder = model.DisplayOrder,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (model.GalleryFiles != null)
        {
            var order = 1;
            foreach (var file in model.GalleryFiles.Where(f => f.Length > 0))
            {
                var url = await _storage.SaveImageAsync(file, "projects/gallery");
                project.Images.Add(new ProjectImage
                {
                    ImageUrl = url,
                    DisplayOrder = order++,
                    UploadedAt = DateTime.UtcNow
                });
            }
        }

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Project \"{project.Title}\" created successfully.";
        return RedirectToAction(nameof(Projects));
    }

    [HttpGet("/Admin/Projects/Edit/{id:int}")]
    public async Task<IActionResult> EditProject(int id)
    {
        var project = await _db.Projects
            .Include(p => p.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.ImageId))
            .FirstOrDefaultAsync(p => p.ProjectId == id);

        if (project == null)
            return NotFound();

        var vm = new ProjectFormViewModel
        {
            ProjectId = project.ProjectId,
            CategoryId = project.CategoryId,
            Title = project.Title,
            Slug = project.Slug,
            ShortDescription = project.ShortDescription,
            CurrentThumbnailUrl = project.ThumbnailUrl,
            ClientName = project.ClientName,
            ProjectDate = project.ProjectDate,
            TechStack = project.TechStack,
            LiveUrl = project.LiveUrl,
            RepoUrl = project.RepoUrl,
            CaseStudyChallenge = project.CaseStudyChallenge,
            CaseStudySolution = project.CaseStudySolution,
            CaseStudyOutcome = project.CaseStudyOutcome,
            IsPublished = project.IsPublished,
            IsFeatured = project.IsFeatured,
            DisplayOrder = project.DisplayOrder,
            ExistingImages = project.Images.ToList(),
            AvailableCategories = await _db.Categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync()
        };

        return View("ProjectForm", vm);
    }

    [HttpPost("/Admin/Projects/Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> EditProject(int id, ProjectFormViewModel model)
    {
        var project = await _db.Projects
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.ProjectId == id);

        if (project == null)
            return NotFound();

        model.ProjectId = id;
        model.CurrentThumbnailUrl = project.ThumbnailUrl;
        model.ExistingImages = project.Images.OrderBy(i => i.DisplayOrder).ThenBy(i => i.ImageId).ToList();
        model.AvailableCategories = await _db.Categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync();

        if (!model.AvailableCategories.Any(c => c.CategoryId == model.CategoryId))
            ModelState.AddModelError(nameof(model.CategoryId), "Selected category does not exist.");

        if (!UrlSafetyHelper.IsValidSafeUrlOrEmpty(model.LiveUrl))
            ModelState.AddModelError(nameof(model.LiveUrl), "Live URL must start with http:// or https://.");

        if (!UrlSafetyHelper.IsValidSafeUrlOrEmpty(model.RepoUrl))
            ModelState.AddModelError(nameof(model.RepoUrl), "Repository URL must start with http:// or https://.");

        if (model.ThumbnailFile != null && model.ThumbnailFile.Length > 0 &&
            !_storage.IsValidImage(model.ThumbnailFile, out var imgErr))
        {
            ModelState.AddModelError(nameof(model.ThumbnailFile), imgErr ?? "Invalid cover image.");
        }

        ValidateGalleryFiles(model.GalleryFiles);

        if (!ModelState.IsValid)
            return View("ProjectForm", model);

        if (model.ThumbnailFile != null && model.ThumbnailFile.Length > 0)
        {
            var oldThumb = project.ThumbnailUrl;
            project.ThumbnailUrl = await _storage.SaveImageAsync(model.ThumbnailFile, "projects");
            await _storage.DeleteImageAsync(oldThumb);
        }

        if (model.RemoveImageIds.Count > 0)
        {
            var toRemove = project.Images.Where(i => model.RemoveImageIds.Contains(i.ImageId)).ToList();
            foreach (var img in toRemove)
            {
                await _storage.DeleteImageAsync(img.ImageUrl);
                _db.ProjectImages.Remove(img);
            }
        }

        if (model.GalleryFiles != null)
        {
            var nextOrder = project.Images.Any() ? project.Images.Max(i => i.DisplayOrder) + 1 : 1;
            foreach (var file in model.GalleryFiles.Where(f => f.Length > 0))
            {
                var url = await _storage.SaveImageAsync(file, "projects/gallery");
                project.Images.Add(new ProjectImage
                {
                    ImageUrl = url,
                    DisplayOrder = nextOrder++,
                    UploadedAt = DateTime.UtcNow
                });
            }
        }

        project.CategoryId = model.CategoryId;
        project.Title = model.Title.Trim();
        project.Slug = await EnsureUniqueProjectSlugAsync(model.Slug, model.Title, id);
        project.ShortDescription = model.ShortDescription.Trim();
        project.ClientName = model.ClientName?.Trim();
        project.ProjectDate = model.ProjectDate;
        project.TechStack = model.TechStack?.Trim();
        project.LiveUrl = model.LiveUrl?.Trim();
        project.RepoUrl = model.RepoUrl?.Trim();
        project.CaseStudyChallenge = model.CaseStudyChallenge?.Trim();
        project.CaseStudySolution = model.CaseStudySolution?.Trim();
        project.CaseStudyOutcome = model.CaseStudyOutcome?.Trim();
        project.IsPublished = model.IsPublished;
        project.IsFeatured = model.IsFeatured;
        project.DisplayOrder = model.DisplayOrder;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Project \"{project.Title}\" updated.";
        return RedirectToAction(nameof(Projects));
    }

    [HttpPost("/Admin/Projects/TogglePublish/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null)
            return NotFound();

        project.IsPublished = !project.IsPublished;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = project.IsPublished
            ? $"\"{project.Title}\" is now published on the live portfolio."
            : $"\"{project.Title}\" has been unpublished (saved as draft).";

        return RedirectToAction(nameof(Projects));
    }

    [HttpPost("/Admin/Projects/Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var project = await _db.Projects
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.ProjectId == id);

        if (project == null)
            return NotFound();

        var files = new List<string> { project.ThumbnailUrl };
        files.AddRange(project.Images.Select(i => i.ImageUrl));

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();

        foreach (var file in files)
            await _storage.DeleteImageAsync(file);

        TempData["Success"] = $"Project \"{project.Title}\" was deleted.";
        return RedirectToAction(nameof(Projects));
    }

    // =========================================================================
    // Categories
    // =========================================================================

    [HttpGet("/Admin/Categories")]
    public async Task<IActionResult> Categories()
    {
        var categories = await _db.Categories
            .Include(c => c.Projects)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
        return View(categories);
    }

    [HttpPost("/Admin/Categories/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a valid category name.";
            return RedirectToAction(nameof(Categories));
        }

        var normalizedName = model.Name.Trim().ToLowerInvariant();
        var duplicateName = await _db.Categories.AnyAsync(c =>
            c.Name.ToLower() == normalizedName && c.CategoryId != model.CategoryId);
        if (duplicateName)
        {
            TempData["Error"] = $"Category \"{model.Name.Trim()}\" already exists.";
            return RedirectToAction(nameof(Categories));
        }

        var slug = await EnsureUniqueCategorySlugAsync(model.Slug, model.Name, model.CategoryId);

        if (model.CategoryId == 0)
        {
            _db.Categories.Add(new Category
            {
                Name = model.Name.Trim(),
                Slug = slug,
                DisplayOrder = model.DisplayOrder,
                CreatedAt = DateTime.UtcNow
            });
            TempData["Success"] = $"Category \"{model.Name.Trim()}\" added.";
        }
        else
        {
            var cat = await _db.Categories.FindAsync(model.CategoryId);
            if (cat == null) return NotFound();
            cat.Name = model.Name.Trim();
            cat.Slug = slug;
            cat.DisplayOrder = model.DisplayOrder;
            TempData["Success"] = $"Category \"{cat.Name}\" updated.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost("/Admin/Categories/Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Projects).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category == null) return NotFound();

        if (category.Projects.Count > 0)
        {
            TempData["Error"] = $"Cannot delete \"{category.Name}\" because {category.Projects.Count} project(s) use it. Reassign those projects first.";
            return RedirectToAction(nameof(Categories));
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Category \"{category.Name}\" deleted.";
        return RedirectToAction(nameof(Categories));
    }

    // =========================================================================
    // Services
    // =========================================================================

    [HttpGet("/Admin/Services")]
    public async Task<IActionResult> Services()
    {
        var services = await _db.Services.OrderBy(s => s.DisplayOrder).ThenBy(s => s.ServiceId).ToListAsync();
        return View(services);
    }

    [HttpPost("/Admin/Services/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveService(ServiceFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a valid service title and description.";
            return RedirectToAction(nameof(Services));
        }

        if (model.ServiceId == 0)
        {
            _db.Services.Add(new ServiceItem
            {
                Title = model.Title.Trim(),
                Description = model.Description.Trim(),
                IconClass = string.IsNullOrWhiteSpace(model.IconClass) ? "bi-layers" : model.IconClass.Trim(),
                DisplayOrder = model.DisplayOrder,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            });
            TempData["Success"] = $"Service \"{model.Title.Trim()}\" added.";
        }
        else
        {
            var svc = await _db.Services.FindAsync(model.ServiceId);
            if (svc == null) return NotFound();
            svc.Title = model.Title.Trim();
            svc.Description = model.Description.Trim();
            svc.IconClass = string.IsNullOrWhiteSpace(model.IconClass) ? "bi-layers" : model.IconClass.Trim();
            svc.DisplayOrder = model.DisplayOrder;
            svc.IsActive = model.IsActive;
            TempData["Success"] = $"Service \"{svc.Title}\" updated.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Services));
    }

    [HttpPost("/Admin/Services/Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteService(int id)
    {
        var svc = await _db.Services.FindAsync(id);
        if (svc == null) return NotFound();

        _db.Services.Remove(svc);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Service \"{svc.Title}\" deleted.";
        return RedirectToAction(nameof(Services));
    }

    // =========================================================================
    // Site Profile / About Content
    // =========================================================================

    [HttpGet("/Admin/Profile")]
    public async Task<IActionResult> Profile()
    {
        var profile = await _db.SiteProfiles.OrderBy(p => p.ProfileId).FirstOrDefaultAsync()
            ?? new SiteProfile();

        var vm = new ProfileFormViewModel
        {
            ProfileId = profile.ProfileId,
            FullName = profile.FullName,
            Headline = profile.Headline,
            AboutSummary = profile.AboutSummary,
            AboutBio = profile.AboutBio,
            CurrentProfileImageUrl = profile.ProfileImageUrl,
            ResumeUrl = profile.ResumeUrl,
            Email = profile.Email,
            Phone = profile.Phone,
            Location = profile.Location,
            GitHubUrl = profile.GitHubUrl,
            LinkedInUrl = profile.LinkedInUrl
        };

        return View(vm);
    }

    [HttpPost("/Admin/Profile")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Profile(ProfileFormViewModel model)
    {
        var profile = await _db.SiteProfiles.OrderBy(p => p.ProfileId).FirstOrDefaultAsync();
        model.CurrentProfileImageUrl = profile?.ProfileImageUrl;

        if (!UrlSafetyHelper.IsValidSafeUrlOrEmpty(model.ResumeUrl))
            ModelState.AddModelError(nameof(model.ResumeUrl), "Resume URL must be a valid http(s) URL or #anchor.");

        if (!UrlSafetyHelper.IsValidSafeUrlOrEmpty(model.GitHubUrl))
            ModelState.AddModelError(nameof(model.GitHubUrl), "GitHub URL must start with http:// or https://.");

        if (!UrlSafetyHelper.IsValidSafeUrlOrEmpty(model.LinkedInUrl))
            ModelState.AddModelError(nameof(model.LinkedInUrl), "LinkedIn URL must start with http:// or https://.");

        if (model.ProfileImageFile != null && model.ProfileImageFile.Length > 0 &&
            !_storage.IsValidImage(model.ProfileImageFile, out var imgErr))
        {
            ModelState.AddModelError(nameof(model.ProfileImageFile), imgErr ?? "Invalid profile image.");
        }

        if (!ModelState.IsValid)
            return View(model);

        if (profile == null)
        {
            profile = new SiteProfile();
            _db.SiteProfiles.Add(profile);
        }

        if (model.ProfileImageFile != null && model.ProfileImageFile.Length > 0)
        {
            var oldUrl = profile.ProfileImageUrl;
            profile.ProfileImageUrl = await _storage.SaveImageAsync(model.ProfileImageFile, "profile");
            await _storage.DeleteImageAsync(oldUrl);
        }

        profile.FullName = model.FullName.Trim();
        profile.Headline = model.Headline.Trim();
        profile.AboutSummary = model.AboutSummary.Trim();
        profile.AboutBio = model.AboutBio.Trim();
        profile.ResumeUrl = model.ResumeUrl?.Trim();
        profile.Email = model.Email.Trim();
        profile.Phone = model.Phone?.Trim();
        profile.Location = model.Location?.Trim();
        profile.GitHubUrl = model.GitHubUrl?.Trim();
        profile.LinkedInUrl = model.LinkedInUrl?.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Site profile & About section updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    // =========================================================================
    // Contact Messages Inbox
    // =========================================================================

    [HttpGet("/Admin/Messages")]
    public async Task<IActionResult> Messages()
    {
        var messages = await _db.ContactMessages
            .OrderBy(m => m.IsRead)
            .ThenByDescending(m => m.SubmittedAt)
            .ToListAsync();
        return View(messages);
    }

    [HttpPost("/Admin/Messages/ToggleRead/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMessageRead(int id)
    {
        var msg = await _db.ContactMessages.FindAsync(id);
        if (msg == null) return NotFound();

        msg.IsRead = !msg.IsRead;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Messages));
    }

    [HttpPost("/Admin/Messages/Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var msg = await _db.ContactMessages.FindAsync(id);
        if (msg == null) return NotFound();

        _db.ContactMessages.Remove(msg);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Message deleted.";
        return RedirectToAction(nameof(Messages));
    }

    // =========================================================================
    // Admin Account Security
    // =========================================================================

    [HttpGet("/Admin/Security")]
    public async Task<IActionResult> Security()
    {
        var user = await GetCurrentAdminAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        return View(new ChangePasswordViewModel
        {
            FullName = user.FullName,
            Email = user.Email
        });
    }

    [HttpPost("/Admin/Security")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(ChangePasswordViewModel model)
    {
        var user = await GetCurrentAdminAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
            return View(model);

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var duplicateEmail = await _db.AdminUsers.AnyAsync(u => u.AdminUserId != user.AdminUserId && u.Email.ToLower() == normalizedEmail);
        if (duplicateEmail)
        {
            ModelState.AddModelError(nameof(model.Email), "Another admin account already uses this email.");
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.PasswordHash = _hasher.HashPassword(user, model.NewPassword);
        await _db.SaveChangesAsync();

        var claims = AccountController.BuildAdminClaims(user);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        TempData["Success"] = "Admin credentials updated successfully.";
        return RedirectToAction(nameof(Security));
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<AdminUser?> GetCurrentAdminAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return null;
        return await _db.AdminUsers.FindAsync(userId);
    }

    private void ValidateGalleryFiles(List<IFormFile>? files)
    {
        if (files == null) return;
        var nonEmpty = files.Where(f => f.Length > 0).ToList();
        if (nonEmpty.Count > 10)
        {
            ModelState.AddModelError(nameof(ProjectFormViewModel.GalleryFiles), "You can upload at most 10 gallery images at once.");
            return;
        }

        foreach (var file in nonEmpty)
        {
            if (!_storage.IsValidImage(file, out var err))
            {
                ModelState.AddModelError(nameof(ProjectFormViewModel.GalleryFiles), $"{file.FileName}: {err}");
                break;
            }
        }
    }

    private async Task<string> EnsureUniqueProjectSlugAsync(string? rawSlug, string title, int excludeProjectId = 0)
    {
        var baseSlug = SlugHelper.GenerateSlug(string.IsNullOrWhiteSpace(rawSlug) ? title : rawSlug);
        var candidate = baseSlug;
        var suffix = 2;

        while (await _db.Projects.AnyAsync(p => p.Slug == candidate && p.ProjectId != excludeProjectId))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    private async Task<string> EnsureUniqueCategorySlugAsync(string? rawSlug, string name, int excludeCategoryId = 0)
    {
        var baseSlug = SlugHelper.GenerateSlug(string.IsNullOrWhiteSpace(rawSlug) ? name : rawSlug);
        var candidate = baseSlug;
        var suffix = 2;

        while (await _db.Categories.AnyAsync(c => c.Slug == candidate && c.CategoryId != excludeCategoryId))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }
}
