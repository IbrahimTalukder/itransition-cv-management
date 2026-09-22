using CvManagementSystem.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CvManagementSystem.Services;

public class CvPdfService
{
    private readonly string _baseUrl;
    public CvPdfService(IConfiguration config) => _baseUrl = config["AppBaseUrl"] ?? "https://localhost:5001";

    public byte[] GeneratePdf(CvViewModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var qrUrl = $"{_baseUrl}/Cv/Details/{model.Cv.Id}";
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
        var qrPng = new PngByteQRCode(qrData).GetGraphic(10);

        var candidateName = $"{model.Cv.Candidate.FirstName} {model.Cv.Candidate.LastName}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(candidateName).FontSize(22).Bold();
                        col.Item().Text(model.Position.Title).FontSize(14).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(80).Image(qrPng);
                });

                page.Content().PaddingTop(20).Column(col =>
                {
                    foreach (var attr in model.Attributes)
                    {
                        col.Item().PaddingBottom(8).Row(r =>
                        {
                            r.RelativeItem(1).Text(attr.Definition.Name).SemiBold();
                            r.RelativeItem(2).Text(attr.IsEmpty ? "—" : FormatValue(attr));
                        });
                    }

                    if (model.Projects.Count > 0)
                    {
                        col.Item().PaddingTop(15).Text("Projects").FontSize(14).Bold();
                        foreach (var p in model.Projects)
                        {
                            col.Item().PaddingTop(5).Text(p.Name).SemiBold();
                            if (!string.IsNullOrWhiteSpace(p.DescriptionMarkdown))
                                col.Item().Text(p.DescriptionMarkdown);
                        }
                    }
                });

                page.Footer().AlignCenter().Text($"Generated via CV Management System — {qrUrl}")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });

        return document.GeneratePdf();
    }

    private static string FormatValue(CvAttributeDisplayValue attr)
    {
        var v = attr.Value!;
        return attr.Definition.Type switch
        {
            AttributeType.Boolean => v.BooleanValue == true ? "Yes" : "No",
            AttributeType.Date => v.DateValue?.ToString("yyyy-MM-dd") ?? "",
            AttributeType.Period => $"{v.PeriodStart:yyyy-MM-dd} — {v.PeriodEnd:yyyy-MM-dd}",
            AttributeType.OneOfMany => v.SelectedOption?.Value ?? "",
            AttributeType.Numeric => v.NumericValue?.ToString() ?? "",
            AttributeType.Image => "[image]",
            _ => v.StringValue ?? v.TextValue ?? ""
        };
    }
}
