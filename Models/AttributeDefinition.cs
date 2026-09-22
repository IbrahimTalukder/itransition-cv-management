using System.ComponentModel.DataAnnotations;

namespace CvManagementSystem.Models;

public enum AttributeType
{
    String,     
    Text,      
    Image,      
    Numeric,
    Date,
    Period,   
    Boolean,
    OneOfMany   

public enum AttributeCategory
{
    Certification,
    DomainKnowledge,
    PersonalInformation,
    SoftSkills,
    TechnicalSkills,
    Language,
    Other
}


public class AttributeDefinition
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty; 

    [MaxLength(1000)]
    public string? Description { get; set; }

    public AttributeCategory Category { get; set; }
    public AttributeType Type { get; set; }

  
    public ICollection<AttributeOption> Options { get; set; } = new List<AttributeOption>();

    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? RegexPattern { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    public bool IsBuiltIn { get; set; } = false; 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; } 

  
    [ConcurrencyCheck]
    public int Version { get; set; } = 0;

    public ICollection<PositionAttribute> PositionAttributes { get; set; } = new List<PositionAttribute>();
    public ICollection<UserAttributeValue> UserValues { get; set; } = new List<UserAttributeValue>();
  }

public class AttributeOption
  {
    public int Id { get; set; }
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
   }
