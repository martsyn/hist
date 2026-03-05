using Octonica.ClickHouseClient;
using Hist.Server.Configuration;
using Hist.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace Hist.Server.Data;

public class ClickHouseRepository(AppSettings settings, ILogger<ClickHouseRepository> logger)
{
    private ClickHouseConnection WriteConn() => new(settings.ClickHouse.WriteConnectionString);
    private ClickHouseConnection ReadConn() => new(settings.ClickHouse.ReadConnectionString);

    // ── Daily Bars ──────────────────────────────────────────────────────────

    public async Task InsertDailyBarsAsync(IReadOnlyList<DailyBar> bars)
    {
        if (bars.Count == 0) return;
        await using var conn = WriteConn();
        await conn.OpenAsync();
        await using var writer = await conn.CreateColumnWriterAsync(
            "INSERT INTO daily_bars (symbol, date, open, high, low, close, volume, adj_open, adj_high, adj_low, adj_close, adj_volume) VALUES",
            CancellationToken.None);

        await writer.WriteTableAsync(
            new List<object>
            {
                bars.Select(b => b.Symbol).ToList(),
                bars.Select(b => b.Date.ToDateTime(TimeOnly.MinValue)).ToList(),
                bars.Select(b => b.Open).ToList(),
                bars.Select(b => b.High).ToList(),
                bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(),
                bars.Select(b => (ulong)b.Volume).ToList(),
                bars.Select(b => b.AdjOpen).ToList(),
                bars.Select(b => b.AdjHigh).ToList(),
                bars.Select(b => b.AdjLow).ToList(),
                bars.Select(b => b.AdjClose).ToList(),
                bars.Select(b => b.AdjVolume).ToList(),
            },
            bars.Count,
            CancellationToken.None);

        logger.LogDebug("Inserted {Count} daily bars", bars.Count);
    }

    // ── Minute Bars ──────────────────────────────────────────────────────────

    public async Task InsertMinuteBarsAsync(IReadOnlyList<MinuteBar> bars)
    {
        if (bars.Count == 0) return;
        await using var conn = WriteConn();
        await conn.OpenAsync();
        await using var writer = await conn.CreateColumnWriterAsync(
            "INSERT INTO minute_bars (symbol, ts, open, high, low, close, volume) VALUES",
            CancellationToken.None);

        await writer.WriteTableAsync(
            new List<object>
            {
                bars.Select(b => b.Symbol).ToList(),
                bars.Select(b => b.Ts.UtcDateTime).ToList(),
                bars.Select(b => b.Open).ToList(),
                bars.Select(b => b.High).ToList(),
                bars.Select(b => b.Low).ToList(),
                bars.Select(b => b.Close).ToList(),
                bars.Select(b => (ulong)b.Volume).ToList()
            },
            bars.Count,
            CancellationToken.None);
    }

    // ── Dividends ────────────────────────────────────────────────────────────

    public async Task InsertDividendsAsync(IReadOnlyList<Dividend> dividends)
    {
        if (dividends.Count == 0) return;
        await using var conn = WriteConn();
        await conn.OpenAsync();
        await using var writer = await conn.CreateColumnWriterAsync(
            "INSERT INTO dividends (symbol, ex_date, amount) VALUES",
            CancellationToken.None);

        await writer.WriteTableAsync(
            new List<object>
            {
                dividends.Select(d => d.Symbol).ToList(),
                dividends.Select(d => d.ExDate.ToDateTime(TimeOnly.MinValue)).ToList(),
                dividends.Select(d => d.Amount).ToList()
            },
            dividends.Count,
            CancellationToken.None);
    }

    // ── Splits ───────────────────────────────────────────────────────────────

    public async Task InsertSplitsAsync(IReadOnlyList<Split> splits)
    {
        if (splits.Count == 0) return;
        await using var conn = WriteConn();
        await conn.OpenAsync();
        await using var writer = await conn.CreateColumnWriterAsync(
            "INSERT INTO splits (symbol, date, numerator, denominator) VALUES",
            CancellationToken.None);

        await writer.WriteTableAsync(
            new List<object>
            {
                splits.Select(s => s.Symbol).ToList(),
                splits.Select(s => s.Date.ToDateTime(TimeOnly.MinValue)).ToList(),
                splits.Select(s => s.Numerator).ToList(),
                splits.Select(s => s.Denominator).ToList()
            },
            splits.Count,
            CancellationToken.None);
    }

    // ── Earnings ─────────────────────────────────────────────────────────────

    public async Task InsertEarningsAsync(IReadOnlyList<Earning> earnings)
    {
        if (earnings.Count == 0) return;
        await using var conn = WriteConn();
        await conn.OpenAsync();
        await using var writer = await conn.CreateColumnWriterAsync(
            "INSERT INTO earnings (symbol, period, eps_actual, eps_estimate, revenue_actual, revenue_estimate, reported_date) VALUES",
            CancellationToken.None);

        await writer.WriteTableAsync(
            new List<object>
            {
                earnings.Select(e => e.Symbol).ToList(),
                earnings.Select(e => e.Period.ToDateTime(TimeOnly.MinValue)).ToList(),
                earnings.Select(e => e.EpsActual).ToList(),
                earnings.Select(e => e.EpsEstimate).ToList(),
                earnings.Select(e => e.RevenueActual).ToList(),
                earnings.Select(e => e.RevenueEstimate).ToList(),
                earnings.Select(e => e.ReportedDate.HasValue
                    ? (object?)e.ReportedDate.Value.ToDateTime(TimeOnly.MinValue)
                    : null).ToList()
            },
            earnings.Count,
            CancellationToken.None);
    }

    // ── Series Coverage ───────────────────────────────────────────────────────

    public async Task UpsertCoverageAsync(SeriesCoverage coverage)
    {
        await using var conn = WriteConn();
        await conn.OpenAsync();
        await using var writer = await conn.CreateColumnWriterAsync(
            "INSERT INTO series_coverage (symbol, data_type, start_date, start_ts, end_date, end_ts, updated_at) VALUES",
            CancellationToken.None);

        await writer.WriteTableAsync(
            new List<object>
            {
                new List<string> { coverage.Symbol },
                new List<string> { coverage.DataType },
                new List<DateOnly?> { coverage.StartDate },
                new List<DateTime?> { coverage.StartTs?.UtcDateTime },
                new List<DateOnly?> { coverage.EndDate },
                new List<DateTime?> { coverage.EndTs?.UtcDateTime },
                new List<DateTime> { coverage.UpdatedAt.UtcDateTime }
            },
            1,
            CancellationToken.None);
    }

    public async Task<List<SeriesCoverage>> GetAllCoverageAsync()
    {
        await using var conn = ReadConn();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand(
            "SELECT symbol, data_type, start_date, start_ts, end_date, end_ts, updated_at " +
            "FROM series_coverage FINAL ORDER BY symbol, data_type");
        await using var reader = await cmd.ExecuteReaderAsync();

        var results = new List<SeriesCoverage>();
        while (await reader.ReadAsync())
        {
            results.Add(new SeriesCoverage(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2)),
                reader.IsDBNull(3) ? null : new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4)),
                reader.IsDBNull(5) ? null : new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
                new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero)
            ));
        }
        return results;
    }

    public async Task<List<string>> GetAllSymbolsAsync()
    {
        await using var conn = ReadConn();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand(
            "SELECT DISTINCT symbol FROM series_coverage FINAL ORDER BY symbol");
        await using var reader = await cmd.ExecuteReaderAsync();

        var symbols = new List<string>();
        while (await reader.ReadAsync())
            symbols.Add(reader.GetString(0));
        return symbols;
    }
}
