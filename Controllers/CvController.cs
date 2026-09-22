using CvManagementSystem.Data;
using CvManagementSystem.Models;
using CvManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Controllers;

[Authorize]
public class CvController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CvGenerationService _cvGen;
    private readonly AccessRuleEvaluator _accessEvaluator;

    public CvController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        CvGenerationService cvGen, AccessRuleEvaluator accessEvaluator)
    {
        _db = db;
        _userManager = userManager;
        _cvGen = cvGen;
        _accessEvaluator = accessEvaluator;
    }

    [Authorize(Roles = "Candidate")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int positionId)
    {
        var userId = _userManager.GetUserId(User)!;

        var already = await _db.Cvs.AnyAsync(c => c.CandidateId == userId && c.PositionId == positionId);
        if (already) return RedirectToAction(nameof(Details), new { id = (await _db.Cvs
            .FirstAsync(c => c.CandidateId == userId && c.PositionId == positionId)).Id });

        var position = await _db.Positions.Include(p => p.AccessRules).FirstOrDefaultAsync(p => p.Id == positionId);
        if (position is null) return NotFound();

        if (!await _accessEvaluator.CandidateHasAccessAsync(userId, position))
            return Forbid();

        var cv = new Cv { CandidateId = userId, PositionId = positionId };
        _db.Cvs.Add(cv);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = cv.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var cv = await _db.Cvs.Include(c => c.Candidate).FirstOrDefaultAsync(c => c.Id == id);
        if (cv is null) return NotFound();

        var userId = _userManager.GetUserId(User);
        var isOwner = cv.CandidateId == userId;
        var isRecruiterOrAdmin = User.IsInRole("Recruiter") || User.IsInRole("Administrator");
        var isAdmin = User.IsInRole("Administrator");


        await _accessEvaluator.RefreshCvAccessFlagAsync(cv);

        if (!isOwner && !isRecruiterOrAdmin) return Forbid();
        if (isRecruiterOrAdmin && !isOwner && cv.Status != CvStatus.Published && !isAdmin)
            return Forbid(); 
        
        if (cv.IsHiddenDueToLostAccess && !isAdmin)
            return Forbid();

        var vm = await _cvGen.BuildAsync(cv);

        ViewBag.CanEdit = isOwner || isAdmin;
        ViewBag.IsOwner = isOwner; 
        ViewBag.IsRecruiterView = isRecruiterOrAdmin && !isOwner;
        ViewBag.LikeCount = await _db.CvLikes.CountAsync(l => l.CvId == cv.Id);
        ViewBag.LikedByMe = User.IsInRole("Recruiter") &&
            await _db.CvLikes.AnyAsync(l => l.CvId == cv.Id && l.RecruiterId == userId);

        return View(vm);
    }

    [Authorize(Roles = "Candidate"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var cv = await _db.Cvs.FirstOrDefaultAsync(c => c.Id == id && c.CandidateId == userId);
        if (cv is null) return NotFound();

        var vm = await _cvGen.BuildAsync(cv);
        if (!vm.CanPublish)
        {
            TempData["Error"] = "All attributes must be filled before publishing.";
            return RedirectToAction(nameof(Details), new { id });
        }

        cv.Status = CvStatus.Published;
        cv.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Recruiter"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int id)
    {
        var recruiterId = _userManager.GetUserId(User)!;
        var like = await _db.CvLikes.FirstOrDefaultAsync(l => l.CvId == id && l.RecruiterId == recruiterId);
        if (like is null)
            _db.CvLikes.Add(new CvLike { CvId = id, RecruiterId = recruiterId });
        else
            _db.CvLikes.Remove(like);

        await _db.SaveChangesAsync();
        var count = await _db.CvLikes.CountAsync(l => l.CvId == id);
        return Json(new { liked = like is null, count });
    }

    [Authorize(Roles = "Candidate"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var cv = await _db.Cvs.FirstOrDefaultAsync(c => c.Id == id && c.CandidateId == userId);
        if (cv is not null)
        {
            _db.Cvs.Remove(cv);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction("Index", "Profile");
    }

    public async Task<IActionResult> ExportPdf(int id, [FromServices] CvPdfService pdfService)
    {
        var cv = await _db.Cvs.Include(c => c.Candidate).FirstOrDefaultAsync(c => c.Id == id);
        if (cv is null) return NotFound();

        var userId = _userManager.GetUserId(User);
        if (cv.CandidateId != userId && !User.IsInRole("Recruiter") && !User.IsInRole("Administrator"))
            return Forbid();

        var vm = await _cvGen.BuildAsync(cv);
        var pdf = pdfService.GeneratePdf(vm);
        return File(pdf, "application/pdf", $"cv-{cv.Id}.pdf");
    }
}
