using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Controllers;

public class HomeViewModel
{
    public required List<Position> LatestPositions { get; init; }
    public required List<(Position Position, int CvCount)> PopularPositions { get; init; }
    public required List<Tag> TagCloud { get; init; }
    public required int CvsLast24h { get; init; }
    public required int TotalPositions { get; init; }
    public required int TotalCandidates { get; init; }
    public required int TotalRecruiters { get; init; }
    public required int TotalCvs { get; init; }
}

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    public HomeController(ApplicationDbContext db) => _db = db;

    [AllowAnonymous] 
    public async Task<IActionResult> Index()
    {
        var latest = await _db.Positions.OrderByDescending(p => p.UpdatedAt).Take(10).ToListAsync();

        var popular = await _db.Positions
            .Select(p => new { Position = p, CvCount = p.Cvs.Count(c => c.Status == CvStatus.Published) })
            .OrderByDescending(x => x.CvCount)
            .Take(5)
            .ToListAsync();

        var tagCloud = await _db.Tags.OrderByDescending(t => t.UsageCount).Take(40).ToListAsync();

        var since = DateTime.UtcNow.AddHours(-24);
        var cvsLast24h = await _db.Cvs.CountAsync(c => c.CreatedAt >= since);

        var vm = new HomeViewModel
        {
            LatestPositions = latest,
            PopularPositions = popular.Select(x => (x.Position, x.CvCount)).ToList(),
            TagCloud = tagCloud,
            CvsLast24h = cvsLast24h,
            TotalPositions = await _db.Positions.CountAsync(),
            TotalCandidates = await _db.UserRoles
                .Join(_db.Roles.Where(r => r.Name == "Candidate"), ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId)
                .CountAsync(),
            TotalRecruiters = await _db.UserRoles
                .Join(_db.Roles.Where(r => r.Name == "Recruiter"), ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId)
                .CountAsync(),
            TotalCvs = await _db.Cvs.CountAsync(c => c.Status == CvStatus.Published)
        };

        return View(vm);
    }

    public IActionResult Error() => View();
}
