using System.ComponentModel.DataAnnotations;

namespace CvManagementSystem.Models;

public enum PositionAccessType
{
    Public,      
    Restricted   
}

public enum PositionLevel { Junior, Middle, Senior, CLevel }


public class Position
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ShortDescription { get; set; }

    [MaxLength(150)]
    public string? Company { get; set; }
    public PositionLevel? Level { get; set; }

    public PositionAccessType AccessType { get; set; } = PositionAccessType.Public;

    public int MaxProjectsInCv { get; set; } = 3;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PositionAttribute> Attributes { get; set; } = new List<PositionAttribute>();
    public ICollection<PositionAccessRule> AccessRules { get; set; } = new List<PositionAccessRule>();
    public ICollection<PositionProjectTag> RelevantProjectTags { get; set; } = new List<PositionProjectTag>();
    public ICollection<Cv> Cvs { get; set; } = new List<Cv>();
    public ICollection<DiscussionPost> DiscussionPosts { get; set; } = new List<DiscussionPost>();

    
    [ConcurrencyCheck]
    public int Version { get; set; } = 0;
}

public class PositionAttribute
{
    public int Id { get; set; }
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;
    public bool IsRequiredForPublish { get; set; } = true;
    public int SortOrder { get; set; }
}

public class PositionProjectTag
{
    public int Id { get; set; }
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    [Required, MaxLength(60)]
    public string TagName { get; set; } = string.Empty;
}

public enum AccessRuleOperator
{
    Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, IsChecked, IsNotChecked
}


public class PositionAccessRule
{
    public int Id { get; set; }
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;

    public AccessRuleOperator Operator { get; set; }

 
    public string? StringValue { get; set; }
    public decimal? NumericValue { get; set; }
    public DateTime? DateValue { get; set; }
    public int? OptionValueId { get; set; }
}
