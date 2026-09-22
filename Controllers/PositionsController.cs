using CvManagementSystem.Data;
using CvManagementSystem.Models;
using CvManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Controllers;

public class PositionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AccessRuleEvaluator _accessEvaluator;
    private readonly UserManager<ApplicationUser> _userManager;

    public PositionsController(ApplicationDbContext db, AccessRuleEvaluator accessEvaluator,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _accessEvaluator = accessEvaluator;
        _userManager = userManager;
    }


    [AllowAnonymous]
    public async Task<IActionResult> Index(string? tag)
    {
        var query = _db.Positions.Include(p => p.AccessRules).Include(p => p.RelevantProjectTags).AsQueryable();
        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(p => p.RelevantProjectTags.Any(t => t.TagName == tag));

        var positions = await query
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        ViewBag.TagFilter = tag;

        if (User.IsInRole("Candidate"))
        {
            var userId = _userManager.GetUserId(User)!;
            
            var values = await _db.UserAttributeValues.Where(v => v.UserId == userId).ToListAsync();

            var accessible = positions
                .Where(p => p.AccessType == PositionAccessType.Public
                    || p.AccessRules.Count == 0
                    || p.AccessRules.All(rule => AccessRuleEvaluator.EvaluateRule(rule,
                        values.FirstOrDefault(v => v.AttributeDefinitionId == rule.AttributeDefinitionId))))
                .Select(p => p.Id)
                .ToList();

            ViewBag.AccessiblePositionIds = accessible;
        }

        return View(positions);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var position = await _db.Positions
            .Include(p => p.Attributes).ThenInclude(a => a.AttributeDefinition)
            .Include(p => p.DiscussionPosts).ThenInclude(dp => dp.Author)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (position is null) return NotFound();
        return View(position);
    }

    [Authorize(Roles = "Recruiter,Administrator")]
    public IActionResult Create() => View(new Position());

    [Authorize(Roles = "Recruiter,Administrator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Position model, int[]? attributeIds, string[]? projectTags)
    {
        if (!ModelState.IsValid) return View(model);

        if (attributeIds is not null)
        {
            var order = 0;
            model.Attributes = attributeIds.Select(id => new PositionAttribute
            { AttributeDefinitionId = id, SortOrder = order++ }).ToList();

         
            await _db.AttributeDefinitions.Where(a => attributeIds.Contains(a.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastUsedAt, DateTime.UtcNow));
        }
        if (projectTags is not null)
            model.RelevantProjectTags = projectTags.Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => new PositionProjectTag { TagName = t }).ToList();

        _db.Positions.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

   
    [Authorize(Roles = "Recruiter,Administrator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        var source = await _db.Positions
            .Include(p => p.Attributes)
            .Include(p => p.AccessRules)
            .Include(p => p.RelevantProjectTags)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (source is null) return NotFound();

        var copy = new Position
        {
            Title = $"{source.Title} (Copy)",
            ShortDescription = source.ShortDescription,
            Company = source.Company,
            Level = source.Level,
            AccessType = source.AccessType,
            MaxProjectsInCv = source.MaxProjectsInCv,
            Attributes = source.Attributes.Select(a => new PositionAttribute
            { AttributeDefinitionId = a.AttributeDefinitionId, SortOrder = a.SortOrder, IsRequiredForPublish = a.IsRequiredForPublish }).ToList(),
            AccessRules = source.AccessRules.Select(r => new PositionAccessRule
            {
                AttributeDefinitionId = r.AttributeDefinitionId, Operator = r.Operator, StringValue = r.StringValue,
                NumericValue = r.NumericValue, DateValue = r.DateValue, OptionValueId = r.OptionValueId
            }).ToList(),
            RelevantProjectTags = source.RelevantProjectTags.Select(t => new PositionProjectTag { TagName = t.TagName }).ToList()
        };

        _db.Positions.Add(copy);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = copy.Id });
    }

    [Authorize(Roles = "Recruiter,Administrator")]
    public async Task<IActionResult> Edit(int id)
    {
        var position = await _db.Positions
            .Include(p => p.Attributes).ThenInclude(a => a.AttributeDefinition)
            .Include(p => p.AccessRules).ThenInclude(r => r.AttributeDefinition)
            .Include(p => p.RelevantProjectTags)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (position is null) return NotFound();
        return View(position);
    }

    [Authorize(Roles = "Recruiter,Administrator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Position model, int version, int[]? attributeIds)
    {
        if (id != model.Id) return BadRequest();

        model.UpdatedAt = DateTime.UtcNow;
        _db.Attach(model);
        _db.Entry(model).Property(p => p.Version).OriginalValue = version;
        model.Version = version + 1;
        _db.Entry(model).State = EntityState.Modified;

        
        if (attributeIds is not null)
        {
            var current = await _db.PositionAttributes.Where(pa => pa.PositionId == id).ToListAsync();
            var toRemove = current.Where(pa => !attributeIds.Contains(pa.AttributeDefinitionId));
            _db.PositionAttributes.RemoveRange(toRemove);

            var toAdd = attributeIds.Except(current.Select(pa => pa.AttributeDefinitionId));
            var order = current.Count;
            foreach (var attrId in toAdd)
                _db.PositionAttributes.Add(new PositionAttribute
                { PositionId = id, AttributeDefinitionId = attrId, SortOrder = order++ });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "This position was changed by another Recruiter. Please reload.");
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Recruiter,Administrator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var position = await _db.Positions.FindAsync(id);
        if (position is null) return NotFound();
        _db.Positions.Remove(position);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

  

    [Authorize(Roles = "Recruiter,Administrator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccessRule(int positionId, int attributeDefinitionId,
        AccessRuleOperator @operator, string? stringValue, decimal? numericValue, DateTime? dateValue, int? optionValueId)
    {
        var position = await _db.Positions.FindAsync(positionId);
        if (position is null) return NotFound();

        _db.PositionAccessRules.Add(new PositionAccessRule
        {
            PositionId = positionId,
            AttributeDefinitionId = attributeDefinitionId,
            Operator = @operator,
            StringValue = stringValue,
            NumericValue = numericValue,
            DateValue = dateValue,
            OptionValueId = optionValueId
        });


        position.AccessType = PositionAccessType.Restricted;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = positionId });
    }

    [Authorize(Roles = "Recruiter,Administrator"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAccessRule(int ruleId, int positionId)
    {
        var rule = await _db.PositionAccessRules.FindAsync(ruleId);
        if (rule is not null)
        {
            _db.PositionAccessRules.Remove(rule);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Edit), new { id = positionId });
    }


    [HttpGet, Authorize(Roles = "Recruiter,Administrator")]
    public async Task<IActionResult> AttributeOperators(int attributeDefinitionId)
    {
        var attr = await _db.AttributeDefinitions.Include(a => a.Options)
            .FirstOrDefaultAsync(a => a.Id == attributeDefinitionId);
        if (attr is null) return NotFound();

        var operators = attr.Type switch
        {
            AttributeType.Boolean => new[] { "IsChecked", "IsNotChecked" },
            AttributeType.Numeric or AttributeType.Date =>
                new[] { "Equals", "NotEquals", "GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual" },
            AttributeType.OneOfMany => new[] { "Equals", "NotEquals" },
            _ => new[] { "Equals", "NotEquals" }
        };

        return Json(new
        {
            type = attr.Type.ToString(),
            operators,
            options = attr.Options.OrderBy(o => o.SortOrder).Select(o => new { o.Id, o.Value })
        });
    }

    [Authorize(Roles = "Recruiter,Administrator")]
    public async Task<IActionResult> Cvs(int id)
    {
       
        var cvs = await _db.Cvs
            .Include(c => c.Candidate)
            .Include(c => c.Likes)
            .Where(c => c.PositionId == id && c.Status == CvStatus.Published && !c.IsHiddenDueToLostAccess)
            .ToListAsync();

        ViewBag.PositionId = id;
        return View(cvs);
    }

    [Authorize(Roles = "Recruiter,Administrator")]
    public async Task<IActionResult> ExportCvs(int id, [FromServices] CvExportService exportService)
    {
        var bytes = await exportService.ExportPositionCvsAsync(id);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"position-{id}-cvs.xlsx");
    }
}
