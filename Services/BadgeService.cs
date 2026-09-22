using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Services;

public class BadgeService
{
    private readonly ApplicationDbContext _db;
    public BadgeService(ApplicationDbContext db) => _db = db;

    private static readonly int[] ProjectThresholds = { 5, 10, 20 };
    private static readonly int[] CvThresholds = { 1, 5, 10 };
    private static readonly int[] LikeThresholds = { 5, 25, 50 };

    public async Task<List<Badge>> GetBadgesAsync(string userId)
    {
        var projectCount = await _db.Projects.CountAsync(p => p.UserId == userId);
        var cvCount = await _db.Cvs.CountAsync(c => c.CandidateId == userId);
        var likeCount = await _db.CvLikes.CountAsync(l => l.Cv.CandidateId == userId);

        var badges = new List<Badge>();
        foreach (var t in ProjectThresholds)
            badges.Add(new Badge(BadgeKind.ProjectsMilestone, $"{t} Projects",
                $"Added {t} or more projects to your profile", t, projectCount >= t, projectCount));
        foreach (var t in CvThresholds)
            badges.Add(new Badge(BadgeKind.CvsMilestone, $"{t} CV{(t > 1 ? "s" : "")}",
                $"Created {t} or more CVs", t, cvCount >= t, cvCount));
        foreach (var t in LikeThresholds)
            badges.Add(new Badge(BadgeKind.LikesMilestone, $"{t} Likes",
                $"Received {t} or more recruiter likes across your CVs", t, likeCount >= t, likeCount));

        return badges;
    }
    public string RenderSvgPanel(IEnumerable<Badge> badges)
    {
        var achieved = badges.Where(b => b.Achieved).ToList();
        var width = Math.Max(320, achieved.Count * 140 + 20);
        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='100' viewBox='0 0 {width} 100'>");
        sb.Append("<rect width='100%' height='100%' fill='#16233d' rx='8'/>");
        var x = 10;
        foreach (var b in achieved)
        {
            sb.Append($"<g transform='translate({x},10)'>");
            sb.Append("<rect width='130' height='80' rx='10' fill='#1f5148' stroke='#2b6e62'/>"); 
            sb.Append($"<text x='65' y='35' fill='#faf9f6' font-size='13' text-anchor='middle' font-family='Georgia,serif' font-weight='600'>{System.Net.WebUtility.HtmlEncode(b.Title)}</text>");
            sb.Append($"<text x='65' y='55' fill='#bcd8d1' font-size='10' text-anchor='middle' font-family='Segoe UI,Arial'>{b.CurrentValue} reached</text>");
            sb.Append("</g>");
            x += 140;
        }
        sb.Append("</svg>");
        return sb.ToString();
    }
}