using CvManagementSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace CvManagementSystem.Controllers;

public class SearchController : Controller
{
    private readonly SearchService _search;
    public SearchController(SearchService search) => _search = search;

    public async Task<IActionResult> Index(string q)
    {
        var includeCvs = User.IsInRole("Recruiter") || User.IsInRole("Administrator");
        var results = await _search.SearchAsync(q, includeCvs);
        ViewBag.Query = q;
        return View(results);
    }
}
