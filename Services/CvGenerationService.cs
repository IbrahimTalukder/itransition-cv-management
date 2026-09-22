using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Services;

public class CvAttributeDisplayValue
{
    public required AttributeDefinition Definition { get; init; }
    public UserAttributeValue? Value { get; init; }
    public bool IsEmpty => Value is null || !Value.IsFilled;
}

public class CvViewModel
{
    public required Cv Cv { get; init; }
    public required Position Position { get; init; }
    public required List<CvAttributeDisplayValue> Attributes { get; init; }
    public required List<Project> Projects { get; init; }
    public bool CanPublish => Attributes.All(a => !a.IsEmpty);
}

public class CvGenerationService
{
    private readonly ApplicationDbContext _db;

    public CvGenerationService(ApplicationDbContext db) => _db = db;

    public async Task<CvViewModel> BuildAsync(Cv cv)
    {
        if (cv.Candidate is null)
            await _db.Entry(cv).Reference(c => c.Candidate).LoadAsync();

        var position = await _db.Positions
            .Include(p => p.Attributes).ThenInclude(pa => pa.AttributeDefinition).ThenInclude(a => a.Options)
            .Include(p => p.RelevantProjectTags)
            .FirstAsync(p => p.Id == cv.PositionId);

        var attributeIds = position.Attributes.Select(a => a.AttributeDefinitionId).ToList();

        var values = await _db.UserAttributeValues
            .Include(v => v.SelectedOption)
            .Where(v => v.UserId == cv.CandidateId && attributeIds.Contains(v.AttributeDefinitionId))
            .ToListAsync();

        var attributeRows = position.Attributes
            .OrderBy(pa => pa.SortOrder)
            .Select(pa => new CvAttributeDisplayValue
            {
                Definition = pa.AttributeDefinition,

                Value = values.FirstOrDefault(v => v.AttributeDefinitionId == pa.AttributeDefinitionId)
            })
            .ToList();

        var relevantTagNames = position.RelevantProjectTags.Select(t => t.TagName).ToHashSet();
        var candidateProjects = await _db.Projects
            .Include(p => p.Tags)
            .Where(p => p.UserId == cv.CandidateId)
            .ToListAsync();

        var filteredProjects = (relevantTagNames.Count == 0
                ? candidateProjects
                : candidateProjects.Where(p => p.Tags.Any(t => relevantTagNames.Contains(t.TagName))))
            .OrderByDescending(p => p.PeriodEnd ?? DateTime.MaxValue) 
            .ThenByDescending(p => p.PeriodStart) 
            .Take(position.MaxProjectsInCv)
            .ToList();

        return new CvViewModel
        {
            Cv = cv,
            Position = position,
            Attributes = attributeRows,
            Projects = filteredProjects
        };
    }

    public async Task<UserAttributeValue> SetAttributeValueAsync(
        string candidateId, int attributeDefinitionId, Action<UserAttributeValue> apply, int? expectedVersion)
    {
        var definition = await _db.AttributeDefinitions.FindAsync(attributeDefinitionId)
            ?? throw new InvalidOperationException("Unknown attribute.");

        var existing = await _db.UserAttributeValues
            .FirstOrDefaultAsync(v => v.UserId == candidateId && v.AttributeDefinitionId == attributeDefinitionId);

        if (existing is null)
        {
            existing = new UserAttributeValue { UserId = candidateId, AttributeDefinitionId = attributeDefinitionId };
            _db.UserAttributeValues.Add(existing);
        }
        else if (expectedVersion is not null)
        {
          
            _db.Entry(existing).Property(v => v.Version).OriginalValue = expectedVersion.Value;
            existing.Version = expectedVersion.Value + 1;
        }

        apply(existing);

        ValidateAgainstTuning(definition, existing);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
           
            throw new OptimisticLockException("This attribute was changed elsewhere. Please reload and retry.", ex);
        }

        return existing;
    }


    private static void ValidateAgainstTuning(AttributeDefinition def, UserAttributeValue value)
    {
        if (def.Type is AttributeType.String or AttributeType.Text)
        {
            var text = value.StringValue ?? value.TextValue;
            if (text is null) return;

            if (def.MinLength is { } min && text.Length < min)
                throw new AttributeValidationException($"'{def.Name}' must be at least {min} characters.");
            if (def.MaxLength is { } max && text.Length > max)
                throw new AttributeValidationException($"'{def.Name}' must be at most {max} characters.");
            if (!string.IsNullOrEmpty(def.RegexPattern) && !System.Text.RegularExpressions.Regex.IsMatch(text, def.RegexPattern))
                throw new AttributeValidationException($"'{def.Name}' does not match the required format.");
        }
        else if (def.Type == AttributeType.Numeric && value.NumericValue is { } num)
        {
            if (def.MinValue is { } min && num < min)
                throw new AttributeValidationException($"'{def.Name}' must be at least {min}.");
            if (def.MaxValue is { } max && num > max)
                throw new AttributeValidationException($"'{def.Name}' must be at most {max}.");
        }
    }
}

public class OptimisticLockException : Exception
{
    public OptimisticLockException(string message, Exception inner) : base(message, inner) { }
}

public class AttributeValidationException : Exception
{
    public AttributeValidationException(string message) : base(message) { }
}
