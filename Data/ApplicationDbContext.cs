using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KitchenAssistant.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    // Data Protection key ring, so auth cookies survive restarts and redeploys.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>(product =>
        {
            product.Property(p => p.Name).IsRequired().HasMaxLength(100);
            product.Property(p => p.NormalizedName).IsRequired().HasMaxLength(100);
            product.Property(p => p.Quantity).HasMaxLength(50);
            product.Property(p => p.Category).HasConversion<string>().HasMaxLength(20);
            product.Property(p => p.StorageLocation).HasConversion<string>().HasMaxLength(20);
            product.Property(p => p.ExpiresOn).HasColumnType("date");

            // Products belong to a user; no navigation on ApplicationUser.
            product.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            product.HasIndex(p => new { p.UserId, p.Category, p.NormalizedName }).IsUnique();
        });
    }
}
