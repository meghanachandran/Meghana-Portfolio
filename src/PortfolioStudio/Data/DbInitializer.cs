using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PortfolioStudio.Models;

namespace PortfolioStudio.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();

        await db.Database.EnsureCreatedAsync();

        var admin = await db.AdminUsers.FirstOrDefaultAsync();
        if (admin == null)
        {
            var adminEmail = config["AdminSeed:Email"] ?? "admin@portfoliostudio.dev";
            var adminPassword = config["AdminSeed:Password"] ?? "Admin@12345!";
            var adminName = config["AdminSeed:FullName"] ?? "Meghana Chandran";

            admin = new AdminUser
            {
                FullName = adminName,
                Email = adminEmail,
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
            db.AdminUsers.Add(admin);
            await db.SaveChangesAsync();
        }
        else if (admin.FullName is "Portfolio Admin" or "Alex Carter")
        {
            admin.FullName = "Meghana Chandran";
            await db.SaveChangesAsync();
        }

        var existingProfile = await db.SiteProfiles.OrderBy(p => p.ProfileId).FirstOrDefaultAsync();
        if (existingProfile == null)
        {
            db.SiteProfiles.Add(BuildMeghanaProfile());
            await db.SaveChangesAsync();
        }
        else if (existingProfile.FullName == "Alex Carter")
        {
            var updated = BuildMeghanaProfile();
            existingProfile.FullName = updated.FullName;
            existingProfile.Headline = updated.Headline;
            existingProfile.AboutSummary = updated.AboutSummary;
            existingProfile.AboutBio = updated.AboutBio;
            existingProfile.ProfileImageUrl = updated.ProfileImageUrl;
            existingProfile.ResumeUrl = updated.ResumeUrl;
            existingProfile.Email = updated.Email;
            existingProfile.Phone = updated.Phone;
            existingProfile.Location = updated.Location;
            existingProfile.GitHubUrl = updated.GitHubUrl;
            existingProfile.LinkedInUrl = updated.LinkedInUrl;
            existingProfile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        if (!await db.Services.AnyAsync())
        {
            db.Services.AddRange(BuildMeghanaServices());
            await db.SaveChangesAsync();
        }
        else if (await db.Services.AnyAsync(s => s.Title == "Full-Stack .NET Engineering"))
        {
            var oldServices = await db.Services.ToListAsync();
            db.Services.RemoveRange(oldServices);
            await db.SaveChangesAsync();
            db.Services.AddRange(BuildMeghanaServices());
            await db.SaveChangesAsync();
        }

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(BuildMeghanaCategories());
            await db.SaveChangesAsync();
        }

        if (!await db.Projects.AnyAsync())
        {
            await SeedMeghanaProjectsAsync(db, admin);
        }
        else if (await db.Projects.AnyAsync(p => p.Slug == "finpulse-treasury-suite"))
        {
            // One-time replacement of initial placeholder seed projects with Meghana's resume projects
            var oldSeedSlugs = new[]
            {
                "finpulse-treasury-suite",
                "veloce-headless-commerce",
                "aether-telemetry-gateway",
                "claritymed-clinical-portal"
            };
            var oldProjects = await db.Projects
                .Include(p => p.Images)
                .Where(p => oldSeedSlugs.Contains(p.Slug))
                .ToListAsync();

            db.Projects.RemoveRange(oldProjects);
            await db.SaveChangesAsync();

            var oldCats = await db.Categories.Where(c => c.Projects.Count == 0).ToListAsync();
            if (oldCats.Any())
            {
                db.Categories.RemoveRange(oldCats);
                await db.SaveChangesAsync();
            }

            foreach (var cat in BuildMeghanaCategories())
            {
                if (!await db.Categories.AnyAsync(c => c.Slug == cat.Slug))
                    db.Categories.Add(cat);
            }
            await db.SaveChangesAsync();

            await SeedMeghanaProjectsAsync(db, admin);
        }

        if (!await db.ContactMessages.AnyAsync())
        {
            db.ContactMessages.Add(new ContactMessage
            {
                SenderName = "Nandini Nair",
                SenderEmail = "nandini.nair@example.in",
                Subject = "Inquiry regarding ASP.NET Core MVC Role",
                MessageBody = "Hi Meghana, I reviewed your CrewConnect and Careeronix projects and would like to discuss an ASP.NET Full Stack opportunity in Kochi.",
                IsRead = false,
                SubmittedAt = DateTime.UtcNow.AddHours(-3)
            });
            await db.SaveChangesAsync();
        }
    }

    private static SiteProfile BuildMeghanaProfile() => new()
    {
        FullName = "Meghana Chandran",
        Headline = "ASP.NET Full Stack Developer building CRUD-driven web apps with C#, MVC & SQL Server.",
        AboutSummary = "MCA graduate from Thrissur, Kerala with hands-on training and project experience across ASP.NET Core MVC, ASP.NET Web Forms, SQL Server, Entity Framework, ADO.NET, JavaScript, and Bootstrap.",
        AboutBio = "I am an ASP.NET Full Stack Developer based in Thrissur, Kerala, holding an MCA from Rajiv Gandhi Institute of Technology, Kottayam and a BCA from Don Bosco College, Mannuthy.\n\nThrough training at ClaySys Technologies, Triocodes Technologies, and Luminar Technolab (Kochi), I have built end-to-end applications covering authentication, role-based access control, stored-procedure-driven SQL Server databases, and responsive front-end delivery using Razor Views, Bootstrap, JavaScript, and Angular.\n\nThis portfolio website itself is built with ASP.NET Core MVC, Entity Framework Core, and SQL—complete with an Admin Panel where every case study, service, category, and profile detail can be updated dynamically.",
        ProfileImageUrl = "/images/seed/avatar.svg",
        ResumeUrl = "#contact",
        Email = "meghanachandran.98@gmail.com",
        Phone = "+91 99471 35682",
        Location = "Thrissur, Kerala, India",
        GitHubUrl = "https://github.com/AdhithyaSomaraj/Meghana",
        LinkedInUrl = "https://www.linkedin.com/in/meghana-chandran-18a38b20a",
        UpdatedAt = DateTime.UtcNow
    };

    private static List<ServiceItem> BuildMeghanaServices() =>
    [
        new()
        {
            Title = "ASP.NET Core MVC & Web Forms",
            Description = "End-to-end C# web applications built with Razor Views, Entity Framework, ADO.NET, LINQ, OOP, and SOLID principles.",
            IconClass = "bi-layers",
            DisplayOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        },
        new()
        {
            Title = "SQL Server & Stored Procedures",
            Description = "Relational database design in SSMS, stored-procedure-driven CRUD workflows, dashboard statistics, and query optimization.",
            IconClass = "bi-database-check",
            DisplayOrder = 2,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        },
        new()
        {
            Title = "Responsive UI & Front-End",
            Description = "Mobile-first interfaces crafted with HTML5, CSS3, Bootstrap, JavaScript, AJAX, and Angular components.",
            IconClass = "bi-window-sidebar",
            DisplayOrder = 3,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        },
        new()
        {
            Title = "Authentication & Role-Based Access",
            Description = "Secure sign-in flows with Cookie/JWT authentication, role-based permissions, OTP password reset, and admin dashboards.",
            IconClass = "bi-shield-check",
            DisplayOrder = 4,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        }
    ];

    private static List<Category> BuildMeghanaCategories() =>
    [
        new() { Name = "ASP.NET Core MVC", Slug = "aspnet-core-mvc", DisplayOrder = 1, CreatedAt = DateTime.UtcNow },
        new() { Name = "Job & HR Portals", Slug = "job-hr-portals", DisplayOrder = 2, CreatedAt = DateTime.UtcNow },
        new() { Name = "ASP.NET Web Forms", Slug = "aspnet-web-forms", DisplayOrder = 3, CreatedAt = DateTime.UtcNow },
        new() { Name = "E-Commerce", Slug = "e-commerce", DisplayOrder = 4, CreatedAt = DateTime.UtcNow }
    ];

    private static async Task SeedMeghanaProjectsAsync(PortfolioDbContext db, AdminUser? admin)
    {
        var mvcCat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "aspnet-core-mvc")
            ?? await db.Categories.FirstAsync();
        var portalCat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "job-hr-portals")
            ?? mvcCat;
        var webFormsCat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "aspnet-web-forms")
            ?? mvcCat;
        var ecomCat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "e-commerce")
            ?? mvcCat;

        var projects = new List<Project>
        {
            new()
            {
                CategoryId = mvcCat.CategoryId,
                Title = "CrewConnect — Employee HR Portal",
                Slug = "crewconnect-employee-hr-portal",
                ShortDescription = "Role-based employee management portal with JWT authentication, dashboard statistics, and stored-procedure driven SQL Server workflows.",
                ThumbnailUrl = "/images/seed/project-crewconnect.svg",
                ClientName = "ClaySys Technologies Training Project",
                ProjectDate = new DateOnly(2026, 2, 10),
                TechStack = "ASP.NET Core MVC, C#, SQL Server, ADO.NET, JWT, Bootstrap",
                RepoUrl = "https://github.com/AdhithyaSomaraj/Meghana",
                CaseStudyChallenge = "HR teams needed a centralized portal to manage employee profiles, track department counts, and restrict sensitive operations by user role without relying on manual spreadsheets.",
                CaseStudySolution = "Built a role-based ASP.NET Core MVC application using Razor Views, Bootstrap, and JavaScript on the front end paired with C#, ADO.NET, and SQL Server stored procedures on the back end. Implemented JWT-based authentication, structured exception handling, and async/await request handlers following OOP and SOLID principles.",
                CaseStudyOutcome = "Delivered clean CRUD operations, live HR dashboard statistics, and reliable role-based access control separating admin and employee views.",
                IsPublished = true,
                IsFeatured = true,
                DisplayOrder = 1,
                CreatedBy = admin?.AdminUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-18),
                UpdatedAt = DateTime.UtcNow.AddDays(-2),
                Images = new List<ProjectImage>
                {
                    new() { ImageUrl = "/images/seed/project-crewconnect.svg", Caption = "CrewConnect Employee Directory & HR Statistics", DisplayOrder = 1 }
                }
            },
            new()
            {
                CategoryId = portalCat.CategoryId,
                Title = "Careeronix — Full-Stack Job Portal",
                Slug = "careeronix-job-portal",
                ShortDescription = "Dual-role recruitment portal connecting employers and job seekers with OTP-based password recovery and real-time application tracking.",
                ThumbnailUrl = "/images/seed/project-careeronix.svg",
                ClientName = "Triocodes Technologies, Kerala",
                ProjectDate = new DateOnly(2025, 5, 15),
                TechStack = "ASP.NET Web Forms, C#, SQL Server, SSMS, Bootstrap, JavaScript",
                RepoUrl = "https://github.com/AdhithyaSomaraj/Meghana",
                CaseStudyChallenge = "Job seekers often lacked visibility into where their applications stood after submitting resumes, while employers needed a straightforward way to post openings and review candidates.",
                CaseStudySolution = "Developed a full-stack job portal in Visual Studio using ASP.NET Web Forms, C#, Bootstrap, and JavaScript backed by an optimized SQL Server database in SSMS. Implemented separate role-based workflows for employers and job seekers, OTP-verified password resets, and real-time application status tracking.",
                CaseStudyOutcome = "Enabled employers to manage job listings efficiently and gave candidates transparent status updates from application submission through shortlisting.",
                IsPublished = true,
                IsFeatured = true,
                DisplayOrder = 2,
                CreatedBy = admin?.AdminUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-32),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
                Images = new List<ProjectImage>
                {
                    new() { ImageUrl = "/images/seed/project-careeronix.svg", Caption = "Careeronix Job Listings & OTP Recovery Flow", DisplayOrder = 1 }
                }
            },
            new()
            {
                CategoryId = portalCat.CategoryId,
                Title = "HireSync — Job Application Management System",
                Slug = "hiresync-application-management",
                ShortDescription = "Online recruitment management platform built with ASP.NET MVC and SQL Server stored procedures for reviewing applicants and updating statuses.",
                ThumbnailUrl = "/images/seed/project-hiresync.svg",
                ClientName = "Recruitment Workflow Project",
                ProjectDate = new DateOnly(2024, 9, 20),
                TechStack = "ASP.NET MVC, C#, SQL Server, Stored Procedures, HTML, CSS, JavaScript",
                RepoUrl = "https://github.com/AdhithyaSomaraj/Meghana",
                CaseStudyChallenge = "Recruiters reviewing high volumes of applications needed structured interfaces to filter candidates, view applicant records, and update hiring stages without slow database queries.",
                CaseStudySolution = "Engineered an ASP.NET MVC platform utilizing SQL Server stored procedures for efficient applicant retrieval and status transitions. Designed clean recruiter views in HTML, CSS, and JavaScript to view applicant details and update statuses across hiring stages.",
                CaseStudyOutcome = "Streamlined applicant data management with fast stored-procedure queries and a clear hiring status workflow.",
                IsPublished = true,
                IsFeatured = false,
                DisplayOrder = 3,
                CreatedBy = admin?.AdminUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-48),
                UpdatedAt = DateTime.UtcNow.AddDays(-8),
                Images = new List<ProjectImage>
                {
                    new() { ImageUrl = "/images/seed/project-hiresync.svg", Caption = "HireSync Applicant Pipeline & Status Board", DisplayOrder = 1 }
                }
            },
            new()
            {
                CategoryId = ecomCat.CategoryId,
                Title = "HomeEase — Home Appliances E-Commerce",
                Slug = "homeease-ecommerce-platform",
                ShortDescription = "Home appliances e-commerce storefront enabling category browsing, product specification views, and streamlined order purchases.",
                ThumbnailUrl = "/images/seed/project-homeease.svg",
                ClientName = "E-Commerce Academic & Training Build",
                ProjectDate = new DateOnly(2024, 6, 12),
                TechStack = "ASP.NET Web Forms, C#, ADO.NET, SQL Server, Bootstrap, JavaScript",
                RepoUrl = "https://github.com/AdhithyaSomaraj/Meghana",
                CaseStudyChallenge = "Shoppers browsing home appliances needed an intuitive storefront organized by appliance categories, clear pricing/product specifications, and a straightforward purchase flow.",
                CaseStudySolution = "Created an e-commerce platform using HTML, CSS, JavaScript, Bootstrap, and ASP.NET Web Forms connected to SQL Server via ADO.NET. Implemented dynamic category browsing, detailed product pages, and purchase handling.",
                CaseStudyOutcome = "Provided a clean, responsive catalog and purchase workflow for cooling, laundry, and kitchen home appliances.",
                IsPublished = true,
                IsFeatured = false,
                DisplayOrder = 4,
                CreatedBy = admin?.AdminUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-64),
                UpdatedAt = DateTime.UtcNow.AddDays(-12),
                Images = new List<ProjectImage>
                {
                    new() { ImageUrl = "/images/seed/project-homeease.svg", Caption = "HomeEase Category Catalog & Product Cards", DisplayOrder = 1 }
                }
            }
        };

        db.Projects.AddRange(projects);
        await db.SaveChangesAsync();
    }
}
