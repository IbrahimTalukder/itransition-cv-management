using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Controllers;

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Users()
    {
        var users = await _db.Users.OrderBy(u => u.UserName).ToListAsync();

     
        var roleNames = await _db.UserRoles
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(g => g.Key, g => (IList<string>)g.Select(x => x.Name!).ToList());

        ViewBag.Roles = roleNames;
        return View(users);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkAction(string action, List<string> userIds, string? role)
    {
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();

        switch (action)
        {
            case "block":
                users.ForEach(u => u.IsBlocked = true);
                await _db.SaveChangesAsync();
                break;
            case "unblock":
                users.ForEach(u => u.IsBlocked = false);
                await _db.SaveChangesAsync();
                break;
            case "delete":
                _db.Users.RemoveRange(users); 
                await _db.SaveChangesAsync();
                break;
            case "addrole":
            case "removerole":
                if (string.IsNullOrWhiteSpace(role)) break;
          
                foreach (var user in users)
                {
                    if (action == "addrole") await _userManager.AddToRoleAsync(user, role);
                    else await _userManager.RemoveFromRoleAsync(user, role);
                }
                break;
        }

        return RedirectToAction(nameof(Users));
    }
}
