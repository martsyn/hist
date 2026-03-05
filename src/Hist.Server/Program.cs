using DotNetEnv;
using Hist.Server.Api;
using Hist.Server.Collection;
using Hist.Server.Collection.Adapters;
using Hist.Server.Collection.Adapters.Tiingo;
using Hist.Server.Collection.Adapters.Yahoo;
using Hist.Server.Configuration;
using Hist.Server.Data;
using Hist.Server.Scheduling;
using OoplesFinance.YahooFinanceAPI;
using Quartz;

// Load .env from working directory (or /app/hist/.env in container)
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────
var appSettings = new AppSettings
{
    Port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port) ? port : 8088,
    ClickHouse = new()
    {
        Host = Environment.GetEnvironmentVariable("CH_HOST") ?? "127.0.0.1",
        TcpPort = int.TryParse(Environment.GetEnvironmentVariable("CH_TCP_PORT"), out var chPort) ? chPort : 9000,
        WriteUser = Environment.GetEnvironmentVariable("CH_WRITE_USER") ?? "hist_writer",
        WritePassword = Environment.GetEnvironmentVariable("CH_WRITE_PASSWORD") ?? "changeme",
        ReadUser = Environment.GetEnvironmentVariable("CH_READ_USER") ?? "hist_reader",
        ReadPassword = Environment.GetEnvironmentVariable("CH_READ_PASSWORD") ?? "changeme_reader",
    },
    Tiingo = new()
    {
        Token = Environment.GetEnvironmentVariable("TIINGO_TOKEN") ?? "",
        MaxThreads = int.TryParse(Environment.GetEnvironmentVariable("TIINGO_MAX_THREADS"), out var tt) ? tt : 4
    },
    Schedules = new()
    {
        DailyBars = Environment.GetEnvironmentVariable("SCHEDULE_DAILY_BARS") ?? "0 0 6 * * ?",
        MinuteBars = Environment.GetEnvironmentVariable("SCHEDULE_MINUTE_BARS") ?? "0 30 6 * * ?",
        Dividends = Environment.GetEnvironmentVariable("SCHEDULE_DIVIDENDS") ?? "0 0 7 * * ?",
        Splits = Environment.GetEnvironmentVariable("SCHEDULE_SPLITS") ?? "0 15 7 * * ?",
        Earnings = Environment.GetEnvironmentVariable("SCHEDULE_EARNINGS") ?? "0 30 7 * * ?",
    }
};

builder.Services.AddSingleton(appSettings);

// ── Data Layer ───────────────────────────────────────────────────────────────
builder.Services.AddSingleton<SchemaInitializer>();
builder.Services.AddSingleton<ClickHouseRepository>();

// ── Collection Layer ─────────────────────────────────────────────────────────
builder.Services.AddSingleton<CollectionQueue>();

// Shared HttpClient for Tiingo (token set per-request in adapters)
builder.Services.AddHttpClient("tiingo");

// OoplesFinance (earnings only)
builder.Services.AddSingleton<YahooClient>();

// Adapters
var tiingoToken = appSettings.Tiingo.Token;
builder.Services.AddSingleton<TiingoEodAdapter>(sp => new TiingoEodAdapter(
    sp.GetRequiredService<ClickHouseRepository>(),
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("tiingo"),
    tiingoToken,
    sp.GetRequiredService<ILogger<TiingoEodAdapter>>()));
builder.Services.AddSingleton<TiingoIntraAdapter>(sp => new TiingoIntraAdapter(
    sp.GetRequiredService<ClickHouseRepository>(),
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("tiingo"),
    tiingoToken,
    sp.GetRequiredService<ILogger<TiingoIntraAdapter>>()));
builder.Services.AddSingleton<YahooEarningsAdapter>();
builder.Services.AddSingleton<IDataAdapter, TiingoAdapter>();

// Worker pool
builder.Services.AddHostedService<WorkerPool>();

// ── Quartz Scheduler ─────────────────────────────────────────────────────────
builder.Services.AddQuartz(q =>
{
    void AddJob(string name, string dataType, string cron)
    {
        var key = new JobKey(name);
        q.AddJob<DailyCollectionJob>(opts => opts
            .WithIdentity(key)
            .UsingJobData(DailyCollectionJob.DataTypeKey, dataType)
            .StoreDurably());

        q.AddTrigger(opts => opts
            .ForJob(key)
            .WithIdentity($"{name}-trigger")
            .WithCronSchedule(cron));
    }

    AddJob("daily-bars", "daily_bars", appSettings.Schedules.DailyBars);
    AddJob("minute-bars", "minute_bars", appSettings.Schedules.MinuteBars);
    AddJob("dividends", "dividends", appSettings.Schedules.Dividends);
    AddJob("splits", "splits", appSettings.Schedules.Splits);
    AddJob("earnings", "earnings", appSettings.Schedules.Earnings);
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// ── HTTP / API ───────────────────────────────────────────────────────────────
builder.WebHost.UseUrls($"http://0.0.0.0:{appSettings.Port}");

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower);

var app = builder.Build();

// Initialize DB schema
var schema = app.Services.GetRequiredService<SchemaInitializer>();
await schema.InitializeAsync();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// ── API Routes ───────────────────────────────────────────────────────────────
var api = app.MapGroup("/api");
api.MapGroup("/queue").MapQueueEndpoints();
api.MapGroup("/universe").MapUniverseEndpoints();
api.MapGroup("/schedules").MapScheduleEndpoints();

// SPA fallback
app.MapFallbackToFile("index.html");

app.Run();
