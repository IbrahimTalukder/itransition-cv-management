using System.ComponentModel.DataAnnotations;

namespace CvManagementSystem.Models;

public class UserAttributeValue
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;

    
    public string? StringValue { get; set; }
    public string? TextValue { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? NumericValue { get; set; }
    public DateTime? DateValue { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public bool? BooleanValue { get; set; }
    public int? SelectedOptionId { get; set; }
    public AttributeOption? SelectedOption { get; set; }

    public bool IsFilled =>
        StringValue is not null || TextValue is not null || ImageUrl is not null ||
        NumericValue is not null || DateValue is not null || PeriodStart is not null ||
        BooleanValue is not null || SelectedOptionId is not null;

  
    [ConcurrencyCheck]
    public int Version { get; set; } = 0;
}
