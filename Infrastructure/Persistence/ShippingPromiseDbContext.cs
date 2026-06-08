using Microsoft.EntityFrameworkCore;

namespace ShippingPromiseService.Infrastructure.Persistence;

public sealed class ShippingPromiseDbContext : DbContext
{
    public ShippingPromiseDbContext(DbContextOptions<ShippingPromiseDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShippingPromiseAudit> Audits => Set<ShippingPromiseAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShippingPromiseAudit>(entity =>
        {
            entity.ToTable("shipping_promise_audits");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.RequestJson)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(x => x.ResponseJson)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(x => x.CandidatesJson)
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .IsRequired();
        });
    }
}
