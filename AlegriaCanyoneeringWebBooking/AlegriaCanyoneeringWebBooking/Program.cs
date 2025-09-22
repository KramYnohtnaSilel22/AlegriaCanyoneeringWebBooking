using AlegriaCanyoneeringWebBooking.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// 1️⃣ Add services BEFORE Build
builder.Services.AddControllersWithViews();

// Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 33)),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)
    ));

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
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
    });

// 2️⃣ Now build the app
var app = builder.Build();

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

// API routes (Controller mappings)
app.MapControllers();  // This maps the controllers to the routes.

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
