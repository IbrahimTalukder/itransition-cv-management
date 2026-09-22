using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Services;


public class AccessRuleEvaluator
{
    private readonly ApplicationDbContext _db;

    public AccessRuleEvaluator(ApplicationDbContext db) => _db = db;

    public async Task<bool> CandidateHasAccessAsync(string candidateId, Position position)
    {
        if (position.AccessType == PositionAccessType.Public)
            return true;

        if (position.AccessRules.Count == 0)
            return true; 
      
        var values = await _db.UserAttributeValues
            .Where(v => v.UserId == candidateId)
            .ToListAsync();

        foreach (var rule in position.AccessRules)
        {
            var value = values.FirstOrDefault(v => v.AttributeDefinitionId == rule.AttributeDefinitionId);
            if (!EvaluateRule(rule, value))
                return false; 
        }

        return true;
    }

    public static bool EvaluateRule(PositionAccessRule rule, UserAttributeValue? value)
    {
        if (value is null) return false;

        return rule.Operator switch
        {
            AccessRuleOperator.IsChecked => value.BooleanValue == true,
            AccessRuleOperator.IsNotChecked => value.BooleanValue == false,
            AccessRuleOperator.Equals => EqualsCheck(rule, value),
            AccessRuleOperator.NotEquals => !EqualsCheck(rule, value),
            AccessRuleOperator.GreaterThan => value.NumericValue > rule.NumericValue,
            AccessRuleOperator.GreaterThanOrEqual => value.NumericValue >= rule.NumericValue,
            AccessRuleOperator.LessThan => value.NumericValue < rule.NumericValue,
            AccessRuleOperator.LessThanOrEqual => value.NumericValue <= rule.NumericValue,
            _ => false
        };
    }

    public async Task RefreshCvAccessFlagAsync(Cv cv)
    {
        var position = await _db.Positions.Include(p => p.AccessRules)
            .FirstOrDefaultAsync(p => p.Id == cv.PositionId);
        if (position is null) return;

        var hasAccess = await CandidateHasAccessAsync(cv.CandidateId, position);
        if (cv.IsHiddenDueToLostAccess == !hasAccess) return; 

        cv.IsHiddenDueToLostAccess = !hasAccess;
        await _db.SaveChangesAsync();
    }

    private static bool EqualsCheck(PositionAccessRule rule, UserAttributeValue value)
    {
        if (rule.OptionValueId is not null) return value.SelectedOptionId == rule.OptionValueId;
        if (rule.StringValue is not null) return value.StringValue == rule.StringValue;
        if (rule.NumericValue is not null) return value.NumericValue == rule.NumericValue;
        if (rule.DateValue is not null) return value.DateValue == rule.DateValue;
        return false;
    }
}
