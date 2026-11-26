using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortfolioStudio.Data;
using PortfolioStudio.Models;
using PortfolioStudio.Services;

var builder = WebApplication.CreateBuilder(args);

var keysDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("PortfolioStudio");

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();

var configuredProvider = builder.Configuration["DatabaseProvider"];
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

var provider = ResolveDatabaseProvider(configuredProvider, rawConnectionString);

builder.Services.AddDbContext<PortfolioDbContext>(options =>
{
    switch (provider)
    {
        case "SqlServer":
            if (string.IsNullOrWhiteSpace(rawConnectionString))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for SqlServer.");
            options.UseSqlServer(rawConnectionString);
            break;

        case "PostgreSql":
            if (string.IsNullOrWhiteSpace(rawConnectionString))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection or DATABASE_URL is required for PostgreSql.");
            options.UseNpgsql(NormalizePostgresConnectionString(rawConnectionString));
            break;

        default:
            var appDataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
            Directory.CreateDirectory(appDataDir);
            var sqliteCs = string.IsNullOrWhiteSpace(rawConnectionString)
                ? $"Data Source={Path.Combine(appDataDir, "portfolio.db")}"
                : rawConnectionString;
            options.UseSqlite(sqliteCs);
            break;
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("contact", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "PortfolioStudio.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var userIdStr = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var stamp = context.Principal?.FindFirstValue("SecurityStamp");

                if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrEmpty(stamp))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<PortfolioDbContext>();
                var user = await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(u => u.AdminUserId == userId);
                if (user == null || !user.IsActive || ComputeSecurityStamp(user.PasswordHash) != stamp)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Security headers + strict anti-caching on authenticated/admin routes to block Back/Forward cache replay
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    var path = context.Request.Path;
    if (path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "-1";
            return Task.CompletedTask;
        });
    }

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

await DbInitializer.InitializeAsync(app.Services, app.Configuration);

app.MapControllers();
app.MapDefaultControllerRoute();

app.Run();

static string ComputeSecurityStamp(string passwordHash)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash));
    return Convert.ToHexString(bytes)[..16];
}

static string ResolveDatabaseProvider(string? configured, string? connectionString)
{
    if (!string.IsNullOrWhiteSpace(configured))
        return configured.Trim();

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            return "PostgreSql";

        if (connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase))
            return "SqlServer";
    }

    return "Sqlite";
}

static string NormalizePostgresConnectionString(string input)
{
    if (!input.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !input.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return input;

    var uri = new Uri(input);
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var database = uri.AbsolutePath.TrimStart('/');
    var port = uri.Port > 0 ? uri.Port : 5432;

    return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}
