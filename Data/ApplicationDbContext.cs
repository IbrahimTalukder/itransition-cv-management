using CvManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<AttributeOption> AttributeOptions => Set<AttributeOption>();
    public DbSet<UserAttributeValue> UserAttributeValues => Set<UserAttributeValue>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTag> ProjectTags => Set<ProjectTag>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<PositionAttribute> PositionAttributes => Set<PositionAttribute>();
    public DbSet<PositionAccessRule> PositionAccessRules => Set<PositionAccessRule>();
    public DbSet<PositionProjectTag> PositionProjectTags => Set<PositionProjectTag>();
    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<CvLike> CvLikes => Set<CvLike>();
    public DbSet<DiscussionPost> DiscussionPosts => Set<DiscussionPost>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

       
        builder.Entity<AttributeDefinition>()
            .HasIndex(a => a.Name)
            .IsUnique();

        builder.Entity<UserAttributeValue>()
            .HasIndex(v => new { v.UserId, v.AttributeDefinitionId })
            .IsUnique(); 

        builder.Entity<PositionAttribute>()
            .HasIndex(pa => new { pa.PositionId, pa.AttributeDefinitionId })
            .IsUnique();

   
        builder.Entity<Cv>()
            .HasIndex(c => new { c.CandidateId, c.PositionId })
            .IsUnique();

      
        builder.Entity<CvLike>()
            .HasIndex(l => new { l.CvId, l.RecruiterId })
            .IsUnique();


        builder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .IsUnique();

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("timestamp without time zone");
                }
            }
        }
    }
}