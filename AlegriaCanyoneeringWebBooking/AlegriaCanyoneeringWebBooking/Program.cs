using AlegriaCanyoneeringWebBooking;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ Add services BEFORE Build
builder.Services.AddControllersWithViews()
    .AddRazorOptions(options =>
    {
        // Clear default view locations
        options.ViewLocationFormats.Clear();

        // Add your custom view locations inside /WebUI/Views/
        options.ViewLocationFormats.Add("/WebUI/Views/{1}/{0}.cshtml");     // e.g. /WebUI/Views/Home/About.cshtml
        options.ViewLocationFormats.Add("/WebUI/Views/Shared/{0}.cshtml");  // e.g. /WebUI/Views/Shared/_Layout.cshtml
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
    options.IdleTimeout = TimeSpan.FromSeconds(30); // Adjust as needed
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ===== Cookie Settings for Identity =====
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // This makes cookie expire when browser closes
    options.ExpireTimeSpan = TimeSpan.Zero;
    options.SlidingExpiration = false;
});

builder.Services.AddResponseCompression(); // 🚀 Enable compression

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("SuperAdmin", policy => policy.RequireRole("Super Admin"));
    options.AddPolicy("Operator", policy => policy.RequireRole("Operator"));
});

// 🔑 Authentication/Authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Authentication/Login";
        options.LogoutPath = "/Authentication/Logout";  // Fixed typo here: "AuAuthenticationth" → "Authentication"
        options.AccessDeniedPath = "/Authentication/AccessDenied";
    });

// 2️⃣ Now build the app
var app = builder.Build();
// Serve static files from custom location: WebUI/wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "WebUI", "wwwroot")),
    RequestPath = "" // or use "/WebUI" if you want URL prefix
});
// 3️⃣ Optional: Test DB Connection AFTER Build
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

// 4️⃣ Configure the middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseCors("AllowAll");  // Apply the CORS policy

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseResponseCompression();

// API routes (Controller mappings)
app.MapControllers();  // This maps the controllers to the routes.

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=About}/{id?}");

// Map SignalR Hub
app.MapHub<BatchCodeHub>("/batchCodeHub");

app.Run();
