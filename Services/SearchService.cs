using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Services;

public record SearchResult(string Type, int Id, string Title, string Subtitle, string Url, int? LikeCount = null);

public class SearchService
{
    private readonly ApplicationDbContext _db;
    public SearchService(ApplicationDbContext db) => _db = db;

    public async Task<List<SearchResult>> SearchAsync(string term, bool includeCvs)
    {
        if (string.IsNullOrWhiteSpace(term)) return new List<SearchResult>();

        var results = new List<SearchResult>();

        var positions = await _db.Positions
            .FromSqlInterpolated($@"
                SELECT * FROM ""Positions""
                WHERE ""SearchVector"" @@ plainto_tsquery('english', {term})")
            .Take(20)
            .ToListAsync();

        results.AddRange(positions.Select(p =>
            new SearchResult("Position", p.Id, p.Title, p.Company ?? "", $"/Positions/Details/{p.Id}")));

        if (includeCvs)
        {
            
            var candidateMatches = await _db.Users
                .FromSqlInterpolated($@"
                    SELECT * FROM ""AspNetUsers""
                    WHERE ""SearchVector"" @@ plainto_tsquery('english', {term})")
                .Select(u => u.Id)
                .ToListAsync();

            var attributeValueMatches = await _db.UserAttributeValues
                .FromSqlInterpolated($@"
                    SELECT * FROM ""UserAttributeValues""
                    WHERE ""SearchVector"" @@ plainto_tsquery('english', {term})")
                .Select(v => v.UserId)
                .Distinct()
                .ToListAsync();

            var candidateIds = candidateMatches.Union(attributeValueMatches).ToList();

            var cvs = await _db.Cvs
                .Include(c => c.Candidate)
                .Include(c => c.Position)
                .Include(c => c.Likes)
                .Where(c => c.Status == CvStatus.Published && !c.IsHiddenDueToLostAccess
                    && candidateIds.Contains(c.CandidateId))
                .Take(20)
                .ToListAsync();

            results.AddRange(cvs.Select(c =>
                new SearchResult("CV", c.Id, $"{c.Candidate.FirstName} {c.Candidate.LastName}",
                    c.Position.Title, $"/Cv/Details/{c.Id}", c.Likes.Count)));
        }

        return results;
    }
}
