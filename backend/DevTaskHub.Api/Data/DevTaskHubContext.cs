using DevTaskHub.Api.Models;
using Microsoft.EntityFrameworkCore;
using TaskPriority = DevTaskHub.Api.Models.TaskPriority;
using TaskStatus = DevTaskHub.Api.Models.TaskStatus;

namespace DevTaskHub.Api.Data;

public class DevTaskHubContext(DbContextOptions<DevTaskHubContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<ProjectInvitation> ProjectInvitations => Set<ProjectInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(pm => new { pm.ProjectId, pm.UserId });
            entity.Property(pm => pm.Role)
                .HasConversion<int>();
            entity.HasOne(pm => pm.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(pm => pm.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(pm => pm.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(200);
            entity.HasIndex(u => u.Email)
                .IsUnique();
            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(p => p.Description)
                .HasMaxLength(2000);
            entity.Property(p => p.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(p => p.Owner)
                .WithMany(u => u.Projects)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(t => t.Description)
                .HasMaxLength(2000);
            entity.Property(t => t.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(t => t.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(t => t.Priority)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(t => t.DueDate);
            entity.Property(t => t.Labels)
                .HasConversion(
                    v => string.Join(',', v),
                    v => string.IsNullOrWhiteSpace(v) ? new List<string>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList())
                .HasMaxLength(500);
            entity.Property(t => t.CompletedLate)
                .HasDefaultValue(false);
            entity.HasOne(t => t.AssignedTo)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(t => t.Checklist)
                .WithOne(c => c.TaskItem)
                .HasForeignKey(c => c.TaskItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskChecklistItem>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(c => c.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ProjectInvitation>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Role)
                .HasConversion<int>();
            entity.Property(i => i.Status)
                .HasConversion<int>();
            entity.Property(i => i.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(i => i.Project)
                .WithMany(p => p.Invitations)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
