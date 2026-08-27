using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rema.App.Data.Entities;
using Rema.App.Data.Tenancy;

namespace Rema.App.Data;

/// <summary>
/// Applikationens EF Core-kontekst. Rummer Identity-tabeller, Data Protection-nøgler
/// og butiksdata. Alle entiteter der implementerer <see cref="ITenantEntity"/> får
/// automatisk et globalt filter på den aktuelle butik.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IDataProtectionKeyContext
{
    private readonly ITenantProvider _tenantProvider = tenantProvider;

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<ProductCalculation> ProductCalculations => Set<ProductCalculation>();
    public DbSet<FloorPlan> FloorPlans => Set<FloorPlan>();
    public DbSet<FloorBox> FloorBoxes => Set<FloorBox>();

    /// <inheritdoc />
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Tidsordnede v7-GUID-nøgler, genereret klientside ved indsættelse.
        foreach (var key in new[]
        {
            builder.Entity<Store>().Property(x => x.Id),
            builder.Entity<ProductCalculation>().Property(x => x.Id),
            builder.Entity<FloorPlan>().Property(x => x.Id),
            builder.Entity<FloorBox>().Property(x => x.Id),
        })
        {
            key.HasValueGenerator<GuidV7ValueGenerator>().ValueGeneratedOnAdd();
        }

        builder.Entity<Store>(e =>
        {
            e.HasIndex(s => s.StoreNumber).IsUnique();
            e.Property(s => s.StoreNumber).IsRequired();
            e.Property(s => s.Name).IsRequired();
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Store)
                .WithMany(s => s.Users)
                .HasForeignKey(u => u.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(u => u.StoreId);
        });

        builder.Entity<ProductCalculation>(e =>
        {
            e.Property(p => p.CostExVat).HasPrecision(12, 4);
            e.Property(p => p.SalesPriceInclVat).HasPrecision(12, 4);
            e.Property(p => p.Deposit).HasPrecision(12, 4);
            e.Property(p => p.VatRate).HasPrecision(6, 4);
            e.Property(p => p.Contribution).HasPrecision(12, 4);
            e.Property(p => p.MarginPct).HasPrecision(9, 4);
            e.HasIndex(p => new { p.StoreId, p.CreatedUtc });
        });

        builder.Entity<FloorPlan>(e =>
        {
            e.Property(p => p.Name).IsRequired();
            e.HasIndex(p => new { p.StoreId, p.Name });
            e.HasMany(p => p.Boxes)
                .WithOne(b => b.FloorPlan!)
                .HasForeignKey(b => b.FloorPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FloorBox>(e =>
        {
            e.Property(b => b.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(b => new { b.StoreId, b.FloorPlanId });
        });

        // Globalt tenant-filter på alt butiks-ejet data.
        builder.Entity<ProductCalculation>()
            .HasQueryFilter(p => p.StoreId == _tenantProvider.StoreId);
        builder.Entity<FloorPlan>()
            .HasQueryFilter(p => p.StoreId == _tenantProvider.StoreId);
        builder.Entity<FloorBox>()
            .HasQueryFilter(b => b.StoreId == _tenantProvider.StoreId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantIds();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantIds();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Sætter <see cref="ITenantEntity.StoreId"/> på nye rækker, så kaldekoden
    /// ikke selv skal huske det – og så data ikke kan skrives til en anden butik.
    /// </summary>
    private void ApplyTenantIds()
    {
        var storeId = _tenantProvider.StoreId;
        if (storeId == Guid.Empty)
            return;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.StoreId == Guid.Empty)
                entry.Entity.StoreId = storeId;
        }
    }
}
