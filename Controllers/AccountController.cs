using CvManagementSystem.Models;
using CvManagementSystem.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Claims;
using System.Text;

namespace CvManagementSystem.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
        IEmailSender emailSender, IConfiguration config)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _emailSender = emailSender;
        _config = config;
    }

  
    private bool RequireEmailConfirmation => _config.GetValue("RequireEmailConfirmation", true);

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string email, string password, string firstName, string lastName)
    {
        var user = new ApplicationUser { UserName = email, Email = email, FirstName = firstName, LastName = lastName };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View();
        }

        await _userManager.AddToRoleAsync(user, "Candidate"); 

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmUrl = Url.Action(nameof(ConfirmEmail), "Account",
            new { userId = user.Id, token = encodedToken }, Request.Scheme);

        await _emailSender.SendAsync(email, "Confirm your CV Management account",
            $"<p>Welcome, {firstName}! Please confirm your account:</p><p><a href=\"{confirmUrl}\">Confirm email</a></p>");

        if (!RequireEmailConfirmation)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            ApplyUserPreferenceCookies(user);
            return RedirectToAction("Index", "Home");
        }

        return RedirectToAction(nameof(RegisterConfirmationSent));
    }

    [HttpGet]
    public IActionResult RegisterConfirmationSent() => View();

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return RedirectToAction(nameof(Login));

        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
        {
            TempData["Error"] = "This confirmation link is invalid or has expired.";
            return RedirectToAction(nameof(Login));
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        ApplyUserPreferenceCookies(user);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null && user.IsBlocked)
        {
            ModelState.AddModelError(string.Empty, "This account has been blocked.");
            return View();
        }
       
        if (RequireEmailConfirmation && user is not null && !user.EmailConfirmed && await _userManager.HasPasswordAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Please confirm your email before logging in.");
            return View();
        }

        var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View();
        }
        ApplyUserPreferenceCookies(user!);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }


    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string returnUrl = "/")
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/")
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null) return RedirectToAction(nameof(Login));

        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        if (result.Succeeded)
        {
            var existing = await _userManager.FindByEmailAsync(
                info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email) ?? "");
            if (existing is not null) ApplyUserPreferenceCookies(existing);
            return LocalRedirect(returnUrl);
        }

        var email = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
        if (email is null) return RedirectToAction(nameof(Login));

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.GivenName),
                LastName = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Surname)
            };
            await _userManager.CreateAsync(user);
            await _userManager.AddToRoleAsync(user, "Candidate");
        }

        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: false);
        ApplyUserPreferenceCookies(user);
        return LocalRedirect(returnUrl);
    }

  

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is not null) { user.PreferredLanguage = culture; await _userManager.UpdateAsync(user); }
        }
        return LocalRedirect(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTheme(string theme, string returnUrl)
    {
        Response.Cookies.Append("theme", theme, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is not null) { user.PreferredTheme = theme; await _userManager.UpdateAsync(user); }
        }
        return LocalRedirect(returnUrl);
    }

    private void ApplyUserPreferenceCookies(ApplicationUser user)
    {
        var cookieOptions = new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) };
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(user.PreferredLanguage)), cookieOptions);
        Response.Cookies.Append("theme", user.PreferredTheme, cookieOptions);
    }
}
