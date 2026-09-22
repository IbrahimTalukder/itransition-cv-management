using CvManagementSystem.Data;
using CvManagementSystem.Hubs;
using CvManagementSystem.Models;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CvManagementSystem.Controllers;

[Authorize]
public class DiscussionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<DiscussionHub> _hub;

    public DiscussionController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IHubContext<DiscussionHub> hub)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(int positionId, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return BadRequest();

        var userId = _userManager.GetUserId(User)!;
        var post = new DiscussionPost { PositionId = positionId, AuthorId = userId, ContentMarkdown = content };

        _db.DiscussionPosts.Add(post);
        await _db.SaveChangesAsync();

        var author = await _db.Users.FindAsync(userId);
        var html = Markdown.ToHtml(content); 

        await _hub.Clients.Group(DiscussionHub.GroupName(positionId)).SendAsync("NewPost", new
        {
            authorName = $"{author!.FirstName} {author.LastName}",
            authorId = author.Id,
            timestamp = post.CreatedAt,
            html
        });

        return Ok();
    }
}
