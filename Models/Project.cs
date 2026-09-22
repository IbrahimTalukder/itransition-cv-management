using System.ComponentModel.DataAnnotations;

namespace CvManagementSystem.Models;

public class Project
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; } 

    
    public string? DescriptionMarkdown { get; set; }

    public ICollection<ProjectTag> Tags { get; set; } = new List<ProjectTag>();

    [ConcurrencyCheck]
    public int Version { get; set; } = 0;
}

public class ProjectTag
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    [Required, MaxLength(60)]
    public string TagName { get; set; } = string.Empty; 
}


public class Tag
{
    public int Id { get; set; }
    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty; 
    public int UsageCount { get; set; } 
}
