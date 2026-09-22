using Microsoft.AspNetCore.Identity;

namespace CvManagementSystem.Models;

public class ApplicationUser : IdentityUser
{
    public bool IsBlocked { get; set; } = false;

 
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredTheme { get; set; } = "light";

    
    [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
    public int Version { get; set; } = 0;

  
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Location { get; set; }
    public string? PhotoUrl { get; set; } 

    public ICollection<UserAttributeValue> AttributeValues { get; set; } = new List<UserAttributeValue>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Cv> Cvs { get; set; } = new List<Cv>();
    public ICollection<DiscussionPost> DiscussionPosts { get; set; } = new List<DiscussionPost>();
    public ICollection<CvLike> LikesGiven { get; set; } = new List<CvLike>();
}
