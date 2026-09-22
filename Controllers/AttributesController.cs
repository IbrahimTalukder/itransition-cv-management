using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Controllers;

[Authorize(Roles = "Recruiter,Administrator")]
public class AttributesController : Controller
{
    private readonly ApplicationDbContext _db;
    public AttributesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? prefix, AttributeCategory? category)
    {
        var query = _db.AttributeDefinitions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(prefix))
            query = query.Where(a => a.Name.StartsWith(prefix)); 

        if (category is not null)
            query = query.Where(a => a.Category == category);

        ViewBag.Prefix = prefix;
        ViewBag.Category = category;

        var items = await query.OrderByDescending(a => a.LastUsedAt).ThenBy(a => a.Name).ToListAsync();
        return View(items);
    }

    public IActionResult Create() => View(new AttributeDefinition());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AttributeDefinition model, List<string>? options)
    {
        if (!ModelState.IsValid) return View(model);

        var nameTaken = await _db.AttributeDefinitions.AnyAsync(a => a.Name == model.Name);
        if (nameTaken)
        {
            ModelState.AddModelError(nameof(model.Name), "An attribute with this name already exists.");
            return View(model);
        }

        if (model.Type == AttributeType.OneOfMany && options is not null)
        {
            var order = 0;
            model.Options = options.Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => new AttributeOption { Value = o, SortOrder = order++ }).ToList();
        }

        _db.AttributeDefinitions.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Attribute created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var attr = await _db.AttributeDefinitions.Include(a => a.Options).FirstOrDefaultAsync(a => a.Id == id);
        if (attr is null) return NotFound();
        return View(attr);
    }

    
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AttributeDefinition model, int version)
    {
        if (id != model.Id) return BadRequest();

        _db.Attach(model);
        _db.Entry(model).Property(a => a.Version).OriginalValue = version;
        model.Version = version + 1;
        _db.Entry(model).State = EntityState.Modified;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty,
                "This attribute was modified by someone else. Please reload and re-apply your changes.");
            return View(model);
        }

        TempData["Success"] = "Attribute updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var attr = await _db.AttributeDefinitions.FindAsync(id);
        if (attr is null) return NotFound();
        if (attr.IsBuiltIn)
        {
            TempData["Error"] = "Built-in attributes cannot be removed.";
            return RedirectToAction(nameof(Index));
        }
        _db.AttributeDefinitions.Remove(attr);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }


    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Lookup(string? prefix, AttributeCategory? category, bool recentOnly = false)
    {
        var query = _db.AttributeDefinitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(prefix)) query = query.Where(a => a.Name.StartsWith(prefix));
        if (category is not null) query = query.Where(a => a.Category == category);
        if (recentOnly) query = query.Where(a => a.LastUsedAt != null).OrderByDescending(a => a.LastUsedAt);
        else query = query.OrderBy(a => a.Name);

        var results = await query.Take(20)
            .Select(a => new { a.Id, a.Name, a.Category, a.Type })
            .ToListAsync();
        return Json(results);
    }
}
