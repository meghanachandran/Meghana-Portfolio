using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PortfolioStudio.Data;
using PortfolioStudio.Models;
using PortfolioStudio.ViewModels;

namespace PortfolioStudio.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AccountController : Controller
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly PortfolioDbContext _db;
    private readonly IPasswordHasher<AdminUser> _hasher;
    private readonly IMemoryCache _cache;

    public AccountController(
        PortfolioDbContext db,
        IPasswordHasher<AdminUser> hasher,
        IMemoryCache cache)
    {
        _db = db;
        _hasher = hasher;
        _cache = cache;
    }

    [HttpGet("/Account/Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
            return RedirectToAction("Index", "Admin");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/Account/Login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var lockoutKey = $"login_fail:{ip}:{email}";

        if (_cache.TryGetValue<int>(lockoutKey, out var failedCount) && failedCount >= MaxFailedAttempts)
        {
            ModelState.AddModelError(string.Empty, "Too many failed sign-in attempts. Please wait 5 minutes before trying again.");
            return View(model);
        }

        var user = await _db.AdminUsers.FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.IsActive);

        if (user == null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            _cache.Set(lockoutKey, failedCount + 1, LockoutDuration);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        _cache.Remove(lockoutKey);

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var claims = BuildAdminClaims(user);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProps = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 72 : 12)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Admin");
    }

    [HttpPost("/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/Account/SessionStatus")]
    [AllowAnonymous]
    public IActionResult SessionStatus()
    {
        var authenticated = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
        return Json(new { authenticated });
    }

    [HttpGet("/Account/AccessDenied")]
    public IActionResult AccessDenied()
    {
        return View();
    }

    internal static List<Claim> BuildAdminClaims(AdminUser user)
    {
        var stampBytes = SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash));
        var securityStamp = Convert.ToHexString(stampBytes)[..16];

        return
        [
            new(ClaimTypes.NameIdentifier, user.AdminUserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("SecurityStamp", securityStamp)
        ];
    }
}
