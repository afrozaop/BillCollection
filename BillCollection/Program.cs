using BillCollection.Interfaces;
using BillCollection.Models;
using BillCollection.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// MVC
// =========================================================

builder.Services.AddControllersWithViews();

// =========================================================
// DATABASE
// =========================================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration
            .GetConnectionString("DefaultConnection")
    ));

// =========================================================
// SERVICES
// =========================================================

builder.Services.AddScoped<
    IAccountService,
    AccountService>();

builder.Services.AddScoped<
    IAdminService,
    AdminService>();

builder.Services.AddScoped<
    IBranchService,
    BranchService>();

// =========================================================
// AUTHENTICATION
// =========================================================

builder.Services.AddAuthentication(
    CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";

        options.AccessDeniedPath =
            "/Account/AccessDenied";

        // Unique authentication cookie
        options.Cookie.Name =
            "BillCollection.Auth";

        options.Cookie.HttpOnly = true;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;

        options.Cookie.SameSite =
            SameSiteMode.Lax;

        options.Cookie.IsEssential = true;

        // Login session = 8 hours
        options.ExpireTimeSpan =
            TimeSpan.FromHours(8);

        options.SlidingExpiration = true;
    });

// =========================================================
// BUILD APPLICATION
// =========================================================

var app = builder.Build();

// =========================================================
// ERROR HANDLING
// =========================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Home/Error");

    app.UseHsts();
}

// =========================================================
// HTTP PIPELINE
// =========================================================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Authentication must come before Authorization
app.UseAuthentication();

app.UseAuthorization();

// =========================================================
// DEFAULT ROUTE
// =========================================================

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Account}/{action=Login}/{id?}");

app.Run();