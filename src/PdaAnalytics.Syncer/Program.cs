using PdaAnalytics.Data;
using PdaAnalytics.Syncer.Services;
using PdaAnalytics.Syncer.Workers;

// ─── Console banner ──────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║   PDA Analytics — Data Syncer            ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Запуск...");

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // ─── Logging ────────────────────────────────────────────────────
    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole(o =>
    {
        o.TimestampFormat = "[HH:mm:ss] ";
        o.SingleLine = true;
    });
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
    builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

    // ─── Не убиваем хост при падении BackgroundService ──────────────
    builder.Services.Configure<HostOptions>(opts =>
    {
        opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });

    // ─── Data Layer ──────────────────────────────────────────────
    builder.Services.AddAnalyticsData(builder.Configuration);

    // ─── Services ────────────────────────────────────────────────
    builder.Services.AddScoped<SyncService>();

    // ─── Discord Integration ─────────────────────────────────────
    builder.Services.AddSingleton<DiscordQueue>();
    builder.Services.AddSingleton<DiscordMentionParser>();

    // Регистрируем DiscordDispatcherService как Singleton + HostedService
    builder.Services.AddSingleton<DiscordDispatcherService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<DiscordDispatcherService>());

    builder.Services.AddHttpClient("discord", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("User-Agent", "PdaAnalytics/1.0");
    });

    // ─── Background Worker ──────────────────────────────────────
    builder.Services.AddHostedService<DataSyncWorker>();

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DI сконфигурирован, строим хост...");
    var host = builder.Build();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Host построен, запускаем...");
    host.Run();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"\n[FATAL] {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    if (ex.InnerException != null)
    {
        Console.Error.WriteLine($"\n  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        Console.Error.WriteLine(ex.InnerException.StackTrace);
    }
    Console.ResetColor();
    Environment.ExitCode = 1;
}
finally
{
    Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Syncer завершён.");
}
