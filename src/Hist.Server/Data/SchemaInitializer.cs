using Octonica.ClickHouseClient;
using Hist.Server.Configuration;
using Microsoft.Extensions.Logging;

namespace Hist.Server.Data;

public class SchemaInitializer(AppSettings settings, ILogger<SchemaInitializer> logger)
{
    public async Task InitializeAsync()
    {
        logger.LogInformation("Initializing ClickHouse schema...");

        await using var conn = new ClickHouseConnection(settings.ClickHouse.WriteConnectionString);
        await conn.OpenAsync();

        var tables = new[]
        {
            """
            CREATE TABLE IF NOT EXISTS daily_bars (
                symbol    LowCardinality(String),
                date      Date,
                open      Decimal(9,2),
                high      Decimal(9,2),
                low       Decimal(9,2),
                close     Decimal(9,2),
                volume    UInt64
            ) ENGINE = ReplacingMergeTree()
            ORDER BY (symbol, date)
            """,

            """
            CREATE TABLE IF NOT EXISTS minute_bars (
                symbol    LowCardinality(String),
                ts        DateTime64(0, 'UTC'),
                open      Decimal(9,2),
                high      Decimal(9,2),
                low       Decimal(9,2),
                close     Decimal(9,2),
                volume    UInt64
            ) ENGINE = ReplacingMergeTree()
            ORDER BY (symbol, ts)
            """,

            """
            CREATE TABLE IF NOT EXISTS dividends (
                symbol    LowCardinality(String),
                ex_date   Date,
                amount    Decimal(9,6)
            ) ENGINE = ReplacingMergeTree()
            ORDER BY (symbol, ex_date)
            """,

            """
            CREATE TABLE IF NOT EXISTS splits (
                symbol      LowCardinality(String),
                date        Date,
                numerator   Decimal(9,4),
                denominator Decimal(9,4)
            ) ENGINE = ReplacingMergeTree()
            ORDER BY (symbol, date)
            """,

            """
            CREATE TABLE IF NOT EXISTS earnings (
                symbol            LowCardinality(String),
                period            Date,
                eps_actual        Nullable(Decimal(9,2)),
                eps_estimate      Nullable(Decimal(9,2)),
                revenue_actual    Nullable(Decimal(18,2)),
                revenue_estimate  Nullable(Decimal(18,2)),
                reported_date     Nullable(Date)
            ) ENGINE = ReplacingMergeTree()
            ORDER BY (symbol, period)
            """,

            """
            CREATE TABLE IF NOT EXISTS series_coverage (
                symbol      LowCardinality(String),
                data_type   LowCardinality(String),
                start_date  Nullable(Date),
                start_ts    Nullable(DateTime64(0, 'UTC')),
                end_date    Nullable(Date),
                end_ts      Nullable(DateTime64(0, 'UTC')),
                updated_at  DateTime64(0, 'UTC')
            ) ENGINE = ReplacingMergeTree(updated_at)
            ORDER BY (symbol, data_type)
            """
        };

        foreach (var sql in tables)
        {
            await using var cmd = conn.CreateCommand(sql);
            await cmd.ExecuteNonQueryAsync();
        }

        logger.LogInformation("ClickHouse schema ready.");
    }
}
