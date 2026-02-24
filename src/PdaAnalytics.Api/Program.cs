using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PdaAnalytics.Api.Hubs;
using PdaAnalytics.Api.Services;
using PdaAnalytics.Data;
using PdaAnalytics.Domain.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ───────────────────────────────────────────
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// ─── Data Layer ──────────────────────────────────────────────
builder.Services.AddAnalyticsData(builder.Configuration);

// ─── Services ────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<ServerSettingsService>();
builder.Services.AddSingleton<SteamService>();
builder.Services.AddHttpClient("steam", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("User-Agent", "PdaAnalytics/1.0");
});

// ─── JWT Authentication ─────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // Поддержка JWT-токена в query string для SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ─── Controllers & Swagger ───────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ─── SignalR ─────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── Background Services ────────────────────────────────────
builder.Services.AddHostedService<MessageBroadcastService>();

// ─── CORS ────────────────────────────────────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ─── Database Seeding ────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
    await db.Database.MigrateAsync();

    // Добавляем discord_role_id если отсутствует (патч поверх миграции)
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE discord_faction_mention_configs 
        ADD COLUMN IF NOT EXISTS discord_role_id VARCHAR(30) NULL
        """);

    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.SeedSuperAdminAsync("admin");
}

// Seed настроек MariaDB из appsettings (только первый запуск)
var settingsService = app.Services.GetRequiredService<ServerSettingsService>();
await settingsService.SeedFromConfigAsync(builder.Configuration);

// ─── Middleware ───────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<PdaHub>("/hubs/pda");

app.Run();
