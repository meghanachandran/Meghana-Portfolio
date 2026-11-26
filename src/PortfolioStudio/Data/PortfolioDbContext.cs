using Microsoft.EntityFrameworkCore;
using PortfolioStudio.Models;

namespace PortfolioStudio.Data;

public class PortfolioDbContext : DbContext
{
    public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : base(options)
    {
    }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<SiteProfile> SiteProfiles => Set<SiteProfile>();
    public DbSet<ServiceItem> Services => Set<ServiceItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectImage> ProjectImages => Set<ProjectImage>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => new { e.IsPublished, e.IsFeatured, e.DisplayOrder, e.CreatedAt });

            entity.HasOne(p => p.Category)
                .WithMany(c => c.Projects)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.CreatedByUser)
                .WithMany(u => u.Projects)
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectImage>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.DisplayOrder });

            entity.HasOne(i => i.Project)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.HasIndex(e => new { e.IsRead, e.SubmittedAt });
        });
    }
}
