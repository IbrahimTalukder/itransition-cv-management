using System.ComponentModel.DataAnnotations;

namespace CvManagementSystem.Models;

public enum CvStatus
 {
    Draft,     
    Published
  }

public class Cv
 {
    public int Id { get; set; }

    public string CandidateId { get; set; } = string.Empty;
    public ApplicationUser Candidate { get; set; } = null!;

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public CvStatus Status { get; set; } = CvStatus.Draft;

  
    public bool IsHiddenDueToLostAccess { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CvLike> Likes { get; set; } = new List<CvLike>();

   
    [ConcurrencyCheck]
    public int Version { get; set; } = 0;
}

public class CvLike
{
    public int Id { get; set; }
    public int CvId { get; set; }
    public Cv Cv { get; set; } = null!;
    public string RecruiterId { get; set; } = string.Empty;
    public ApplicationUser Recruiter { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 
}
