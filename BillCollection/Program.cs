using BillCollection.Interfaces;
using BillCollection.Models;
using BillCollection.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using BillCollection.Models.BREB;


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
builder.Services.Configure<BREBOptions>(
    builder.Configuration.GetSection("BREB"));

builder.Services.AddHttpClient<IBREBService, BREBService>();

// =========================================================
// AUTHENTICATION
// =========================================================

builder.Services.AddAuthentication(
    CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Login page
        options.LoginPath =
            "/Account/Login";

        // Access denied page
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

        // Login session
        options.ExpireTimeSpan =
            TimeSpan.FromHours(8);

        options.SlidingExpiration = true;

        // =====================================================
        // VALIDATE COOKIE
        // =====================================================

        options.Events =
            new CookieAuthenticationEvents
            {
                OnValidatePrincipal =
                    async context =>
                    {
                        // Development environment এ
                        // application restart হলে নতুন
                        // application instance তৈরি হবে।
                        //
                        // এখানে এখন কোনো extra validation
                        // করা হচ্ছে না।

                        await Task.CompletedTask;
                    }
            };
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