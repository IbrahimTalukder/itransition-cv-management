using CvManagementSystem.Data;
using CvManagementSystem.Models;
using CvManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BadgeService _badges;

    public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, BadgeService badges)
    {
        _db = db;
        _userManager = userManager;
        _badges = badges;
    }


    public async Task<IActionResult> Index(string? id)
    {
        var targetId = id ?? _userManager.GetUserId(User)!;
        var isOwner = targetId == _userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Administrator");

        if (!isOwner && !isAdmin && !User.IsInRole("Recruiter")) return Forbid();

        var user = await _db.Users
            .Include(u => u.AttributeValues).ThenInclude(v => v.AttributeDefinition)
            .Include(u => u.Projects).ThenInclude(p => p.Tags)
            .Include(u => u.Cvs).ThenInclude(c => c.Position).ThenInclude(p => p.AccessRules)
            .Include(u => u.Cvs).ThenInclude(c => c.Likes)
            .FirstOrDefaultAsync(u => u.Id == targetId);
        if (user is null) return NotFound();

        if (isOwner || isAdmin)
        {
           
            var allValues = await _db.UserAttributeValues.Where(v => v.UserId == targetId).ToListAsync();
            foreach (var cv in user.Cvs)
            {
                var hasAccess = cv.Position.AccessType == PositionAccessType.Public
                    || cv.Position.AccessRules.Count == 0
                    || cv.Position.AccessRules.All(rule => AccessRuleEvaluator.EvaluateRule(rule,
                        allValues.FirstOrDefault(v => v.AttributeDefinitionId == rule.AttributeDefinitionId)));
                cv.IsHiddenDueToLostAccess = !hasAccess;
            }
            await _db.SaveChangesAsync();
        }

        ViewBag.CanEdit = isOwner || isAdmin; 
        ViewBag.Badges = await _badges.GetBadgesAsync(targetId);
        return View(user);
    }

    
    [AllowAnonymous]
   
    public async Task<IActionResult> BadgesSvg(string? id)
    {
        var targetId = id ?? _userManager.GetUserId(User)!;
        var badges = await _badges.GetBadgesAsync(targetId);
        var svg = _badges.RenderSvgPanel(badges);
        return File(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml");
    }

    
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSaveMe([FromBody] MeAutoSaveDto dto)
    {
        var userId = _userManager.GetUserId(User)!;
        if (dto.UserId != userId && !User.IsInRole("Administrator")) return Forbid();

        var user = await _db.Users.FindAsync(dto.UserId);
        if (user is null) return NotFound();

        _db.Entry(user).Property(u => u.Version).OriginalValue = dto.Version;
        user.Version = dto.Version + 1;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Location = dto.Location;
        user.PhotoUrl = dto.PhotoUrl;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {

            await _db.Entry(user).ReloadAsync();
            return Conflict(new
            {
                message = "Profile was changed elsewhere. Reloading latest version.",
                current = new { user.FirstName, user.LastName, user.Location, user.PhotoUrl, user.Version }
            });
        }

        return Ok(new { user.Version });
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInfoAttribute(int attributeDefinitionId)
    {
        var userId = _userManager.GetUserId(User)!;
        var exists = await _db.UserAttributeValues
            .AnyAsync(v => v.UserId == userId && v.AttributeDefinitionId == attributeDefinitionId);
        if (!exists)
        {
            _db.UserAttributeValues.Add(new UserAttributeValue { UserId = userId, AttributeDefinitionId = attributeDefinitionId });
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveInfoAttribute(int valueId)
    {
        var userId = _userManager.GetUserId(User)!;
        var value = await _db.UserAttributeValues.FirstOrDefaultAsync(v => v.Id == valueId && v.UserId == userId);
        if (value is not null)
        {
            _db.UserAttributeValues.Remove(value);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoSaveAttribute([FromBody] AttributeAutoSaveDto dto,
        [FromServices] CvGenerationService cvGen)
    {
        var userId = _userManager.GetUserId(User)!;
        try
        {
            var value = await cvGen.SetAttributeValueAsync(userId, dto.AttributeDefinitionId, v =>
            {
                v.StringValue = dto.StringValue;
                v.TextValue = dto.TextValue;
                v.NumericValue = dto.NumericValue;
                v.DateValue = dto.DateValue;
                v.BooleanValue = dto.BooleanValue;
                v.SelectedOptionId = dto.SelectedOptionId;
                v.PeriodStart = dto.PeriodStart;
                v.PeriodEnd = dto.PeriodEnd;
                v.ImageUrl = dto.ImageUrl;
            }, dto.Version);

            return Ok(new { value.Version });
        }
        catch (OptimisticLockException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (AttributeValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record MeAutoSaveDto(string UserId, string? FirstName, string? LastName, string? Location, string? PhotoUrl, int Version);

public record AttributeAutoSaveDto(int AttributeDefinitionId, string? StringValue, string? TextValue,
    decimal? NumericValue, DateTime? DateValue, bool? BooleanValue, int? SelectedOptionId,
    DateTime? PeriodStart, DateTime? PeriodEnd, string? ImageUrl, int? Version);