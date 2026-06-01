using Microsoft.EntityFrameworkCore;
using ServiceRequest.Domain.Entities;
using ServiceRequest.Domain.Enums;

namespace ServiceRequest.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ServiceRequestEntity> ServiceRequests => Set<ServiceRequestEntity>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceRequestEntity>(e =>
        {
            e.ToTable("service_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Title).HasMaxLength(255).IsRequired();
            e.HasOne(x => x.Requester).WithMany(u => u.RequestsAsRequester)
                .HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Requestee).WithMany(u => u.RequestsAsRequestee)
                .HasForeignKey(x => x.RequesteeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });
    }
}
