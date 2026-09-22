using ClosedXML.Excel;
using CvManagementSystem.Data;
using CvManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Services;

public class CvExportService
{
    private readonly ApplicationDbContext _db;

    public CvExportService(ApplicationDbContext db) => _db = db;

    public async Task<byte[]> ExportPositionCvsAsync(int positionId)
    {
        var position = await _db.Positions
            .Include(p => p.Attributes).ThenInclude(a => a.AttributeDefinition).ThenInclude(a => a.Options)
            .FirstAsync(p => p.Id == positionId);

        var cvs = await _db.Cvs
            .Include(c => c.Candidate)
            .Where(c => c.PositionId == positionId && c.Status == CvStatus.Published)
            .ToListAsync();

        var attributeDefs = position.Attributes.OrderBy(a => a.SortOrder).Select(a => a.AttributeDefinition).ToList();
        var attributeIds = attributeDefs.Select(a => a.Id).ToList();
        var candidateIds = cvs.Select(c => c.CandidateId).ToList();
        var cvIds = cvs.Select(c => c.Id).ToList();


        var allValues = await _db.UserAttributeValues
            .Include(v => v.SelectedOption)
            .Where(v => candidateIds.Contains(v.UserId) && attributeIds.Contains(v.AttributeDefinitionId))
            .ToListAsync();

        var likeCounts = await _db.CvLikes
            .Where(l => cvIds.Contains(l.CvId))
            .GroupBy(l => l.CvId)
            .Select(g => new { CvId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CvId, x => x.Count);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(position.Title.Length > 28 ? position.Title[..28] : position.Title);

        sheet.Cell(1, 1).Value = "Candidate";
        sheet.Cell(1, 2).Value = "Likes";
        for (var i = 0; i < attributeDefs.Count; i++)
            sheet.Cell(1, 3 + i).Value = attributeDefs[i].Name;
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var cv in cvs)
        {
            sheet.Cell(row, 1).Value = $"{cv.Candidate.FirstName} {cv.Candidate.LastName}";
            sheet.Cell(row, 2).Value = likeCounts.GetValueOrDefault(cv.Id, 0);

            for (var i = 0; i < attributeDefs.Count; i++)
            {
                var value = allValues.FirstOrDefault(v => v.UserId == cv.CandidateId && v.AttributeDefinitionId == attributeDefs[i].Id);
                sheet.Cell(row, 3 + i).Value = value is null || !value.IsFilled ? "" : FormatForExport(attributeDefs[i], value);
            }
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string FormatForExport(AttributeDefinition def, UserAttributeValue v) => def.Type switch
    {
        AttributeType.Boolean => v.BooleanValue == true ? "Yes" : "No",
        AttributeType.Date => v.DateValue?.ToString("yyyy-MM-dd") ?? "",
        AttributeType.OneOfMany => v.SelectedOption?.Value ?? "",
        AttributeType.Numeric => v.NumericValue?.ToString() ?? "",
        _ => v.StringValue ?? v.TextValue ?? ""
    };
}
