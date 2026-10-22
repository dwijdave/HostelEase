using HostelEase.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// MVC + automatic anti-forgery (CSRF) validation on every POST
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Data access layer (ADO.NET)
builder.Services.AddSingleton<DbConnection>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<StudentRepository>();
builder.Services.AddScoped<HostelRepository>();
builder.Services.AddScoped<RoomRepository>();
builder.Services.AddScoped<AllocationRepository>();
builder.Services.AddScoped<ComplaintRepository>();
builder.Services.AddScoped<DashboardRepository>();
builder.Services.AddScoped<DbInitializer>();
builder.Services.AddScoped<DbSeeder>();

// Cookie login + role-based access (Admin / Student)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.Name = "HostelEase.Auth";
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Create/upgrade the database, then create the default accounts.
using (var scope = app.Services.CreateScope())
{
    try
    {
        scope.ServiceProvider.GetRequiredService<DbInitializer>().Initialize();
        scope.ServiceProvider.GetRequiredService<DbSeeder>().Seed();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex,
            "Database setup failed. Make sure SQL Server Express (SQLEXPRESS) is running and the connection string in appsettings.json is correct.");
    }
}

app.Run();
