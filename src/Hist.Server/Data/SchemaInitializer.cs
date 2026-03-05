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
                symbol     LowCardinality(String),
                date       Date,
                open       Decimal(9,2),
                high       Decimal(9,2),
                low        Decimal(9,2),
                close      Decimal(9,2),
                volume     UInt64,
                adj_open   Decimal(12,4) DEFAULT 0,
                adj_high   Decimal(12,4) DEFAULT 0,
                adj_low    Decimal(12,4) DEFAULT 0,
                adj_close  Decimal(12,4) DEFAULT 0,
                adj_volume UInt64 DEFAULT 0
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

        // Migrations: add adj columns to daily_bars for existing installs
        var migrations = new[]
        {
            "ALTER TABLE daily_bars ADD COLUMN IF NOT EXISTS adj_open   Decimal(12,4) DEFAULT 0",
            "ALTER TABLE daily_bars ADD COLUMN IF NOT EXISTS adj_high   Decimal(12,4) DEFAULT 0",
            "ALTER TABLE daily_bars ADD COLUMN IF NOT EXISTS adj_low    Decimal(12,4) DEFAULT 0",
            "ALTER TABLE daily_bars ADD COLUMN IF NOT EXISTS adj_close  Decimal(12,4) DEFAULT 0",
            "ALTER TABLE daily_bars ADD COLUMN IF NOT EXISTS adj_volume UInt64 DEFAULT 0",
        };

        foreach (var sql in migrations)
        {
            await using var cmd = conn.CreateCommand(sql);
            await cmd.ExecuteNonQueryAsync();
        }

        // Views
        var views = new[]
        {
            // Split+dividend adjusted daily bars (uses Tiingo's pre-computed adj prices).
            // Rows where adj_close = 0 are excluded (symbol not yet re-collected after migration).
            """
            CREATE OR REPLACE VIEW daily_bars_adjusted AS
            SELECT symbol, date,
                   adj_open   AS open,
                   adj_high   AS high,
                   adj_low    AS low,
                   adj_close  AS close,
                   adj_volume AS volume
            FROM daily_bars FINAL
            WHERE adj_close > 0
            """,

            // Split-adjusted minute bars computed on-the-fly from the splits table.
            // Uses ASOF JOIN: for each bar, find the most recent split boundary <= bar ts,
            // then divide price by the cumulative split factor from that point forward.
            """
            CREATE OR REPLACE VIEW minute_bars_adjusted AS
            WITH
            total_factors AS (
                SELECT symbol,
                       exp(sum(log(toFloat64(numerator) / toFloat64(denominator)))) AS total_factor
                FROM splits FINAL
                WHERE numerator > 0 AND denominator > 0
                GROUP BY symbol
            ),
            cum_to AS (
                SELECT symbol, date,
                       exp(sum(log(toFloat64(numerator) / toFloat64(denominator)))
                           OVER (PARTITION BY symbol ORDER BY date ASC
                                 ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)) AS cum_factor
                FROM splits FINAL
                WHERE numerator > 0 AND denominator > 0
            ),
            intervals AS (
                SELECT c.symbol,
                       toDateTime64(c.date, 0, 'UTC') AS start_ts,
                       t.total_factor / c.cum_factor  AS factor
                FROM cum_to c
                JOIN total_factors t ON c.symbol = t.symbol
                UNION ALL
                SELECT symbol,
                       toDateTime64('1900-01-01 00:00:00', 0, 'UTC'),
                       total_factor
                FROM total_factors
            )
            SELECT b.symbol,
                   b.ts,
                   b.open  / if(sf.factor > 0, sf.factor, 1.0) AS open,
                   b.high  / if(sf.factor > 0, sf.factor, 1.0) AS high,
                   b.low   / if(sf.factor > 0, sf.factor, 1.0) AS low,
                   b.close / if(sf.factor > 0, sf.factor, 1.0) AS close,
                   toUInt64(b.volume * if(sf.factor > 0, sf.factor, 1.0)) AS volume
            FROM (SELECT * FROM minute_bars FINAL) b
            ASOF LEFT JOIN intervals sf
                ON b.symbol = sf.symbol AND b.ts >= sf.start_ts
            """,
        };

        foreach (var sql in views)
        {
            await using var cmd = conn.CreateCommand(sql);
            await cmd.ExecuteNonQueryAsync();
        }

        logger.LogInformation("ClickHouse schema ready.");
    }
}
