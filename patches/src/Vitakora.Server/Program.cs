using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Vitakora.Server.Data;
using Vitakora.Server.Hubs;
using Vitakora.Server.Options;
using Vitakora.Server.Services;
using Vitakora.Shared;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");

builder.Services.Configure<VitakoraOptions>(builder.Configuration.GetSection("Vitakora"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Vitakora");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "vitakora.db");

builder.Services.AddDbContext<VitakoraDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSignalR(o => o.AddFilter<SessionHubFilter>());
builder.Services.AddScoped<SessionHubFilter>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<ConnectionRegistry>();
builder.Services.AddHostedService<LanDiscoveryService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", product = "Vitakora", version = "0.4.0" }));
app.MapGet("/api/workstations", async (VitakoraDbContext db) =>
    await db.Workstations.Where(x => x.IsActive).OrderBy(x => x.Name)
        .Select(x => new WorkstationDto(x.Id, x.Name)).ToListAsync());
app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth, CancellationToken ct) =>
{
    var result = await auth.LoginAsync(req, ct);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});
app.MapGet("/api/directory", async (VitakoraDbContext db) =>
{
    var users = await db.Users.Where(x => x.IsActive)
        .Select(x => new DirectoryItemDto(x.Id, x.DisplayName, RecipientKind.User)).ToListAsync();
    var workstations = await db.Workstations.Where(x => x.IsActive)
        .Select(x => new DirectoryItemDto(x.Id, x.Name, RecipientKind.Workstation)).ToListAsync();
    var groups = await db.Groups.Where(x => x.IsActive)
        .Select(x => new DirectoryItemDto(x.Id, x.Name, RecipientKind.Group)).ToListAsync();
    return users.Concat(workstations).Concat(groups).OrderBy(x => x.Kind).ThenBy(x => x.Name);
}).RequireAuthorization();
app.MapHub<ChatHub>("/hubs/chat");

await SeedAsync(app.Services);
app.Run();

static async Task SeedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VitakoraDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (!await db.Workstations.AnyAsync())
        db.Workstations.Add(new Workstation { Name = "PUESTO-01" });

    if (!await db.Users.AnyAsync())
    {
        var user = new AppUser
        {
            Username = "master",
            DisplayName = "Master Vitakora",
            Role = UserRole.Master
        };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, "Vitakora2026!");
        db.Users.Add(user);
    }

    await db.SaveChangesAsync();
}
