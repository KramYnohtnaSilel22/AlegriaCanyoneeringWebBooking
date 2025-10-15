using AlegriaCanyoneeringWebBooking;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ Add services BEFORE Build
builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        options.ViewLocationFormats.Clear();
        options.ViewLocationFormats.Add("/WebUI/Views/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/WebUI/Views/Shared/{0}.cshtml");
    });

// Add SignalR service
builder.Services.AddSignalR();

// Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 33)),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)
    ));

builder.Services.AddScoped<IGuestService, GuestService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// ===== Session =====
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.Name = "AlegriaCanyoneering.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Changed from 30 seconds to 30 minutes
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// 🔑 Authentication - MUST come before Authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Authentication/Login";
        options.LogoutPath = "/Authentication/Logout";
        options.AccessDeniedPath = "/Authentication/AccessDenied";
        options.Cookie.Name = "AlegriaCanyoneering.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Authorization - MUST come after Authentication
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy => policy.RequireRole("Super Admin"));
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Operator", policy => policy.RequireRole("Operator"));
    options.AddPolicy("AdminOrSuperAdmin", policy =>
        policy.RequireRole("Admin", "Super Admin"));
});

builder.Services.AddResponseCompression();

// 2️⃣ Build the app
var app = builder.Build();

// Serve static files
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "WebUI", "wwwroot")),
    RequestPath = ""
});

// 3️⃣ Test DB Connection
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var canConnect = context.Database.CanConnect();
        Console.WriteLine($"Database connection successful: {canConnect}");
        if (canConnect)
        {
            context.Database.EnsureCreated();
            Console.WriteLine("Database ensured created successfully");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while connecting to the database: {ex.Message}");
    }
}

// 4️⃣ Configure middleware pipeline - ORDER MATTERS!
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ✅ CRITICAL: Authentication MUST come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.UseSession();
app.UseResponseCompression();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=About}/{id?}");

app.MapHub<BatchCodeHub>("/batchCodeHub");

app.Run();