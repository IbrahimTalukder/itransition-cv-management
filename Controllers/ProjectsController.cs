using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Controllers;

[Authorize(Roles = "Candidate,Administrator")]
public class ProjectsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProjectsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, DateTime periodStart, DateTime? periodEnd,
        string? descriptionMarkdown, string[]? tags)
    {
        var userId = _userManager.GetUserId(User)!;
        var project = new Project
        {
            UserId = userId,
            Name = name,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            DescriptionMarkdown = descriptionMarkdown
        };

        await AttachTagsAsync(project, tags);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return RedirectToAction("Index", "Profile");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, DateTime periodStart, DateTime? periodEnd,
        string? descriptionMarkdown, string[]? tags, int version)
    {
        var userId = _userManager.GetUserId(User)!;
        var project = await _db.Projects.Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project is null) return NotFound();

        _db.Entry(project).Property(p => p.Version).OriginalValue = version;
        project.Version = version + 1;
        project.Name = name;
        project.PeriodStart = periodStart;
        project.PeriodEnd = periodEnd;
        project.DescriptionMarkdown = descriptionMarkdown;

        _db.ProjectTags.RemoveRange(project.Tags);
        project.Tags.Clear();
        await AttachTagsAsync(project, tags);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "This project was changed elsewhere. Please reload.";
        }

        return RedirectToAction("Index", "Profile");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project is not null)
        {
            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index", "Profile");
    }

   
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> TagSuggestions(string? prefix)
    {
        var query = _db.Tags.AsQueryable();
        if (!string.IsNullOrWhiteSpace(prefix)) query = query.Where(t => t.Name.StartsWith(prefix));
        var tags = await query.OrderByDescending(t => t.UsageCount).Take(15).Select(t => t.Name).ToListAsync();
        return Json(tags);
    }

    private async Task AttachTagsAsync(Project project, string[]? tags)
    {
        if (tags is null) return;
        foreach (var raw in tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()))
        {
            var master = await _db.Tags.FirstOrDefaultAsync(t => t.Name == raw);
            if (master is null)
            {
                master = new Tag { Name = raw, UsageCount = 0 };
                _db.Tags.Add(master);
            }
            master.UsageCount++;
            project.Tags.Add(new ProjectTag { TagName = raw });
        }
    }
}
