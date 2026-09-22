namespace CvManagementSystem.Models;

public enum BadgeKind
{
    ProjectsMilestone,  
    CvsMilestone,    
    LikesMilestone  
}

public record Badge(BadgeKind Kind, string Title, string Description, int Threshold, bool Achieved, int CurrentValue);
