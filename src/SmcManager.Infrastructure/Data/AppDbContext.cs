using Microsoft.EntityFrameworkCore;
using SmcManager.Core.Models;

namespace SmcManager.Infrastructure.Data;

/// <summary>
/// Контекст SQLite для контента, тегов и аккаунтов.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ContentItem> ContentItems => Set<ContentItem>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<ContentTag> Tags => Set<ContentTag>();

    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.MediaFiles).WithOne().HasForeignKey(m => m.ContentItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MediaFile>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<ContentTag>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<SocialAccount>(e => e.HasKey(x => x.Id));
    }
}
